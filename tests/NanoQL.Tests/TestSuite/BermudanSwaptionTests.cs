// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2021, 2022 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/bermudanswaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testCachedValues' finite-difference checks (lines 171-175, 184-188 and 198-203), which wait for B3.2's FdHullWhiteSwaptionEngine; testCachedG2Values, which waits for B3.1's G2 and B3.2's FdG2SwaptionEngine; testTreeEngineTimeSnapping, which waits for B3.2's FdHullWhiteSwaptionEngine; testBermudanOISSwaptionWithHW and testBermudanOISSwaptionPreservesFeatures, which wait for batch 2's Eonia and B3.2's FdHullWhiteSwaptionEngine; testBermudanOISSwaptionWithG2, which waits for batch 2's Eonia, B3.1's G2 and B3.2's FdG2SwaptionEngine

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
    // context the swaps' Ibor legs and the engines read; the engines are the static DiscountingSwapEngine.Calculate and
    // TreeSwaptionEngine.Calculate. Without the finite-difference engine and its checks, lines 150-151, 154, 157, 160,
    // 171-175, 184-188 and 198-203, which are on the partial line.
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

        Real itmValue, atmValue, otmValue;
        if (!usingAtParCoupons)
        {
            (itmValue, atmValue, otmValue) = (42.2402, 12.9032, 2.49758);
        }
        else
        {
            (itmValue, atmValue, otmValue) = (42.2460, 12.9069, 2.4985);
        }

        Real tolerance = 1.0e-4;

        var swaption = new Swaption(itmSwap, exercise);
        Real npv = TreeEngine(swaption).NPV!.Value;
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

        // The swap's fair rate on the discounting engine QuantLib's makeSwap sets on it.
        public Rate FairRate(VanillaSwap swap) =>
            DiscountingSwapEngine.Calculate(swap, Context, TermStructure).FairRate!.Value;
    }
}
