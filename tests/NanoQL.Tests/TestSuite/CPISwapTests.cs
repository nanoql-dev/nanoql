// SPDX-FileCopyrightText: Copyright (C) 2011 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/inflationcpiswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's CPI swap tests, <c>test-suite/inflationcpiswap.cpp</c>.</summary>
[Trait("Oracle", "QuantLib")]
public class CPISwapTests
{
    // Adapted: the fixings added to the indexes (lines 304 and 311) are stored in the context the swap is priced in,
    // whose at-par switch is QuantLib's usingAtParCoupons; QuantLib's QL_REQUIREs are assertions.
    [Fact]
    public void Consistency()
    {
        // Checking CPI swap against inflation term structure...

        // check inflation leg vs calculation directly from inflation TS
        var common = new CommonVars();

        SwapType type = SwapType.Payer;
        double nominal = 1000000.0;
        bool subtractInflationNominal = true;

        // float+spread leg
        double spread = 0.0;
        DayCounter floatDayCount = new Actual365Fixed();
        BusinessDayConvention floatPaymentConvention = BusinessDayConvention.ModifiedFollowing;
        int fixingDays = 0;
        IborIndex floatIndex = new GBPLibor(new Period(6, TimeUnit.Months), common.NominalTS);

        // fixed x inflation leg
        double fixedRate = 0.1; // 1% would be 0.01
        double baseCPI = 206.1; // would be 206.13871 if we were interpolating
        DayCounter fixedDayCount = new Actual365Fixed();
        BusinessDayConvention fixedPaymentConvention = BusinessDayConvention.ModifiedFollowing;
        ZeroInflationIndex fixedIndex = common.Ii;
        Period contractObservationLag = common.ContractObservationLag;
        CPIInterpolationType observationInterpolation = common.ContractObservationInterpolation;

        // set the schedules
        var startDate = new Date(2, October, 2007);
        var endDate = new Date(2, October, 2052);
        Schedule floatSchedule = new MakeSchedule().From(startDate).To(endDate)
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithCalendar(new UnitedKingdom())
            .WithConvention(floatPaymentConvention)
            .Backwards()
            .ToSchedule();
        Schedule fixedSchedule = new MakeSchedule().From(startDate).To(endDate)
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithCalendar(new UnitedKingdom())
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards()
            .ToSchedule();

        var zisV = new CPISwap(
            type,
            nominal,
            subtractInflationNominal,
            spread,
            floatDayCount,
            floatSchedule,
            floatPaymentConvention,
            fixingDays,
            floatIndex,
            fixedRate,
            baseCPI,
            fixedDayCount,
            fixedSchedule,
            fixedPaymentConvention,
            contractObservationLag,
            fixedIndex,
            common.Context,
            observationInterpolation);
        Date asofDate = common.Context.EvaluationDate;
        double[] floatFix = [0.06255, 0.05975, 0.0637, 0.018425, 0.0073438, -1, -1];
        double[] cpiFix = [211.4, 217.2, 211.4, 213.4, -2, -2];
        IndexFixings fixings = common.Context.Fixings;
        for (int i = 0; i < floatSchedule.Size; i++)
        {
            if (floatSchedule[i] < common.EvaluationDate)
            {
                fixings = fixings.AddFixing(floatIndex, floatSchedule[i], floatFix[i], true); // true=overwrite
            }

            if (zisV.CpiLeg[i] is CPICoupon zic)
            {
                if (zic.FixingDate() < (common.EvaluationDate - new Period(1, TimeUnit.Months)))
                {
                    fixings = fixings.AddFixing(fixedIndex, zic.FixingDate(), cpiFix[i], true);
                }
            }
        }

        EvaluationContext context = common.Context with { Fixings = fixings };

        // simple structure so simple pricing engine - most work done by index
        CPISwapResults results = DiscountingSwapEngine.Calculate(zisV, context, common.NominalTS);
        bool usingAtParCoupons = context.UseAtParCoupons;

        // get float+spread & fixed*inflation leg prices separately
        double testInfLegNPV = 0.0;
        for (int i = 0; i < zisV.Leg(0).Length; i++)
        {
            Date zicPayDate = zisV.Leg(0)[i].Date;
            if (zicPayDate > asofDate)
            {
                testInfLegNPV += zisV.Leg(0)[i].Amount(context) * common.NominalTS.Discount(zicPayDate);
            }

            if (zisV.CpiLeg[i] is CPICoupon zicV)
            {
                double couponDiff = Math.Abs(zicV.Rate(context) - (fixedRate * (zicV.IndexFixing(context) / baseCPI)));
                Assert.True(
                    couponDiff < 1e-8,
                    $"failed {i}th coupon reconstruction as {fixedRate * (zicV.IndexFixing(context) / baseCPI)} vs rate = "
                    + $"{zicV.Rate(context)}, with difference: {couponDiff}");
            }
        }

        double error = Math.Abs(testInfLegNPV - results.LegNPV[0]!.Value);
        Assert.True(
            error < 1e-5,
            $"failed manual inf leg NPV calc vs pricing engine: {testInfLegNPV} vs {results.LegNPV[0]}");

        double diff = Math.Abs(1 - (results.NPV!.Value / 4191797.54));

        double maxDiff = usingAtParCoupons ? 1e-5 : 3e-5;

        Assert.True(diff < maxDiff, $"failed stored consistency value test, ratio = {diff}");
    }

    // Adapted: the swaps are priced by calling the engine; QuantLib's QL_REQUIREs are assertions.
    [Fact]
    public void Zciisconsistency()
    {
        // Checking CPI swap against zero-coupon inflation swap...

        var common = new CommonVars();

        SwapType ztype = SwapType.Payer;
        double nominal = 1000000.0;
        Date startDate = common.EvaluationDate;
        var endDate = new Date(25, November, 2059);
        Calendar cal = new UnitedKingdom();
        BusinessDayConvention paymentConvention = BusinessDayConvention.ModifiedFollowing;
        DayCounter? dummyDC = null;
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        var observationLag = new Period(2, TimeUnit.Months);
        CPIInterpolationType interpolation = CPIInterpolationType.Flat;

        double quote = 0.03714;
        var zciis = new ZeroCouponInflationSwap(
            ztype, nominal, startDate, endDate, cal, paymentConvention, dc, quote, common.Ii, observationLag, interpolation);

        // simple structure so simple pricing engine - most work done by index
        SwapResults zciisResults = DiscountingSwapEngine.Calculate(zciis, common.Context, common.NominalTS);
        Assert.True(Math.Abs(zciisResults.NPV!.Value) < 1e-3, "zciis does not reprice to zero");

        Date[] oneDate = [endDate];
        var schOneDate = new Schedule(oneDate, cal, paymentConvention);

        SwapType stype = SwapType.Payer;
        double inflationNominal = nominal;
        double floatNominal = inflationNominal * Math.Pow(1.0 + quote, 50);
        bool subtractInflationNominal = true;
        double dummySpread = 0.0, dummyFixedRate = 0.0;
        int fixingDays = 0;
        double baseCPI = CPI.LaggedFixing(common.Ii, startDate, observationLag, interpolation, common.Context);

        IborIndex? dummyFloatIndex = null;

        var cS = new CPISwap(
            stype,
            floatNominal,
            subtractInflationNominal,
            dummySpread,
            dummyDC,
            schOneDate,
            paymentConvention,
            fixingDays,
            dummyFloatIndex,
            dummyFixedRate,
            baseCPI,
            dummyDC,
            schOneDate,
            paymentConvention,
            observationLag,
            common.Ii,
            common.Context,
            interpolation,
            inflationNominal);

        CPISwapResults cSResults = DiscountingSwapEngine.Calculate(cS, common.Context, common.NominalTS);
        Assert.True(Math.Abs(cSResults.NPV!.Value) < 1e-3, "CPISwap as ZCIIS does not reprice to zero");

        for (int i = 0; i < 2; i++)
        {
            Assert.True(
                Math.Abs(cSResults.LegNPV[i]!.Value - zciisResults.LegNPV[i]!.Value) < 1e-3,
                "zciis leg does not equal CPISwap leg");
        }
    }

    // Adapted: as Consistency; the bond is priced by calling the engine.
    [Fact]
    public void Cpibondconsistency()
    {
        // Checking CPI swap against CPI bond...

        var common = new CommonVars();

        SwapType type = SwapType.Payer;
        double nominal = 1000000.0;
        bool subtractInflationNominal = false;

        // float+spread leg
        double spread = 0.0;
        DayCounter floatDayCount = new Actual365Fixed();
        BusinessDayConvention floatPaymentConvention = BusinessDayConvention.ModifiedFollowing;
        int fixingDays = 0;
        IborIndex floatIndex = new GBPLibor(new Period(6, TimeUnit.Months), common.NominalTS);

        // fixed x inflation leg
        double fixedRate = 0.1; // 1% would be 0.01
        double baseCPI = 206.1; // would be 206.13871 if we were interpolating
        DayCounter fixedDayCount = new Actual365Fixed();
        BusinessDayConvention fixedPaymentConvention = BusinessDayConvention.ModifiedFollowing;
        ZeroInflationIndex fixedIndex = common.Ii;
        Period contractObservationLag = common.ContractObservationLag;
        CPIInterpolationType observationInterpolation = common.ContractObservationInterpolation;

        // set the schedules
        var startDate = new Date(2, October, 2007);
        var endDate = new Date(2, October, 2052);
        Schedule floatSchedule = new MakeSchedule().From(startDate).To(endDate)
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithCalendar(new UnitedKingdom())
            .WithConvention(floatPaymentConvention)
            .Backwards()
            .ToSchedule();
        Schedule fixedSchedule = new MakeSchedule().From(startDate).To(endDate)
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithCalendar(new UnitedKingdom())
            .WithConvention(BusinessDayConvention.Unadjusted)
            .Backwards()
            .ToSchedule();

        var zisV = new CPISwap(
            type,
            nominal,
            subtractInflationNominal,
            spread,
            floatDayCount,
            floatSchedule,
            floatPaymentConvention,
            fixingDays,
            floatIndex,
            fixedRate,
            baseCPI,
            fixedDayCount,
            fixedSchedule,
            fixedPaymentConvention,
            contractObservationLag,
            fixedIndex,
            common.Context,
            observationInterpolation);
        double[] floatFix = [0.06255, 0.05975, 0.0637, 0.018425, 0.0073438, -1, -1];
        double[] cpiFix = [211.4, 217.2, 211.4, 213.4, -2, -2];
        IndexFixings fixings = common.Context.Fixings;
        for (int i = 0; i < floatSchedule.Size; i++)
        {
            if (floatSchedule[i] < common.EvaluationDate)
            {
                fixings = fixings.AddFixing(floatIndex, floatSchedule[i], floatFix[i], true); // true=overwrite
            }

            if (zisV.CpiLeg[i] is CPICoupon zic)
            {
                if (zic.FixingDate() < (common.EvaluationDate - new Period(1, TimeUnit.Months)))
                {
                    fixings = fixings.AddFixing(fixedIndex, zic.FixingDate(), cpiFix[i], true);
                }
            }
        }

        EvaluationContext context = common.Context with { Fixings = fixings };

        // simple structure so simple pricing engine - most work done by index
        CPISwapResults results = DiscountingSwapEngine.Calculate(zisV, context, common.NominalTS);

        // now do the bond equivalent
        double[] fixedRates = [fixedRate];
        int settlementDays = 1; // cannot be zero!
        var cpiB = new CPIBond(
            settlementDays,
            nominal,
            baseCPI,
            contractObservationLag,
            fixedIndex,
            observationInterpolation,
            fixedSchedule,
            fixedRates,
            fixedDayCount,
            fixedPaymentConvention);

        double bondNpv = DiscountingBondEngine.Calculate(cpiB, context, common.NominalTS).NPV!.Value;

        Assert.True(
            Math.Abs(bondNpv - results.LegNPV[0]!.Value) < 1e-5,
            "cpi bond does not equal equivalent cpi swap leg");
    }

    // Adapted: QuantLib's fairRate() and fairSpread() refuse "result not available" where the results hold none, a
    // null here.
    [Fact]
    public void TestZeroBpsFairRateAndSpread()
    {
        // Testing CPI swap fair rate/spread calculation with zero BPS...

        var common = new CommonVars();

        SwapType type = SwapType.Payer;
        double nominal = 0.0;
        Date startDate = common.EvaluationDate;
        var endDate = new Date(25, November, 2059);
        Calendar cal = new UnitedKingdom();
        BusinessDayConvention paymentConvention = BusinessDayConvention.ModifiedFollowing;
        DayCounter? dummyDC = null;
        var observationLag = new Period(2, TimeUnit.Months);
        CPIInterpolationType interpolation = CPIInterpolationType.Flat;

        Date[] oneDate = [endDate];
        var schOneDate = new Schedule(oneDate, cal, paymentConvention);

        double baseCPI = CPI.LaggedFixing(common.Ii, startDate, observationLag, interpolation, common.Context);
        IborIndex? dummyFloatIndex = null;

        var swap = new CPISwap(
            type,
            nominal,
            true,
            0.0,
            dummyDC,
            schOneDate,
            paymentConvention,
            0,
            dummyFloatIndex,
            0.1,
            baseCPI,
            dummyDC,
            schOneDate,
            paymentConvention,
            observationLag,
            common.Ii,
            common.Context,
            interpolation);

        CPISwapResults results = DiscountingSwapEngine.Calculate(swap, common.Context, common.NominalTS);

        Assert.False(swap.IsExpired(common.Context));
        Assert.Equal(0.0, results.LegBPS[0]);
        Assert.Equal(0.0, results.LegBPS[1]);

        // QuantLib's "result not available"
        Assert.Null(results.FairRate);
        Assert.Null(results.FairSpread);
    }

    // Adapted: as the zero-BPS case; the expired swap is priced in a context a year after its maturity.
    [Fact]
    public void TestExpiredSwapFairRateAndSpread()
    {
        // Testing CPI swap fair rate/spread for expired swap...

        var common = new CommonVars();

        SwapType type = SwapType.Payer;
        double nominal = 1000000.0;
        Date startDate = common.EvaluationDate;
        var endDate = new Date(25, November, 2059);
        Calendar cal = new UnitedKingdom();
        BusinessDayConvention paymentConvention = BusinessDayConvention.ModifiedFollowing;
        DayCounter? dummyDC = null;
        var observationLag = new Period(2, TimeUnit.Months);
        CPIInterpolationType interpolation = CPIInterpolationType.Flat;

        Date[] oneDate = [endDate];
        var schOneDate = new Schedule(oneDate, cal, paymentConvention);

        double baseCPI = CPI.LaggedFixing(common.Ii, startDate, observationLag, interpolation, common.Context);
        IborIndex? dummyFloatIndex = null;

        var swap = new CPISwap(
            type,
            nominal,
            true,
            0.0,
            dummyDC,
            schOneDate,
            paymentConvention,
            0,
            dummyFloatIndex,
            0.1,
            baseCPI,
            dummyDC,
            schOneDate,
            paymentConvention,
            observationLag,
            common.Ii,
            common.Context,
            interpolation);

        EvaluationContext later = common.Context with { EvaluationDate = endDate + new Period(1, TimeUnit.Years) };

        Assert.True(swap.IsExpired(later));
        CPISwapResults results = DiscountingSwapEngine.Calculate(swap, later, common.NominalTS);

        // QuantLib's "result not available"
        Assert.Null(results.FairRate);
        Assert.Null(results.FairSpread);
    }

    // QuantLib's makeHelpers (lines 56-75), on its zero-coupon helper.
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

    // QuantLib's Datum (lines 51-54).
    private readonly record struct Datum(Date Date, double Rate);

    // QuantLib's CommonVars (lines 78-241), the fixings in its evaluation context.
    private sealed class CommonVars
    {
        public CommonVars()
        {
            ContractObservationLag = new Period(3, TimeUnit.Months);
            ContractObservationInterpolation = CPIInterpolationType.Flat;

            // usual setup
            Calendar = new UnitedKingdom();
            Convention = BusinessDayConvention.ModifiedFollowing;
            var today = new Date(25, November, 2009);
            EvaluationDate = Calendar.Adjust(today);
            Context = new EvaluationContext { EvaluationDate = EvaluationDate };
            DayCounter dcZCIIS = new ActualActual(ActualActualConvention.ISDA);
            DayCounter dcNominal = new ActualActual(ActualActualConvention.ISDA);

            // UK RPI index fixing data
            Schedule rpiSchedule = new MakeSchedule()
                .From(new Date(1, July, 2007))
                .To(new Date(1, September, 2009))
                .WithFrequency(Frequency.Monthly)
                .ToSchedule();
            double[] fixData =
            [
                206.1, 207.3, 208.0, 208.9, 209.7, 210.9,
                209.8, 211.4, 212.1, 214.0, 215.1, 216.8,
                216.5, 217.2, 218.4, 217.7, 216.0, 212.9,
                210.1, 211.4, 211.3, 211.5, 212.8, 213.4,
                213.4, 213.4, 214.4,
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
                new(new Date(26, November, 2009), 0.475),
                new(new Date(2, December, 2009), 0.47498),
                new(new Date(29, December, 2009), 0.49988),
                new(new Date(25, February, 2010), 0.59955),
                new(new Date(18, March, 2010), 0.65361),
                new(new Date(25, May, 2010), 0.82830),

                // { Date(17, June, 2010), 0.7 },  // can't bootstrap with this data point
                new(new Date(16, September, 2010), 0.78960),
                new(new Date(16, December, 2010), 0.93762),
                new(new Date(17, March, 2011), 1.12037),
                new(new Date(16, June, 2011), 1.31308),
                new(new Date(22, September, 2011), 1.52011),
                new(new Date(25, November, 2011), 1.78399),
                new(new Date(26, November, 2012), 2.41170),
                new(new Date(25, November, 2013), 2.83935),
                new(new Date(25, November, 2014), 3.12888),
                new(new Date(25, November, 2015), 3.34298),
                new(new Date(25, November, 2016), 3.50632),
                new(new Date(27, November, 2017), 3.63666),
                new(new Date(26, November, 2018), 3.74723),
                new(new Date(25, November, 2019), 3.83988),
                new(new Date(25, November, 2021), 4.00508),
                new(new Date(25, November, 2024), 4.16042),
                new(new Date(26, November, 2029), 4.15577),
                new(new Date(27, November, 2034), 4.04933),
                new(new Date(25, November, 2039), 3.95217),
                new(new Date(25, November, 2049), 3.80932),
                new(new Date(25, November, 2059), 3.80849),
                new(new Date(25, November, 2069), 3.72677),
                new(new Date(27, November, 2079), 3.63082),
            ];

            var nomD = new List<Date>();
            var nomR = new List<double>();
            foreach (Datum d in nominalData)
            {
                nomD.Add(d.Date);
                nomR.Add(d.Rate / 100.0);
            }

            NominalTS = new InterpolatedZeroCurve<Linear>(nomD, nomR, dcNominal);

            // now build the zero inflation curve
            var observationLag = new Period(2, TimeUnit.Months);

            Datum[] zciisData =
            [
                new(new Date(25, November, 2010), 3.0495),
                new(new Date(25, November, 2011), 2.93),
                new(new Date(26, November, 2012), 2.9795),
                new(new Date(25, November, 2013), 3.029),
                new(new Date(25, November, 2014), 3.1425),
                new(new Date(25, November, 2015), 3.211),
                new(new Date(25, November, 2016), 3.2675),
                new(new Date(25, November, 2017), 3.3625),
                new(new Date(25, November, 2018), 3.405),
                new(new Date(25, November, 2019), 3.48),
                new(new Date(25, November, 2021), 3.576),
                new(new Date(25, November, 2024), 3.649),
                new(new Date(26, November, 2029), 3.751),
                new(new Date(27, November, 2034), 3.77225),
                new(new Date(25, November, 2039), 3.77),
                new(new Date(25, November, 2049), 3.734),
                new(new Date(25, November, 2059), 3.714),
            ];

            // now build the helpers ...
            List<BootstrapHelper<ZeroInflationTermStructure>> helpers =
                MakeHelpers(zciisData, ii, observationLag, Calendar, Convention, dcZCIIS, Context);

            // we can use historical or first ZCIIS for this
            // we know historical is WAY off market-implied, so use market implied flat.
            Date baseDate = ii.LastFixingDate(Context);
            var pCPIts = new PiecewiseZeroInflationCurve<Linear, IterativeBootstrap, ZeroInflationTraits>(
                EvaluationDate, baseDate, ii.Frequency, dcZCIIS, helpers, Context);

            // make sure that the index has the latest zero inflation term structure
            Ii = new UKRPI(pCPIts);
        }

        public Calendar Calendar { get; }

        public BusinessDayConvention Convention { get; }

        public Date EvaluationDate { get; }

        public EvaluationContext Context { get; }

        public UKRPI Ii { get; }

        public Period ContractObservationLag { get; }

        public CPIInterpolationType ContractObservationInterpolation { get; }

        public YieldTermStructure NominalTS { get; }
    }
}
