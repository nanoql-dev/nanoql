// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2021, 2022 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/bermudanswaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BermudanSwaptionTests
{
    // Adapted: CommonVars' relinkable handle, linked to the curve (lines 127-129) after the index is built on it (line
    // 76), is the curve itself, built first, with the index built on it, and its today and settlement dates (lines
    // 80-81, which read today's date) are the case's (lines 121 and 125); the evaluation date it sets (line 123) is the
    // context the swaps' Ibor legs and the engines read; the engines are the static DiscountingSwapEngine.Calculate,
    // TreeSwaptionEngine.Calculate and FdHullWhiteSwaptionEngine.Calculate.
    [Fact]
    public void TestCachedValues()
    {
        // Testing Bermudan swaption with HW model against cached values...

        var vars = new CommonVars(new Date(15, February, 2002), new Date(19, February, 2002));
        bool usingAtParCoupons = vars.Context.UseAtParCoupons;

        Rate atmRate = vars.FairRate(vars.MakeSwap(0.0));

        VanillaSwap itmSwap = vars.MakeSwap(0.8 * atmRate);
        VanillaSwap atmSwap = vars.MakeSwap(atmRate);
        VanillaSwap otmSwap = vars.MakeSwap(1.2 * atmRate);

        Real a = 0.048696, sigma = 0.0058904;
        var model = new HullWhite(vars.TermStructure, a, sigma);
        var exerciseDates = new List<Date>();
        Leg leg = atmSwap.FixedLeg;
        foreach (CashFlow i in leg)
        {
            var coupon = (Coupon)i;
            exerciseDates.Add(coupon.AccrualStartDate);
        }

        Exercise exercise = new BermudanExercise(exerciseDates);

        InstrumentResults TreeEngine(Swaption s) => TreeSwaptionEngine.Calculate(s, vars.Context, model, 50);
        InstrumentResults FdmEngine(Swaption s) => FdHullWhiteSwaptionEngine.Calculate(s, vars.Context, model);

        Real itmValue, atmValue, otmValue;
        Real itmValueFdm, atmValueFdm, otmValueFdm;
        if (!usingAtParCoupons)
        {
            (itmValue, atmValue, otmValue) = (42.2402, 12.9032, 2.49758);
            (itmValueFdm, atmValueFdm, otmValueFdm) = (42.2111, 12.8879, 2.44443);
        }
        else
        {
            (itmValue, atmValue, otmValue) = (42.2460, 12.9069, 2.4985);
            (itmValueFdm, atmValueFdm, otmValueFdm) = (42.2091, 12.8864, 2.4437);
        }

        Real tolerance = 1.0e-4;

        var swaption = new Swaption(itmSwap, exercise);
        Real npv = TreeEngine(swaption).NPV!.Value;
        Assert.False(
            Math.Abs(npv - itmValue) > tolerance,
            $"failed to reproduce cached in-the-money swaption value:\ncalculated: {npv}\nexpected:   {itmValue}");
        npv = FdmEngine(swaption).NPV!.Value;
        Assert.False(
            Math.Abs(npv - itmValueFdm) > tolerance,
            $"failed to reproduce cached in-the-money swaption value:\ncalculated: {npv}\nexpected:   {itmValueFdm}");

        swaption = new Swaption(atmSwap, exercise);
        npv = TreeEngine(swaption).NPV!.Value;
        Assert.False(
            Math.Abs(npv - atmValue) > tolerance,
            $"failed to reproduce cached at-the-money swaption value:\ncalculated: {npv}\nexpected:   {atmValue}");

        npv = FdmEngine(swaption).NPV!.Value;
        Assert.False(
            Math.Abs(npv - atmValueFdm) > tolerance,
            $"failed to reproduce cached at-the-money swaption value:\ncalculated: {npv}\nexpected:   {atmValueFdm}");

        swaption = new Swaption(otmSwap, exercise);
        npv = TreeEngine(swaption).NPV!.Value;
        Assert.False(
            Math.Abs(npv - otmValue) > tolerance,
            $"failed to reproduce cached out-of-the-money swaption value:\ncalculated: {npv}\nexpected:   {otmValue}");

        npv = FdmEngine(swaption).NPV!.Value;
        Assert.False(
            Math.Abs(npv - otmValueFdm) > tolerance,
            $"failed to reproduce cached out-of-the-money swaption value:\ncalculated: {npv}\nexpected:   {otmValueFdm}");

        for (int k = 0; k < exerciseDates.Count; k++)
        {
            exerciseDates[k] = vars.Calendar.Adjust(exerciseDates[k] - 10);
        }

        exercise = new BermudanExercise(exerciseDates);

        if (!usingAtParCoupons)
        {
            (itmValue, atmValue, otmValue) = (42.1791, 12.7699, 2.4368);
        }
        else
        {
            (itmValue, atmValue, otmValue) = (42.1849, 12.7736, 2.4379);
        }

        swaption = new Swaption(itmSwap, exercise);
        npv = TreeEngine(swaption).NPV!.Value;
        Assert.False(
            Math.Abs(npv - itmValue) > tolerance,
            $"failed to reproduce cached in-the-money swaption value:\ncalculated: {npv}\nexpected:   {itmValue}");
        swaption = new Swaption(atmSwap, exercise);
        npv = TreeEngine(swaption).NPV!.Value;
        Assert.False(
            Math.Abs(npv - atmValue) > tolerance,
            $"failed to reproduce cached at-the-money swaption value:\ncalculated: {npv}\nexpected:   {atmValue}");
        swaption = new Swaption(otmSwap, exercise);
        npv = TreeEngine(swaption).NPV!.Value;
        Assert.False(
            Math.Abs(npv - otmValue) > tolerance,
            $"failed to reproduce cached out-of-the-money swaption value:\ncalculated: {npv}\nexpected:   {otmValue}");
    }

    // Adapted: as testCachedValues; the engines are the static FdG2SwaptionEngine.Calculate and
    // TreeSwaptionEngine.Calculate.
    [Fact]
    public void TestCachedG2Values()
    {
        // Testing Bermudan swaption with G2 model against cached values...

        var vars = new CommonVars(new Date(15, September, 2016), new Date(19, September, 2016));
        bool usingAtParCoupons = vars.Context.UseAtParCoupons;

        Rate atmRate = vars.FairRate(vars.MakeSwap(0.0));
        var swaptions = new List<Swaption>();
        for (Real s = 0.5; s < 1.51; s += 0.25)
        {
            VanillaSwap swap = vars.MakeSwap(s * atmRate);

            var exerciseDates = new List<Date>();
            foreach (CashFlow i in swap.FixedLeg)
            {
                exerciseDates.Add(((Coupon)i).AccrualStartDate);
            }

            swaptions.Add(new Swaption(swap, new BermudanExercise(exerciseDates)));
        }

        Real a = 0.1, sigma = 0.01, b = 0.2, eta = 0.013, rho = -0.5;

        var g2Model = new G2(vars.TermStructure, a, sigma, b, eta, rho);
        InstrumentResults FdmEngine(Swaption s) => FdG2SwaptionEngine.Calculate(s, vars.Context, g2Model, 50, 75, 75, 0, 1e-3);
        InstrumentResults TreeEngine(Swaption s) => TreeSwaptionEngine.Calculate(s, vars.Context, g2Model, 50);

        Real[] expectedFdm, expectedTree;
        if (!usingAtParCoupons)
        {
            expectedFdm = [103.231, 54.6519, 20.0475, 5.26941, 1.07097];
            expectedTree = [103.245, 54.6685, 20.1656, 5.43999, 1.12702];
        }
        else
        {
            expectedFdm = [103.227, 54.6502, 20.0469, 5.26924, 1.07093];
            expectedTree = [103.248, 54.6726, 20.1685, 5.44118, 1.12737];
        }

        Real tol = 0.005;
        for (int i = 0; i < swaptions.Count; ++i)
        {
            Real calculatedFdm = FdmEngine(swaptions[i]).NPV!.Value;

            Assert.False(
                Math.Abs(calculatedFdm - expectedFdm[i]) > tol,
                $"failed to reproduce cached G2 FDM swaption value:\ncalculated: {calculatedFdm}\nexpected:   {expectedFdm[i]}");

            Real calculatedTree = TreeEngine(swaptions[i]).NPV!.Value;

            Assert.False(
                Math.Abs(calculatedTree - expectedTree[i]) > tol,
                $"failed to reproduce cached G2 Tree swaption value:\ncalculated: {calculatedTree}\nexpected:   {expectedTree[i]}");
        }
    }

    // Adapted: the relinkable handle is the curve itself; the engines are the static FdHullWhiteSwaptionEngine.Calculate
    // and TreeSwaptionEngine.Calculate, on a model built once, as each loop builds an equal one.
    [Fact]
    public void TestTreeEngineTimeSnapping()
    {
        // Testing snap of exercise dates for discretized swaption...

        var today = new Date(8, July, 2021);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure termStructure = new FlatForward(today, 0.02, new Actual365Fixed());
        var index = new Euribor3M(termStructure);

        Swaption MakeBermudanSwaption(Date callDate)
        {
            var effectiveDate = new Date(15, May, 2025);
            VanillaSwap swap = new MakeVanillaSwap(new Period(10, TimeUnit.Years), index)
                .WithFixedRate(0.05)
                .WithEffectiveDate(effectiveDate)
                .WithNominal(10000.00)
                .WithType(SwapType.Payer)
                .ToVanillaSwap(context);

            List<Date> exerciseDates = [effectiveDate, callDate];
            var bermudanExercise = new BermudanExercise(exerciseDates);
            return new Swaption(swap, bermudanExercise);
        }

        int intervalOfDaysToTest = 10;
        var initialCallDate = new Date(15, May, 2030);
        Calendar calendar = index.FixingCalendar;

        for (int i = -intervalOfDaysToTest; i < intervalOfDaysToTest + 1; i++)
        {
            Date callDate = initialCallDate + i;
            if (calendar.IsBusinessDay(callDate))
            {
                Swaption bermudanSwaption = MakeBermudanSwaption(callDate);

                var model = new HullWhite(termStructure);

                Real npvFD = FdHullWhiteSwaptionEngine.Calculate(bermudanSwaption, context, model).NPV!.Value;

                const int timesteps = 14 * 4 * 4;

                Real npvTree = TreeSwaptionEngine.Calculate(bermudanSwaption, context, model, timesteps).NPV!.Value;

                Real npvDiff = npvTree - npvFD;

                Real tolerance = 1.0;
                Assert.False(
                    Math.Abs(npvTree - npvFD) > tolerance,
                    $"At {callDate}: The difference between the npv of the FD and the tree engine is expected to be smaller "
                    + $"than {tolerance} but was {npvDiff:F2}. (FD: {npvFD:F2}, tree: {npvTree:F2})");
            }
        }
    }

    // Adapted: as testCachedValues; the swaps' engines are the static DiscountingSwapEngine.Calculate, which nothing here
    // reads, and the swaptions' the static FdHullWhiteSwaptionEngine.Calculate.
    [Fact]
    public void TestBermudanOISSwaptionWithHW()
    {
        // Testing Bermudan swaption with OIS underlying and HW model...

        var vars = new CommonVars(new Date(15, February, 2002), new Date(19, February, 2002));

        // Build OIS with same economics as the VanillaSwap test
        var overnightIndex = new Eonia(vars.TermStructure);
        (Schedule fixedSchedule, Schedule overnightSchedule) = vars.OisSchedules();

        // Under a flat single curve, the VanillaSwap ATM rate equals the OIS ATM rate
        Rate atmRate = vars.FairRate(vars.MakeSwap(0.0));

        OvernightIndexedSwap MakeOIS(Rate fixedRate) => new(
            vars.Type, vars.Nominal, fixedSchedule, fixedRate, vars.FixedDayCount, overnightSchedule, overnightIndex, 0.0);

        OvernightIndexedSwap itmOIS = MakeOIS(0.8 * atmRate);
        OvernightIndexedSwap atmOIS = MakeOIS(atmRate);
        OvernightIndexedSwap otmOIS = MakeOIS(1.2 * atmRate);

        // Build Bermudan exercise from fixed leg
        var exerciseDates = new List<Date>();
        foreach (CashFlow cf in atmOIS.FixedLeg)
        {
            exerciseDates.Add(((Coupon)cf).AccrualStartDate);
        }

        var exercise = new BermudanExercise(exerciseDates);

        Real a = 0.048696, sigma = 0.0058904;
        var model = new HullWhite(vars.TermStructure, a, sigma);
        Real FdmEngine(Swaption s) => FdHullWhiteSwaptionEngine.Calculate(s, vars.Context, model).NPV!.Value;

        // Price OIS Bermudan swaptions
        Real itmValue = FdmEngine(new Swaption(itmOIS, exercise));
        Real atmValue = FdmEngine(new Swaption(atmOIS, exercise));
        Real otmValue = FdmEngine(new Swaption(otmOIS, exercise));

        // OIS Bermudan should produce positive values
        Assert.False(itmValue <= 0.0, $"ITM OIS Bermudan swaption has non-positive value: {itmValue}");
        Assert.False(atmValue <= 0.0, $"ATM OIS Bermudan swaption has non-positive value: {atmValue}");
        Assert.False(otmValue <= 0.0, $"OTM OIS Bermudan swaption has non-positive value: {otmValue}");

        // ITM > ATM > OTM monotonicity
        Assert.False(itmValue <= atmValue, $"ITM OIS Bermudan ({itmValue}) should exceed ATM ({atmValue})");
        Assert.False(atmValue <= otmValue, $"ATM OIS Bermudan ({atmValue}) should exceed OTM ({otmValue})");

        // Compare with VanillaSwap Bermudan - under HW, difference should be small
        Real itmVS = FdmEngine(new Swaption(vars.MakeSwap(0.8 * atmRate), exercise));
        Real atmVS = FdmEngine(new Swaption(vars.MakeSwap(atmRate), exercise));
        Real otmVS = FdmEngine(new Swaption(vars.MakeSwap(1.2 * atmRate), exercise));

        // Under single-factor HW with flat curve, VanillaSwap and OIS
        // Bermudans should be close (floating leg ≈ par in both cases).
        // Allow up to 5% relative difference.
        Real relTol = 0.05;

        static Real RelDiff(Real a, Real b) => Math.Abs(a - b) / Math.Max(Math.Abs(b), 1e-10);
        Real itmDiff = RelDiff(itmValue, itmVS);
        Real atmDiff = RelDiff(atmValue, atmVS);
        Real otmDiff = RelDiff(otmValue, otmVS);

        Assert.False(itmDiff > relTol, $"ITM OIS vs VanillaSwap Bermudan difference too large: {itmValue:E16} vs {itmVS:E16} (rel diff {itmDiff:E16})");
        Assert.False(atmDiff > relTol, $"ATM OIS vs VanillaSwap Bermudan difference too large: {atmValue:E16} vs {atmVS:E16} (rel diff {atmDiff:E16})");
        Assert.False(otmDiff > relTol, $"OTM OIS vs VanillaSwap Bermudan difference too large: {otmValue:E16} vs {otmVS:E16} (rel diff {otmDiff:E16})");
    }

    // Adapted: as testBermudanOISSwaptionWithHW, on the static FdG2SwaptionEngine.Calculate.
    [Fact]
    public void TestBermudanOISSwaptionWithG2()
    {
        // Testing Bermudan swaption with OIS underlying and G2 model...

        var vars = new CommonVars(new Date(15, February, 2002), new Date(19, February, 2002));

        var overnightIndex = new Eonia(vars.TermStructure);
        (Schedule fixedSchedule, Schedule overnightSchedule) = vars.OisSchedules();

        // Under a flat single curve, the VanillaSwap ATM rate equals the OIS ATM rate
        Rate atmRate = vars.FairRate(vars.MakeSwap(0.0));

        var atmOIS = new OvernightIndexedSwap(
            vars.Type, vars.Nominal, fixedSchedule, atmRate, vars.FixedDayCount, overnightSchedule, overnightIndex, 0.0);

        var exerciseDates = new List<Date>();
        foreach (CashFlow cf in atmOIS.FixedLeg)
        {
            exerciseDates.Add(((Coupon)cf).AccrualStartDate);
        }

        var exercise = new BermudanExercise(exerciseDates);

        var model = new G2(vars.TermStructure);
        Real FdmEngine(Swaption s) => FdG2SwaptionEngine.Calculate(s, vars.Context, model).NPV!.Value;

        Real oisValue = FdmEngine(new Swaption(atmOIS, exercise));

        Assert.False(oisValue <= 0.0, $"ATM OIS Bermudan swaption (G2) has non-positive value: {oisValue}");

        // Compare with VanillaSwap Bermudan
        Real vsValue = FdmEngine(new Swaption(vars.MakeSwap(atmRate), exercise));

        Real relDiff = Math.Abs(oisValue - vsValue) / Math.Max(Math.Abs(vsValue), 1e-10);
        Real relTol = 0.05;
        Assert.False(relDiff > relTol, $"ATM OIS vs VanillaSwap Bermudan (G2) difference too large: {oisValue:E16} vs {vsValue:E16} (rel diff {relDiff:E16})");
    }

    // Adapted: as testBermudanOISSwaptionWithHW; QuantLib's Null<Natural>() look-back days are null.
    [Fact]
    public void TestBermudanOISSwaptionPreservesFeatures()
    {
        // Testing that Bermudan OIS swaption preserves averaging and lockout...

        var vars = new CommonVars(new Date(15, February, 2002), new Date(19, February, 2002));

        var overnightIndex = new Eonia(vars.TermStructure);
        (Schedule fixedSchedule, Schedule overnightSchedule) = vars.OisSchedules();

        // Under a flat single curve, the VanillaSwap ATM rate equals the OIS ATM rate
        Rate atmRate = vars.FairRate(vars.MakeSwap(0.0));

        OvernightIndexedSwap MakeOIS(RateAveragingType avg, Natural lockout) => new(
            vars.Type,
            vars.Nominal,
            fixedSchedule,
            atmRate,
            vars.FixedDayCount,
            overnightSchedule,
            overnightIndex,
            0.0,
            0, // paymentLag
            vars.FloatingConvention,
            null, // paymentCalendar
            false, // telescopicValueDates
            avg,
            null, // lookbackDays
            lockout);

        // Build exercise from fixed leg
        OvernightIndexedSwap refOIS = MakeOIS(RateAveragingType.Compound, 0);
        var exerciseDates = new List<Date>();
        foreach (CashFlow cf in refOIS.FixedLeg)
        {
            exerciseDates.Add(((Coupon)cf).AccrualStartDate);
        }

        var exercise = new BermudanExercise(exerciseDates);

        Real a = 0.048696, sigma = 0.0058904;
        var model = new HullWhite(vars.TermStructure, a, sigma);
        Real FdmEngine(Swaption s) => FdHullWhiteSwaptionEngine.Calculate(s, vars.Context, model).NPV!.Value;

        // Price with compound averaging (default)
        Real compoundValue = FdmEngine(new Swaption(MakeOIS(RateAveragingType.Compound, 0), exercise));

        // Price with simple averaging — should differ meaningfully
        Real simpleValue = FdmEngine(new Swaption(MakeOIS(RateAveragingType.Simple, 0), exercise));

        // Simple vs compound averaging produces ~9-10% difference at 5% rate
        // level (arithmetic vs geometric compounding over semi-annual periods).
        // Use a conservative 0.1% floor to avoid false passes.
        Real avgDiff = Math.Abs(compoundValue - simpleValue) / Math.Max(compoundValue, 1e-10);
        Assert.False(
            avgDiff < 0.001,
            $"Simple vs compound OIS Bermudan should differ, got {avgDiff * 100:E16}% (compound={compoundValue:E16}, simple={simpleValue:E16})");

        // Price with lockout — should differ from plain compound
        Real lockoutValue = FdmEngine(new Swaption(MakeOIS(RateAveragingType.Compound, 5), exercise));

        // Lockout freezes the last N fixings, producing a small but
        // non-zero change. Use relative tolerance rather than exact equality.
        Real lockDiff = Math.Abs(lockoutValue - compoundValue) / Math.Max(compoundValue, 1e-10);
        Assert.False(
            lockDiff < 1e-8,
            $"5-day lockout OIS Bermudan should differ from plain, rel diff = {lockDiff:E16} (lockout={lockoutValue:E16}, plain={compoundValue:E16})");
    }

    // QuantLib's CommonVars, on the curve the case links, with today and the settlement date given.
    private sealed class CommonVars
    {
        public CommonVars(Date today, Date settlement)
        {
            StartYears = 1;
            Length = 5;
            Type = SwapType.Payer;
            Nominal = 1000.0;
            SettlementDays = 2;
            FixedConvention = BusinessDayConvention.Unadjusted;
            FloatingConvention = BusinessDayConvention.ModifiedFollowing;
            FixedFrequency = Frequency.Annual;
            FloatingFrequency = Frequency.Semiannual;
            FixedDayCount = new Thirty360(Thirty360Convention.BondBasis);

            // flat yield term structure impling 1x5 swap at 5%
            TermStructure = new FlatForward(settlement, 0.04875825, new Actual365Fixed());
            Index = new Euribor6M(TermStructure);
            Calendar = Index.FixingCalendar;
            Today = today;
            Settlement = settlement;
            Context = new EvaluationContext { EvaluationDate = today };
        }

        // global data
        public Date Today { get; }

        public Date Settlement { get; }

        public Calendar Calendar { get; }

        public EvaluationContext Context { get; }

        // underlying swap parameters
        public Integer StartYears { get; }

        public Integer Length { get; }

        public SwapType Type { get; }

        public Real Nominal { get; }

        public BusinessDayConvention FixedConvention { get; }

        public BusinessDayConvention FloatingConvention { get; }

        public Frequency FixedFrequency { get; }

        public Frequency FloatingFrequency { get; }

        public DayCounter FixedDayCount { get; }

        public IborIndex Index { get; }

        public Natural SettlementDays { get; }

        public YieldTermStructure TermStructure { get; }

        // utilities
        public VanillaSwap MakeSwap(Rate fixedRate)
        {
            Date start = Calendar.Advance(Settlement, StartYears, TimeUnit.Years);
            Date maturity = Calendar.Advance(start, Length, TimeUnit.Years);
            var fixedSchedule = new Schedule(
                start,
                maturity,
                new Period(FixedFrequency),
                Calendar,
                FixedConvention,
                FixedConvention,
                DateGenerationRule.Forward,
                false,
                Context);
            var floatSchedule = new Schedule(
                start,
                maturity,
                new Period(FloatingFrequency),
                Calendar,
                FloatingConvention,
                FloatingConvention,
                DateGenerationRule.Forward,
                false,
                Context);
            return new VanillaSwap(
                Type,
                Nominal,
                fixedSchedule,
                fixedRate,
                FixedDayCount,
                floatSchedule,
                Index,
                0.0,
                Index.DayCounter,
                Context);
        }

        // The OIS cases' fixed and overnight schedules, from a year after the settlement date over the swap's length.
        public (Schedule Fixed, Schedule Overnight) OisSchedules()
        {
            Date start = Calendar.Advance(Settlement, StartYears, TimeUnit.Years);
            Date maturity = Calendar.Advance(start, Length, TimeUnit.Years);
            var fixedSchedule = new Schedule(
                start,
                maturity,
                new Period(FixedFrequency),
                Calendar,
                FixedConvention,
                FixedConvention,
                DateGenerationRule.Forward,
                false,
                Context);
            var overnightSchedule = new Schedule(
                start,
                maturity,
                new Period(FloatingFrequency),
                Calendar,
                FloatingConvention,
                FloatingConvention,
                DateGenerationRule.Forward,
                false,
                Context);
            return (fixedSchedule, overnightSchedule);
        }

        // The swap's fair rate on the discounting engine QuantLib's makeSwap sets on it.
        public Rate FairRate(VanillaSwap swap) =>
            DiscountingSwapEngine.Calculate(swap, Context, TermStructure).FairRate!.Value;
    }
}
