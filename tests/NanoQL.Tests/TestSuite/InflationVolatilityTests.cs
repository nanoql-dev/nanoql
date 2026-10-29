// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/inflationvolatility.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testConstantVolatilityQuoteIsObserved (Conventions/ConstantInflationVolatilityQuoteTests.cs)

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

/// <summary>
/// QuantLib's year-on-year inflation volatility cases: the price surface's at-the-money swap rates and year-on-year
/// curve, and the volatilities stripped from it. <c>testYoYPriceSurfaceToVol</c> carries QuantLib's master's node checks
/// (#2941) on the stripped curves, read from the surface's stripped set where QuantLib's case subclasses the stripper.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationVolatilityTests
{
    private static readonly Date Eval = new(23, Month.November, 2007);

    [Fact]
    public void TestYoYPriceSurfaceToVol()
    {
        Market m = Setup();
        InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic> priceSurfEU = SetupPriceSurface(m);

        // caplet pricer: the unit displaced engine on the index and the nominal curve, the volatility the trial's
        Func<YoYInflationCapFloor, EvaluationContext, YoYOptionletVolatilitySurface, InstrumentResults> yoyPricerUD =
            (c, ctx, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, ctx, m.YoYIndexEU, v, m.NominalEUR);

        // cap stripper
        var yoyOptionletStripper = new InterpolatedYoYOptionletStripper<Linear>();

        // now set up all the variables for the stripping
        const int SettlementDays = 0;
        var cal = new TARGET();
        const BusinessDayConvention Bdc = BusinessDayConvention.ModifiedFollowing;
        DayCounter dc = new Actual365Fixed();
        YoYCapFloorTermPriceSurface capFloorPrices = priceSurfEU;
        Period lag = priceSurfEU.ObservationLag;

        // when you have bad data, i.e. very low/constant prices for short dated extreem strikes then you cannot assume
        // constant caplet vol (else arbitrage)
        const double Slope = -0.5;

        // Actually is doesn't matter what the interpolation is because we only
        // intend to use the K values that correspond to quotes ... for model fitting.
        var yoySurf = KInterpolatedYoYOptionletVolatilitySurface<Linear>.Create(
            SettlementDays, cal, Bdc, dc, lag, capFloorPrices, yoyPricerUD, yoyOptionletStripper, Slope, m.Context);

        // Bootstrapped curves must expose their calibrated nodes through the shared interface.
        Assert.NotEmpty(yoySurf.Stripped.Curves);
        foreach (YoYOptionletVolatilitySurface curve in yoySurf.Stripped.Curves)
        {
            var interpolated = Assert.IsAssignableFrom<IInterpolatedCurve<YoYInflationVolatilityTraits, Linear>>(curve);
            var nodes = interpolated.Nodes();
            var dates = interpolated.Dates();
            var data = interpolated.Data();
            Assert.True(nodes.Length > 1);
            Assert.Equal(nodes.Length, dates.Length);
            Assert.Equal(nodes.Length, data.Length);
            for (int i = 0; i < nodes.Length; ++i)
            {
                Assert.Equal(dates[i], nodes[i].Key);
                Assert.Equal(data[i], nodes[i].Value);
            }
        }

        // now use it for something ... like stating what the T=const lines look like
        double[] volATyear1 =
        [
            0.0129, 0.0094, 0.0083, 0.0073, 0.0064,
            0.0058, 0.0042, 0.0046, 0.0053, 0.0064,
            0.0098,
        ];
        double[] volATyear3 =
        [
            0.0080, 0.0058, 0.0051, 0.0045, 0.0040,
            0.0035, 0.0026, 0.0028, 0.0033, 0.0040,
            0.0061,
        ];

        Date d = yoySurf.BaseDate() + new Period(1, TimeUnit.Years);
        var someSlice = yoySurf.Dslice(d);
        const double Eps = 0.0001;
        for (int i = 0; i < someSlice.Strikes.Length; i++)
        {
            Assert.True(
                Math.Abs(someSlice.Volatilities[i] - volATyear1[i]) < Eps,
                $" could not recover 1yr vol: {someSlice.Volatilities[i]} vs {volATyear1[i]}");
        }

        d = yoySurf.BaseDate() + new Period(3, TimeUnit.Years);
        var someOtherSlice = yoySurf.Dslice(d);
        for (int i = 0; i < someOtherSlice.Strikes.Length; i++)
        {
            Assert.True(
                Math.Abs(someOtherSlice.Volatilities[i] - volATyear3[i]) < Eps,
                $"could not recover 3yr vol: {someOtherSlice.Volatilities[i]} vs {volATyear3[i]}");
        }
    }

    [Fact]
    public void TestYoYPriceSurfaceToATM()
    {
        Market m = Setup();
        InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic> priceSurfEU = SetupPriceSurface(m);

        var yyATMt = priceSurfEU.AtmYoYSwapTimeRates();
        var yyATMd = priceSurfEU.AtmYoYSwapDateRates();

        double[] crv = [0.024586, 0.0247575, 0.0249396, 0.0252596, 0.0258498, 0.0262883, 0.0267915];
        double[] swaps = [0.024586, 0.0247575, 0.0249396, 0.0252596, 0.0258498, 0.0262883, 0.0267915];
        double[] ayoy = [0.0247659, 0.0251437, 0.0255945, 0.0265015, 0.0280457, 0.0285534, 0.0295884];
        const double Eps = 2e-5;
        for (int i = 0; i < yyATMt.Times.Length; i++)
        {
            Assert.True(
                Math.Abs(yyATMt.Rates[i] - crv[i]) < Eps,
                $"could not recover cached yoy swap curve {yyATMt.Rates[i]} vs {crv[i]}");
        }

        for (int i = 0; i < yyATMd.Dates.Length; i++)
        {
            double rate = priceSurfEU.AtmYoYSwapRate(yyATMd.Dates[i]);
            Assert.True(Math.Abs(rate - swaps[i]) < Eps, $"could not recover yoy swap curve {rate} vs {swaps[i]}");
        }

        for (int i = 0; i < yyATMd.Dates.Length; i++)
        {
            double rate = priceSurfEU.AtmYoYRate(yyATMd.Dates[i]);
            Assert.True(
                Math.Abs(rate - ayoy[i]) < Eps,
                $" could not recover cached yoy curve {rate} vs {ayoy[i]} at {yyATMd.Dates[i]}");
        }
    }

    // QuantLib's setup(): the euro and sterling nominal curves, the euro year-on-year curve and the euro price data; the
    // sterling index and price data, which no case reads, aren't built.
    internal static Market Setup()
    {
        var context = new EvaluationContext { EvaluationDate = Eval };

        // nominal yield curve (interpolated; times assume year parts have 365 days)
        double[] timesEUR =
        [
            0.0109589, 0.0684932, 0.263014, 0.317808, 0.567123, 0.816438,
            1.06575, 1.31507, 1.56438, 2.0137, 3.01918, 4.01644,
            5.01644, 6.01644, 7.01644, 8.01644, 9.02192, 10.0192,
            12.0192, 15.0247, 20.0301, 25.0356, 30.0329, 40.0384,
            50.0466,
        ];
        double[] ratesEUR =
        [
            0.0415600, 0.0426840, 0.0470980, 0.0458506, 0.0449550, 0.0439784,
            0.0431887, 0.0426604, 0.0422925, 0.0424591, 0.0421477, 0.0421853,
            0.0424016, 0.0426969, 0.0430804, 0.0435011, 0.0439368, 0.0443825,
            0.0452589, 0.0463389, 0.0472636, 0.0473401, 0.0470629, 0.0461092,
            0.0450794,
        ];
        var d = new List<Date>();
        foreach (double t in timesEUR)
        {
            int ys = (int)Math.Floor(t);
            int ds = (int)((t - ys) * 365);
            d.Add(Eval + new Period(ys, TimeUnit.Years) + new Period(ds, TimeUnit.Days));
        }

        var nominalEUR = new InterpolatedZeroCurve<Cubic>(d, ratesEUR, new Actual365Fixed());

        // times = years - lag, where the lag is 2 months or 2/12
        // because this data is derived from cap/floor data that
        // is based on a 2 month lag.
        // note that these are NOT swap rates
        // also not that the first value MUST be in the base period
        // i.e. the first rate is for a negative time
        double[] yoyEUrates =
        [
            0.0237951,
            0.0238749, 0.0240334, 0.0241934, 0.0243567, 0.0245323,
            0.0247213, 0.0249348, 0.0251768, 0.0254337, 0.0257258,
            0.0260217, 0.0263006, 0.0265538, 0.0267803, 0.0269378,
            0.0270608, 0.0271363, 0.0272, 0.0272512, 0.0272927,
            0.027317, 0.0273615, 0.0273811, 0.0274063, 0.0274307,
            0.0274625, 0.027527, 0.0275952, 0.0276734, 0.027794,
        ];
        var euhicp = new EUHICP();

        // the base date is based on the last published index fixing
        Date baseDate = InflationTermStructureFunctions.InflationPeriod(
            Eval - new Period(1, TimeUnit.Months), euhicp.Frequency).Start;
        var yd = new List<Date> { baseDate };

        // cap maturities are based on the observation lag
        var target = new TARGET();
        Date capStartDate = target.Advance(Eval, -2, TimeUnit.Months, BusinessDayConvention.ModifiedFollowing);
        for (int i = 1; i < yoyEUrates.Length; i++)
        {
            yd.Add(target.Advance(capStartDate, i, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing));
        }

        var pYTSEU = new InterpolatedYoYInflationCurve<Linear>(
            Eval, yd, yoyEUrates, Frequency.Monthly, new Actual365Fixed());
        var yoyIndexEU = new YoYInflationIndex(euhicp, pYTSEU);

        // price data
        double[] capStrikesEU = [0.02, 0.025, 0.03, 0.035, 0.04, 0.05];
        Period[] capMaturitiesEU =
        [
            new(3, TimeUnit.Years), new(5, TimeUnit.Years), new(7, TimeUnit.Years), new(10, TimeUnit.Years),
            new(15, TimeUnit.Years), new(20, TimeUnit.Years), new(30, TimeUnit.Years),
        ];
        var capPricesEU = new Matrix(new double[,]
        {
            { 116.225, 204.945, 296.285, 434.29, 654.47, 844.775, 1132.33 },
            { 34.305, 71.575, 114.1, 184.33, 307.595, 421.395, 602.35 },
            { 6.37, 19.085, 35.635, 66.42, 127.69, 189.685, 296.195 },
            { 1.325, 5.745, 12.585, 26.945, 58.95, 94.08, 158.985 },
            { 0.501, 2.37, 5.38, 13.065, 31.91, 53.95, 96.97 },
            { 0.501, 0.695, 1.47, 4.415, 12.86, 23.75, 46.7 },
        });
        double[] floorStrikesEU = [-0.01, 0.00, 0.005, 0.01, 0.015, 0.02];
        var floorPricesEU = new Matrix(new double[,]
        {
            { 0.501, 0.851, 2.44, 6.645, 16.23, 26.85, 46.365 },
            { 0.501, 2.236, 5.555, 13.075, 28.46, 44.525, 73.08 },
            { 1.025, 3.935, 9.095, 19.64, 39.93, 60.375, 96.02 },
            { 2.465, 7.885, 16.155, 31.6, 59.34, 86.21, 132.045 },
            { 6.9, 17.92, 32.085, 56.08, 95.95, 132.85, 194.18 },
            { 23.52, 47.625, 74.085, 114.355, 175.72, 229.565, 316.285 },
        });
        return new Market(
            context, nominalEUR, yoyIndexEU, capStrikesEU, floorStrikesEU, capMaturitiesEU, capPricesEU, floorPricesEU);
    }

    // QuantLib's setupPriceSurface(): the euro price surface, bicubic in time and strike, its swap rates cubic.
    internal static InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic> SetupPriceSurface(Market m)
    {
        const int FixingDays = 0;
        const int Lag = 3; // must be 3 because we use an interpolated index (EU)
        var yyLag = new Period(Lag, TimeUnit.Months);
        DayCounter dc = new Actual365Fixed();
        var cal = new TARGET();
        const BusinessDayConvention Bdc = BusinessDayConvention.ModifiedFollowing;
        return InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic>.Create(
            FixingDays, yyLag, m.YoYIndexEU, CPIInterpolationType.Linear, m.NominalEUR, dc, cal, Bdc, m.CStrikesEU,
            m.FStrikesEU, m.CfMaturitiesEU, m.CPriceEU, m.FPriceEU, m.Context);
    }

    internal sealed record Market(
        EvaluationContext Context,
        YieldTermStructure NominalEUR,
        YoYInflationIndex YoYIndexEU,
        double[] CStrikesEU,
        double[] FStrikesEU,
        Period[] CfMaturitiesEU,
        Matrix CPriceEU,
        Matrix FPriceEU);
}
