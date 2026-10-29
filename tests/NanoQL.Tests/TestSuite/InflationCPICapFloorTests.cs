// SPDX-FileCopyrightText: Copyright (C) 2011 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/inflationcpicapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>
/// QuantLib's CPI cap and floor cases: the price surface reproducing its quotes, and the interpolating engine
/// reproducing a quoted cap. The index's fixings are in the cases' evaluation context.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationCPICapFloorTests
{
    [Fact]
    public void Cpicapfloorpricesurface()
    {
        // Checking CPI cap/floor against price surface...
        var common = new CommonVars();
        const double Nominal = 1.0;
        var cpiSurf = InterpolatedCPICapFloorTermPriceSurface<Bilinear>.Create(
            Nominal,
            common.BaseZeroRate,
            common.ObservationLag,
            common.Calendar,
            common.Convention,
            common.DcZCIIS,
            common.Ii,
            CPIInterpolationType.Flat,
            common.NominalUK,
            common.CStrikesUK,
            common.FStrikesUK,
            common.CfMaturitiesUK,
            common.CPriceUK,
            common.FPriceUK,
            common.Context);

        // test code - note order of indices
        for (int i = 0; i < common.FStrikesUK.Length; i++)
        {
            double qK = common.FStrikesUK[i];
            for (int j = 0; j < common.CfMaturitiesUK.Length; j++)
            {
                Period t = common.CfMaturitiesUK[j];
                double a = common.FPriceUK[i, j];
                double b = cpiSurf.FloorPrice(t, qK);
                Assert.True(
                    Math.Abs(a - b) < 1e-7, $"cannot reproduce cpi floor data from surface: {a} vs constructed = {b}");
            }
        }

        for (int i = 0; i < common.CStrikesUK.Length; i++)
        {
            double qK = common.CStrikesUK[i];
            for (int j = 0; j < common.CfMaturitiesUK.Length; j++)
            {
                Period t = common.CfMaturitiesUK[j];
                double a = common.CPriceUK[i, j];
                double b = cpiSurf.CapPrice(t, qK);
                Assert.True(
                    Math.Abs(a - b) < 1e-7, $"cannot reproduce cpi cap data from surface: {a} vs constructed = {b}");
            }
        }

        // Test the price method also i.e. does it pick out the correct premium?
        // Look up premium from surface at 3 years and strike of 1%
        // Expect, as 1% < ATM, to get back floor premium at 1% i.e. 53.61 bps
        double premium = cpiSurf.Price(new Period(3, TimeUnit.Years), 0.01, common.Context);
        double expPremium = common.FPriceUK[2, 0];
        Assert.True(
            Math.Abs(premium - expPremium) <= 1e-12,
            $"The requested premium, {premium}, does not equal the expected premium, {expPremium}.");
    }

    [Fact]
    public void Cpicapfloorpricer()
    {
        // Checking CPI cap/floor pricer...
        var common = new CommonVars();
        const double Nominal = 1.0;
        CPICapFloorTermPriceSurface cpiCFpriceSurf = InterpolatedCPICapFloorTermPriceSurface<Bilinear>.Create(
            Nominal,
            common.BaseZeroRate,
            common.ObservationLag,
            common.Calendar,
            common.Convention,
            common.DcZCIIS,
            common.Ii,
            CPIInterpolationType.Flat,
            common.NominalUK,
            common.CStrikesUK,
            common.FStrikesUK,
            common.CfMaturitiesUK,
            common.CPriceUK,
            common.FPriceUK,
            common.Context);

        // interpolation pricer first
        // N.B. no new instrument required but we do need a new pricer
        Date startDate = common.Context.EvaluationDate;
        Date maturity = startDate + new Period(3, TimeUnit.Years);
        Calendar fixCalendar = new UnitedKingdom(), payCalendar = new UnitedKingdom();
        const BusinessDayConvention FixConvention = BusinessDayConvention.Unadjusted;
        const BusinessDayConvention PayConvention = BusinessDayConvention.ModifiedFollowing;
        const double Strike = 0.03;
        const CPIInterpolationType ObservationInterpolation = CPIInterpolationType.Linear; // because the surface interpolates
        double baseCPI = CPI.LaggedFixing(
            common.Ii, startDate, common.ObservationLag, ObservationInterpolation, common.Context);
        var aCap = new CPICapFloor(
            OptionType.Call,
            Nominal,
            startDate, // start date of contract (only)
            baseCPI,
            maturity, // this is pre-adjustment!
            fixCalendar,
            FixConvention,
            payCalendar,
            PayConvention,
            Strike,
            common.Ii,
            common.ObservationLag,
            ObservationInterpolation);

        double npv = InterpolatingCPICapFloorEngine.Calculate(aCap, common.Context, cpiCFpriceSurf).NPV!.Value;

        // We should get back the cap premium at strike 0.03 i.e. 227.6 bps
        double cached = common.CPriceUK[0, 0];
        Assert.True(
            Math.Abs(cached - npv) < 1e-10,
            $"InterpolatingCPICapFloorEngine does not reproduce cached price: {cached} vs {npv}");
    }

    // QuantLib's makeHelpers, on its zero-coupon helper.
    private static List<BootstrapHelper<ZeroInflationTermStructure>> MakeHelpers(
        Datum[] iiData,
        ZeroInflationIndex ii,
        Period observationLag,
        Calendar calendar,
        BusinessDayConvention bdc,
        DayCounter dc,
        EvaluationContext context)
    {
        var instruments = new List<BootstrapHelper<ZeroInflationTermStructure>>();
        foreach (Datum datum in iiData)
        {
            Date maturity = datum.Date;
            double quote = datum.Rate / 100.0;
            var anInstrument = new ZeroCouponInflationSwapHelper(
                quote, observationLag, maturity, calendar, bdc, dc, ii, CPIInterpolationType.Flat, context);
            instruments.Add(anInstrument);
        }

        return instruments;
    }

    // QuantLib's Datum.
    private readonly record struct Datum(Date Date, double Rate);

    // QuantLib's CommonVars, the fixings in its evaluation context; the members no case reads aren't kept.
    internal sealed class CommonVars
    {
        public CommonVars()
        {
            // usual setup
            Calendar = new UnitedKingdom();
            Convention = BusinessDayConvention.ModifiedFollowing;
            var today = new Date(1, June, 2010);
            Date evaluationDate = Calendar.Adjust(today);
            Context = new EvaluationContext { EvaluationDate = evaluationDate };
            DcZCIIS = new ActualActual(ActualActualConvention.ISDA);
            DayCounter dcNominal = new ActualActual(ActualActualConvention.ISDA);

            // UK RPI index fixing data
            Schedule rpiSchedule = new MakeSchedule()
                .From(new Date(1, July, 2007))
                .To(new Date(1, April, 2010))
                .WithFrequency(Frequency.Monthly)
                .ToSchedule();
            double[] fixData =
            [
                206.1, 207.3, 208.0, 208.9, 209.7, 210.9,
                209.8, 211.4, 212.1, 214.0, 215.1, 216.8, //  2008
                216.5, 217.2, 218.4, 217.7, 216.0, 212.9,
                210.1, 211.4, 211.3, 211.5, 212.8, 213.4, //  2009
                213.4, 214.4, 215.3, 216.0, 216.6, 218.0,
                217.9, 219.2, 220.7, 222.8, //  2010
            ];

            // link from cpi index to cpi TS
            var ii = new UKRPI();
            for (int i = 0; i < rpiSchedule.Size; i++)
            {
                // force overwrite in case multiple use
                Context = Context with { Fixings = Context.Fixings.AddFixing(ii, rpiSchedule[i], fixData[i], true) };
            }

            Datum[] nominalData =
            [
                new(new Date(2, June, 2010), 0.499997),
                new(new Date(3, June, 2010), 0.524992),
                new(new Date(8, June, 2010), 0.524974),
                new(new Date(15, June, 2010), 0.549942),
                new(new Date(22, June, 2010), 0.549913),
                new(new Date(1, July, 2010), 0.574864),
                new(new Date(2, August, 2010), 0.624668),
                new(new Date(1, September, 2010), 0.724338),
                new(new Date(16, September, 2010), 0.769461),
                new(new Date(1, December, 2010), 0.997501),

                // { Date( 16, December, 2010), 0.838164 },
                new(new Date(17, March, 2011), 0.916996),
                new(new Date(16, June, 2011), 0.984339),
                new(new Date(22, September, 2011), 1.06085),
                new(new Date(22, December, 2011), 1.141788),
                new(new Date(1, June, 2012), 1.504426),
                new(new Date(3, June, 2013), 1.92064),
                new(new Date(2, June, 2014), 2.290824),
                new(new Date(1, June, 2015), 2.614394),
                new(new Date(1, June, 2016), 2.887445),
                new(new Date(1, June, 2017), 3.122128),
                new(new Date(1, June, 2018), 3.322511),
                new(new Date(3, June, 2019), 3.483997),
                new(new Date(1, June, 2020), 3.616896),
                new(new Date(1, June, 2022), 3.8281),
                new(new Date(2, June, 2025), 4.0341),
                new(new Date(3, June, 2030), 4.070854),
                new(new Date(1, June, 2035), 4.023202),
                new(new Date(1, June, 2040), 3.954748),
                new(new Date(1, June, 2050), 3.870953),
                new(new Date(1, June, 2060), 3.85298),
                new(new Date(2, June, 2070), 3.757542),
                new(new Date(3, June, 2080), 3.651379),
            ];
            var nomD = new List<Date>();
            var nomR = new List<double>();
            foreach (Datum d in nominalData)
            {
                nomD.Add(d.Date);
                nomR.Add(d.Rate / 100.0);
            }

            NominalUK = new InterpolatedZeroCurve<Linear>(nomD, nomR, dcNominal);

            // now build the zero inflation curve
            ObservationLag = new Period(2, TimeUnit.Months);
            Datum[] zciisData =
            [
                new(new Date(1, June, 2011), 3.087),
                new(new Date(1, June, 2012), 3.12),
                new(new Date(1, June, 2013), 3.059),
                new(new Date(1, June, 2014), 3.11),
                new(new Date(1, June, 2015), 3.15),
                new(new Date(1, June, 2016), 3.207),
                new(new Date(1, June, 2017), 3.253),
                new(new Date(1, June, 2018), 3.288),
                new(new Date(1, June, 2019), 3.314),
                new(new Date(1, June, 2020), 3.401),
                new(new Date(1, June, 2022), 3.458),
                new(new Date(1, June, 2025), 3.52),
                new(new Date(1, June, 2030), 3.655),
                new(new Date(1, June, 2035), 3.668),
                new(new Date(1, June, 2040), 3.695),
                new(new Date(1, June, 2050), 3.634),
                new(new Date(1, June, 2060), 3.629),
            ];

            // now build the helpers ...
            List<BootstrapHelper<ZeroInflationTermStructure>> helpers =
                MakeHelpers(zciisData, ii, ObservationLag, Calendar, Convention, DcZCIIS, Context);

            // we can use historical or first ZCIIS for this
            // we know historical is WAY off market-implied, so use market implied flat.
            BaseZeroRate = zciisData[0].Rate / 100.0;
            Date baseDate = ii.LastFixingDate(Context);
            var pCPIts = new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
                evaluationDate, baseDate, ii.Frequency, DcZCIIS, helpers, Context);

            // make sure that the index has the latest zero inflation term structure
            Ii = new UKRPI(pCPIts);

            // cpi CF price surf data
            CfMaturitiesUK =
            [
                new(3, TimeUnit.Years), new(5, TimeUnit.Years), new(7, TimeUnit.Years), new(10, TimeUnit.Years),
                new(15, TimeUnit.Years), new(20, TimeUnit.Years), new(30, TimeUnit.Years),
            ];
            CStrikesUK = [0.03, 0.04, 0.05, 0.06];
            FStrikesUK = [-0.01, 0, 0.01, 0.02];
            double[,] cPrice =
            {
                { 227.6, 100.27, 38.8, 14.94 },
                { 345.32, 127.9, 40.59, 14.11 },
                { 477.95, 170.19, 50.62, 16.88 },
                { 757.81, 303.95, 107.62, 43.61 },
                { 1140.73, 481.89, 168.4, 63.65 },
                { 1537.6, 607.72, 172.27, 54.87 },
                { 2211.67, 839.24, 184.75, 45.03 },
            };
            double[,] fPrice =
            {
                { 15.62, 28.38, 53.61, 104.6 },
                { 21.45, 36.73, 66.66, 129.6 },
                { 24.45, 42.08, 77.04, 152.24 },
                { 39.25, 63.52, 109.2, 203.44 },
                { 36.82, 63.62, 116.97, 232.73 },
                { 39.7, 67.47, 121.79, 238.56 },
                { 41.48, 73.9, 139.75, 286.75 },
            };

            // now load the data into vector and Matrix classes
            CPriceUK = new Matrix(CStrikesUK.Length, CfMaturitiesUK.Length);
            FPriceUK = new Matrix(FStrikesUK.Length, CfMaturitiesUK.Length);
            for (int i = 0; i < CStrikesUK.Length; i++)
            {
                for (int j = 0; j < CfMaturitiesUK.Length; j++)
                {
                    CPriceUK[i, j] = cPrice[j, i] / 10000.0;
                }
            }

            for (int i = 0; i < FStrikesUK.Length; i++)
            {
                for (int j = 0; j < CfMaturitiesUK.Length; j++)
                {
                    FPriceUK[i, j] = fPrice[j, i] / 10000.0;
                }
            }
        }

        public double BaseZeroRate { get; }

        public Calendar Calendar { get; }

        public BusinessDayConvention Convention { get; }

        public EvaluationContext Context { get; }

        public DayCounter DcZCIIS { get; }

        public Period ObservationLag { get; }

        public UKRPI Ii { get; }

        public YieldTermStructure NominalUK { get; }

        public double[] CStrikesUK { get; }

        public double[] FStrikesUK { get; }

        public Period[] CfMaturitiesUK { get; }

        public Matrix CPriceUK { get; }

        public Matrix FPriceUK { get; }
    }
}
