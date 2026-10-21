// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/swap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testInArrears, which waits for batch 2's SimpleDayCounter; testFixedTenorInferenceWithTerminationDate, which waits for batch 2's Bbsw; testSpotDateUsesFixingCalendar, which waits for batch 2's Taiwan calendar
// Replaced: testNotifications (Vectors/Instruments/VanillaSwapVectorTests.cs)

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class SwapTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void TestFairRate()
    {
        // Testing vanilla-swap calculation of fair fixed rate...

        var vars = new CommonVars(Context);

        Integer[] lengths = [1, 2, 5, 10, 20];
        Spread[] spreads = [-0.001, -0.01, 0.0, 0.01, 0.001];

        foreach (Integer length in lengths)
        {
            foreach (Spread spread in spreads)
            {
                VanillaSwap swap = vars.MakeSwap(length, 0.0, spread);
                swap = vars.MakeSwap(length, vars.Price(swap).FairRate!.Value, spread);
                Real npv = vars.Price(swap).NPV!.Value;
                Assert.True(
                    Math.Abs(npv) <= 1.0e-10,
                    $"recalculating with implied rate: length {length} years, floating spread {spread}, swap value "
                    + $"{npv}");
            }
        }
    }

    [Fact]
    public void TestFairSpread()
    {
        // Testing vanilla-swap calculation of fair floating spread...

        var vars = new CommonVars(Context);

        Integer[] lengths = [1, 2, 5, 10, 20];
        Rate[] rates = [0.04, 0.05, 0.06, 0.07];

        foreach (Integer length in lengths)
        {
            foreach (Rate j in rates)
            {
                VanillaSwap swap = vars.MakeSwap(length, j, 0.0);
                swap = vars.MakeSwap(length, j, vars.Price(swap).FairSpread!.Value);
                Real npv = vars.Price(swap).NPV!.Value;
                Assert.True(
                    Math.Abs(npv) <= 1.0e-10,
                    $"recalculating with implied spread: length {length} years, fixed rate {j}, swap value {npv}");
            }
        }
    }

    [Fact]
    public void TestRateDependency()
    {
        // Testing vanilla-swap dependency on fixed rate...

        var vars = new CommonVars(Context);

        Integer[] lengths = [1, 2, 5, 10, 20];
        Spread[] spreads = [-0.001, -0.01, 0.0, 0.01, 0.001];
        Rate[] rates = [0.03, 0.04, 0.05, 0.06, 0.07];

        foreach (Integer length in lengths)
        {
            foreach (Spread spread in spreads)
            {
                // store the results for different rates...
                var swapValues = new List<Real>();
                foreach (Rate rate in rates)
                {
                    VanillaSwap swap = vars.MakeSwap(length, rate, spread);
                    swapValues.Add(vars.Price(swap).NPV!.Value);
                }

                // and check that they go the right way
                for (int n = 0; n + 1 < swapValues.Count; ++n)
                {
                    Assert.False(
                        swapValues[n] < swapValues[n + 1],
                        $"NPV is increasing with the fixed rate in a swap: length {length} years, value "
                        + $"{swapValues[n]} paying fixed rate {rates[n]}, value {swapValues[n + 1]} paying fixed rate "
                        + $"{rates[n + 1]}");
                }
            }
        }
    }

    [Fact]
    public void TestSpreadDependency()
    {
        // Testing vanilla-swap dependency on floating spread...

        var vars = new CommonVars(Context);

        Integer[] lengths = [1, 2, 5, 10, 20];
        Rate[] rates = [0.04, 0.05, 0.06, 0.07];
        Spread[] spreads = [-0.01, -0.002, -0.001, 0.0, 0.001, 0.002, 0.01];

        foreach (Integer length in lengths)
        {
            foreach (Rate j in rates)
            {
                // store the results for different spreads...
                var swapValues = new List<Real>();
                foreach (Spread spread in spreads)
                {
                    VanillaSwap swap = vars.MakeSwap(length, j, spread);
                    swapValues.Add(vars.Price(swap).NPV!.Value);
                }

                // and check that they go the right way
                for (int n = 0; n + 1 < swapValues.Count; ++n)
                {
                    Assert.False(
                        swapValues[n] > swapValues[n + 1],
                        $"NPV is decreasing with the floating spread in a swap: length {length} years, value "
                        + $"{swapValues[n]} receiving spread {spreads[n]}, value {swapValues[n + 1]} receiving spread "
                        + $"{spreads[n + 1]}");
                }
            }
        }
    }

    // Adapted: the evaluation date set after the fixture (line 299) becomes the fixture's context, and the curve
    // relinked to the new settlement date (line 302) becomes the index and the engine built on it. QuantLib reads the
    // global at-par switch (line 294) to choose its expected value (line 312); the case runs once in a context for each
    // setting of the switch, each with its expected value.
    [Fact]
    public void TestCachedValue()
    {
        // Testing vanilla-swap calculation against cached value...

        foreach (bool usingAtParCoupons in new[] { true, false })
        {
            var vars = new CommonVars(new EvaluationContext
            {
                EvaluationDate = new Date(17, June, 2002),
                UseAtParCoupons = usingAtParCoupons,
            });

            VanillaSwap swap = vars.MakeSwap(10, 0.06, 0.001);

            Assert.True(swap.NumberOfLegs == 2, $"failed to return correct number of legs: {swap.NumberOfLegs}");

            Real cachedNPV = usingAtParCoupons ? -5.872863313209 : -5.872342992212;

            Real npv = vars.Price(swap).NPV!.Value;
            Assert.True(
                Math.Abs(npv - cachedNPV) <= 1.0e-11,
                $"failed to reproduce cached swap value (at-par coupons {usingAtParCoupons}): calculated {npv}, "
                + $"expected {cachedNPV}");
        }
    }

    [Fact]
    public void TestThirdWednesdayAdjustment()
    {
        // Testing third-Wednesday adjustment...

        var vars = new CommonVars(Context);

        VanillaSwap swap = vars.MakeSwap(1, 0.0, -0.001, DateGenerationRule.ThirdWednesdayInclusive);

        Assert.True(
            swap.FloatingSchedule.StartDate() == new Date(16, September, 2015),
            $"Wrong Start Date {swap.FloatingSchedule.StartDate()}");

        Assert.True(
            swap.FloatingSchedule.EndDate() == new Date(21, September, 2016),
            $"Wrong End Date {swap.FloatingSchedule.EndDate()}");
    }

    // Adapted: the curve linked to a handle (line 515) and the index built on the handle after it (line 517) become
    // the index built on the curve, and the evaluation date (line 512) is the context's.
    [Fact]
    public void TestSettlementDaysEffectiveDateConflict()
    {
        // Testing that MakeVanillaSwap rejects settlementDays and effectiveDate together...

        Date today = new(15, January, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        YieldTermStructure yts = FlatForward.Create(0, new NullCalendar(), 0.03, new Actual365Fixed(), context);

        var index = new Euribor6M(yts);
        Date effectiveDate = new(19, January, 2026);

        // settlementDays first, then effectiveDate
        NanoQLException e = Assert.Throws<NanoQLException>(() =>
            new MakeVanillaSwap(new Period(5, TimeUnit.Years), index)
                .WithFixedRate(0.03)
                .WithSettlementDays(2)
                .WithEffectiveDate(effectiveDate)
                .ToVanillaSwap(context));
        Assert.Contains("cannot set both", e.Message, StringComparison.Ordinal);

        // effectiveDate first, then settlementDays
        e = Assert.Throws<NanoQLException>(() =>
            new MakeVanillaSwap(new Period(5, TimeUnit.Years), index)
                .WithFixedRate(0.03)
                .WithEffectiveDate(effectiveDate)
                .WithSettlementDays(2)
                .ToVanillaSwap(context));
        Assert.Contains("cannot set both", e.Message, StringComparison.Ordinal);

        // withSettlementDays alone works
        VanillaSwap swap1 = new MakeVanillaSwap(new Period(5, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .WithSettlementDays(2)
            .ToVanillaSwap(context);
        Assert.True(swap1.StartDate() != default);

        // withEffectiveDate alone works
        VanillaSwap swap2 = new MakeVanillaSwap(new Period(5, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .WithEffectiveDate(effectiveDate)
            .ToVanillaSwap(context);
        Assert.Equal(effectiveDate, swap2.StartDate());

        // neither set (constructor defaults) works
        VanillaSwap swap3 = new MakeVanillaSwap(new Period(5, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .ToVanillaSwap(context);
        Assert.True(swap3.StartDate() != default);
    }

    // Adapted: the evaluation date (line 627) is the context's.
    [Fact]
    public void TestSpotDateFromNonBusinessEvaluationDate()
    {
        // Testing that the swap spot date is calculated from the actual evaluation date when the latter is not a
        // business day...

        // Saturday
        Date today = new(20, June, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        var index = new Euribor6M();
        Calendar calendar = index.FixingCalendar;

        // settlement days are counted from the actual trade date,
        // not from the next business day
        Date expectedStart = calendar.Advance(today, 2, TimeUnit.Days);

        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .WithSettlementDays(2)
            .ToVanillaSwap(context);

        Assert.True(
            swap.StartDate() == expectedStart,
            $"swap start date not calculated from the actual evaluation date: expected {expectedStart}, obtained "
            + $"{swap.StartDate()}");
    }

    // Adapted: the evaluation date (line 657) is the context's.
    [Fact]
    public void TestSettlementCalendar()
    {
        // Testing that the swap spot date can be calculated on an explicit settlement calendar...

        // 3 July 2026 is a TARGET business day, but a US holiday
        // (Independence Day observed), so the settlement calendar and the
        // index fixing calendar diverge between here and the spot date.
        Date today = new(2, July, 2026);
        var context = new EvaluationContext { EvaluationDate = today };

        var index = new Euribor6M();
        Calendar settlementCalendar = new JointCalendar(new TARGET(), new UnitedStates(UnitedStatesMarket.Settlement));

        Date expectedStart = settlementCalendar.Advance(today, 2, TimeUnit.Days);

        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), index)
            .WithFixedRate(0.03)
            .WithSettlementDays(2)
            .WithSettlementCalendar(settlementCalendar)
            .ToVanillaSwap(context);

        Assert.True(
            swap.StartDate() == expectedStart,
            $"swap start date not calculated on the settlement calendar: expected {expectedStart}, obtained "
            + $"{swap.StartDate()}");

        // sanity check: the two calendars must actually diverge here
        Assert.True(expectedStart != index.FixingCalendar.Advance(today, 2, TimeUnit.Days));
    }

    // Adapted: QuantLib's fairRate() and fairSpread() throw "result not available" on a null result (lines 695-700);
    // nanoql's results hold it as null, so the checks read the null.
    [Fact]
    public void TestZeroBpsFairRateAndSpread()
    {
        // Testing vanilla swap fair rate/spread calculation with zero BPS...

        var vars = new CommonVars(Context) { Nominal = 0.0 };
        VanillaSwap swap = vars.MakeSwap(10, 0.0, 0.0);

        Assert.False(swap.IsExpired(Context));
        FixedVsFloatingSwapResults results = vars.Price(swap);
        Assert.Equal(0.0, results.LegBPS[0]);
        Assert.Equal(0.0, results.LegBPS[1]);

        Assert.Null(results.FairRate);
        Assert.Null(results.FairSpread);
    }

    // Adapted: the evaluation date moved after the swap is built (line 711) becomes a second context, which prices the
    // same swap; QuantLib's fairRate() and fairSpread() throw "result not available" on the null results (lines
    // 714-719), which nanoql's results hold as null.
    [Fact]
    public void TestExpiredSwapFairRateAndSpread()
    {
        // Testing vanilla swap fair rate/spread for expired swap...

        var vars = new CommonVars(Context);
        VanillaSwap swap = vars.MakeSwap(10, 0.0, 0.0);

        EvaluationContext later = Context with { EvaluationDate = vars.Settlement + new Period(20, TimeUnit.Years) };

        Assert.True(swap.IsExpired(later));
        FixedVsFloatingSwapResults results = DiscountingSwapEngine.Calculate(swap, later, vars.TermStructure);
        Assert.Null(results.FairRate);
        Assert.Null(results.FairSpread);
    }

    // Adapted: the term structure, a handle linked after the index is built on it (lines 98-103), becomes the curve the
    // index is built on, and the engine set on each swap (lines 84-85) becomes the pricing of each swap on that curve.
    private sealed class CommonVars
    {
        public CommonVars(EvaluationContext context)
        {
            Context = context;
            Type = SwapType.Payer;
            SettlementDays = 2;
            Nominal = 100.0;
            FixedConvention = BusinessDayConvention.Unadjusted;
            FloatingConvention = BusinessDayConvention.ModifiedFollowing;
            FixedFrequency = Frequency.Annual;
            FloatingFrequency = Frequency.Semiannual;
            FixedDayCount = new Thirty360(Thirty360Convention.BondBasis);
            Calendar = new Euribor(new Period(FloatingFrequency)).FixingCalendar;
            Today = Calendar.Adjust(context.EvaluationDate);
            Settlement = Calendar.Advance(Today, SettlementDays, TimeUnit.Days);
            TermStructure = new FlatForward(Settlement, 0.05, new Actual365Fixed());
            Index = new Euribor(new Period(FloatingFrequency), TermStructure);
        }

        public EvaluationContext Context { get; }

        public Date Today { get; }

        public Date Settlement { get; }

        public SwapType Type { get; }

        public Real Nominal { get; init; }

        public Calendar Calendar { get; }

        public BusinessDayConvention FixedConvention { get; }

        public BusinessDayConvention FloatingConvention { get; }

        public Frequency FixedFrequency { get; }

        public Frequency FloatingFrequency { get; }

        public DayCounter FixedDayCount { get; }

        public IborIndex Index { get; }

        public Natural SettlementDays { get; }

        public YieldTermStructure TermStructure { get; }

        public VanillaSwap MakeSwap(
            Integer length,
            Rate fixedRate,
            Spread floatingSpread,
            DateGenerationRule rule = DateGenerationRule.Forward)
        {
            Date maturity = Calendar.Advance(Settlement, length, TimeUnit.Years, FloatingConvention);
            var fixedSchedule = new Schedule(
                Settlement, maturity, new Period(FixedFrequency), Calendar, FixedConvention, FixedConvention, rule,
                false, Context);
            var floatSchedule = new Schedule(
                Settlement, maturity, new Period(FloatingFrequency), Calendar, FloatingConvention, FloatingConvention,
                rule, false, Context);
            return new VanillaSwap(
                Type,
                Nominal,
                fixedSchedule,
                fixedRate,
                FixedDayCount,
                floatSchedule,
                Index,
                floatingSpread,
                Index.DayCounter,
                Context);
        }

        public FixedVsFloatingSwapResults Price(VanillaSwap swap) =>
            DiscountingSwapEngine.Calculate(swap, Context, TermStructure);
    }
}
