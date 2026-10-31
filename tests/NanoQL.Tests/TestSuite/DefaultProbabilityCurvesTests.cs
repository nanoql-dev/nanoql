// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/defaultprobabilitycurves.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

/// <summary>QuantLib's default-probability curve tests, <c>test-suite/defaultprobabilitycurves.cpp</c>.</summary>
[Trait("Oracle", "QuantLib")]
public class DefaultProbabilityCurvesTests
{
    // QuantLib's test runner's evaluation date (quantlibglobalfixture.cpp:62-63)
    private static readonly Date RunnerDate = new(16, September, 2015);

    [Fact]
    public void TestDefaultProbability()
    {
        // Testing default-probability structure...

        double hazardRate = 0.0100;
        DayCounter dayCounter = new Actual360();
        Calendar calendar = new TARGET();
        int n = 20;

        double tolerance = 1.0e-10;
        Date today = RunnerDate;
        Date startDate = today;
        Date endDate = startDate;

        var flatHazardRate = new FlatHazardRate(startDate, hazardRate, dayCounter);

        for (int i = 0; i < n; i++)
        {
            startDate = endDate;
            endDate = calendar.Advance(endDate, 1, TimeUnit.Years);

            double pStart = flatHazardRate.DefaultProbability(startDate);
            double pEnd = flatHazardRate.DefaultProbability(endDate);

            double pBetweenComputed = flatHazardRate.DefaultProbability(startDate, endDate);

            double pBetween = pEnd - pStart;

            Assert.False(
                Math.Abs(pBetween - pBetweenComputed) > tolerance,
                "Failed to reproduce probability(d1, d2) for default probability structure\n"
                + $"    calculated probability: {pBetweenComputed:G12}\n    expected probability:   {pBetween:G12}");

            double t2 = dayCounter.YearFraction(today, endDate);
            double timeProbability = flatHazardRate.DefaultProbability(t2);
            double dateProbability = flatHazardRate.DefaultProbability(endDate);

            Assert.False(
                Math.Abs(timeProbability - dateProbability) > tolerance,
                "single-time probability and single-date probability do not match\n"
                + $"    time probability: {timeProbability:G10}\n    date probability: {dateProbability:G10}");

            double t1 = dayCounter.YearFraction(today, startDate);
            timeProbability = flatHazardRate.DefaultProbability(t1, t2);
            dateProbability = flatHazardRate.DefaultProbability(startDate, endDate);

            Assert.False(
                Math.Abs(timeProbability - dateProbability) > tolerance,
                "double-time probability and double-date probability do not match\n"
                + $"    time probability: {timeProbability:G10}\n    date probability: {dateProbability:G10}");
        }
    }

    [Fact]
    public void TestFlatHazardRate()
    {
        // Testing flat hazard rate...

        double hazardRate = 0.0100;
        DayCounter dayCounter = new Actual360();
        Calendar calendar = new TARGET();
        int n = 20;

        double tolerance = 1.0e-10;
        Date today = RunnerDate;
        Date startDate = today;
        Date endDate = startDate;

        var flatHazardRate = new FlatHazardRate(today, hazardRate, dayCounter);

        for (int i = 0; i < n; i++)
        {
            endDate = calendar.Advance(endDate, 1, TimeUnit.Years);
            double t = dayCounter.YearFraction(startDate, endDate);
            double probability = 1.0 - Math.Exp(-hazardRate * t);
            double computedProbability = flatHazardRate.DefaultProbability(t);

            Assert.False(
                Math.Abs(probability - computedProbability) > tolerance,
                "Failed to reproduce probability for flat hazard rate\n"
                + $"    calculated probability: {computedProbability:G10}\n    expected probability:   {probability:G10}");
        }
    }

    [Fact]
    public void TestFlatHazardConsistency()
    {
        // Testing piecewise-flat hazard-rate consistency...
        TestBootstrapFromSpread<HazardRate, BackwardFlat>();
        TestBootstrapFromUpfront<HazardRate, BackwardFlat>(Context());
    }

    [Fact]
    public void TestFlatDensityConsistency()
    {
        // Testing piecewise-flat default-density consistency...
        TestBootstrapFromSpread<DefaultDensity, BackwardFlat>();
        TestBootstrapFromUpfront<DefaultDensity, BackwardFlat>(Context());
    }

    [Fact]
    public void TestLinearDensityConsistency()
    {
        // Testing piecewise-linear default-density consistency...
        TestBootstrapFromSpread<DefaultDensity, Linear>();
        TestBootstrapFromUpfront<DefaultDensity, Linear>(Context());
    }

    [Fact]
    public void TestLogLinearSurvivalConsistency()
    {
        // Testing log-linear survival-probability consistency...
        TestBootstrapFromSpread<SurvivalProbability, LogLinear>();
        TestBootstrapFromUpfront<SurvivalProbability, LogLinear>(Context());
    }

    // Adapted: the curve bootstraps in its constructor, where QuantLib's recalculate() does.
    [Fact]
    public void TestSingleInstrumentBootstrap()
    {
        // Testing single-instrument curve bootstrap...

        Calendar calendar = new TARGET();

        EvaluationContext context = Context();
        Date today = context.EvaluationDate;

        int settlementDays = 0;

        double quote = 0.005;
        var tenor = new Period(2, TimeUnit.Years);

        Frequency frequency = Frequency.Quarterly;
        BusinessDayConvention convention = BusinessDayConvention.Following;
        DateGenerationRule rule = DateGenerationRule.TwentiethIMM;
        DayCounter dayCounter = new Thirty360(Thirty360Convention.BondBasis);
        double recoveryRate = 0.4;

        var discountCurve = new FlatForward(today, 0.06, new Actual360());

        BootstrapHelper<DefaultProbabilityTermStructure>[] helpers =
        [
            new SpreadCdsHelper(
                quote,
                tenor,
                settlementDays,
                calendar,
                frequency,
                convention,
                rule,
                dayCounter,
                recoveryRate,
                discountCurve,
                context),
        ];

        _ = new PiecewiseDefaultCurve<HazardRate, BackwardFlat, IterativeBootstrap>(today, helpers, dayCounter, context);
    }

    // Adapted: QuantLib sets the global flag to false and checks it is false again after the bootstrap; here the
    // context carries false and is left unchanged.
    [Fact]
    public void TestUpfrontBootstrap()
    {
        // Testing bootstrap on upfront quotes...

        // Setting this to false would prevent the upfront from being used.
        // By checking that the bootstrap works, we indirectly check that
        // UpfrontCdsHelper::impliedQuote() overrides it.
        EvaluationContext context = Context() with { IncludeTodaysCashFlows = false };

        TestBootstrapFromUpfront<HazardRate, BackwardFlat>(context);

        // This checks that UpfrontCdsHelper::impliedQuote() didn't
        // override the flag permanently; after the bootstrap, it should
        // go back to its previous value.
        Assert.False(context.IncludeTodaysCashFlows != false, "Cash-flow settings improperly modified");
    }

    // This test attempts to build a default curve from CDS spreads as of 1 Apr 2020. The spreads are real and from a
    // distressed reference entity with an inverted CDS spread curve. Using the default IterativeBootstrap with no
    // retries, the default curve building fails. Allowing retries, it expands the min survival probability bounds but
    // still fails. We set dontThrow to true in IterativeBootstrap to use a fall back curve.
    // Adapted: the curves bootstrap in their constructors, where QuantLib's bootstrap on the survival probability
    // asked.
    [Fact]
    public void TestIterativeBootstrapRetries()
    {
        // Testing iterative bootstrap with retries...

        var asof = new Date(1, April, 2020);
        var context = new EvaluationContext { EvaluationDate = asof };
        DayCounter tsDayCounter = new Actual365Fixed();

        // USD discount curve built out of FedFunds OIS swaps.
        Date[] usdCurveDates =
        [
            new(1, April, 2020),
            new(2, April, 2020),
            new(14, April, 2020),
            new(21, April, 2020),
            new(28, April, 2020),
            new(6, May, 2020),
            new(5, June, 2020),
            new(7, July, 2020),
            new(5, August, 2020),
            new(8, September, 2020),
            new(7, October, 2020),
            new(5, November, 2020),
            new(7, December, 2020),
            new(6, January, 2021),
            new(5, February, 2021),
            new(5, March, 2021),
            new(7, April, 2021),
            new(4, April, 2022),
            new(3, April, 2023),
            new(3, April, 2024),
            new(3, April, 2025),
            new(5, April, 2027),
            new(3, April, 2030),
            new(3, April, 2035),
            new(3, April, 2040),
            new(4, April, 2050),
        ];

        double[] usdCurveDfs =
        [
            1.000000000,
            0.999955835,
            0.999931070,
            0.999914629,
            0.999902799,
            0.999887990,
            0.999825782,
            0.999764392,
            0.999709076,
            0.999647785,
            0.999594638,
            0.999536198,
            0.999483093,
            0.999419291,
            0.999379417,
            0.999324981,
            0.999262356,
            0.999575101,
            0.996135441,
            0.995228348,
            0.989366687,
            0.979271200,
            0.961150726,
            0.926265361,
            0.891640651,
            0.839314063,
        ];

        var usdYts = new InterpolatedDiscountCurve<LogLinear>(usdCurveDates, usdCurveDfs, tsDayCounter);

        // CDS spreads, in the order of QuantLib's std::map on their tenors
        (Period Tenor, double Spread)[] cdsSpreads =
        [
            (new Period(6, TimeUnit.Months), 2.957980250),
            (new Period(1, TimeUnit.Years), 3.076933100),
            (new Period(2, TimeUnit.Years), 2.944524520),
            (new Period(3, TimeUnit.Years), 2.844498960),
            (new Period(4, TimeUnit.Years), 2.769234420),
            (new Period(5, TimeUnit.Years), 2.713474100),
        ];
        double recoveryRate = 0.035;

        // Conventions
        int settlementDays = 1;
        var calendar = new WeekendsOnly();
        Frequency frequency = Frequency.Quarterly;
        BusinessDayConvention paymentConvention = BusinessDayConvention.Following;
        DateGenerationRule rule = DateGenerationRule.CDS2015;
        var dayCounter = new Actual360();
        var lastPeriodDayCounter = new Actual360(true);

        // Create the CDS spread helpers.
        var instruments = new List<BootstrapHelper<DefaultProbabilityTermStructure>>(cdsSpreads.Length);
        foreach ((Period tenor, double spread) in cdsSpreads)
        {
            instruments.Add(new SpreadCdsHelper(
                spread,
                tenor,
                settlementDays,
                calendar,
                frequency,
                paymentConvention,
                rule,
                dayCounter,
                recoveryRate,
                usdYts,
                context,
                true,
                true,
                null,
                lastPeriodDayCounter));
        }

        // Create the default curve with the default IterativeBootstrap.
        // Check that the default curve throws by requesting a default probability.
        var testDate = new Date(21, December, 2020);
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new PiecewiseDefaultCurve<SurvivalProbability, LogLinear, IterativeBootstrap>(
                asof, instruments, tsDayCounter, context).SurvivalProbability(testDate));
        Assert.Contains("1st iteration: failed at 1st alive instrument", e.Message, StringComparison.Ordinal);

        // Create the default curve with an IterativeBootstrap allowing for 4 retries.
        // Use a maxFactor value of 1.0 so that we still use the previous survival probability at each pillar. In other
        // words, the survival probability cannot increase with time so best max at current pillar is the previous
        // pillar's value - there is no point increasing it on a retry.
        var ib = new IterativeBootstrap(null, null, null, 5, 1.0, 10.0);

        // Check that the default curve still throws. It throws at the third pillar because the survival probability is
        // too low at the second pillar.
        e = Assert.Throws<NanoQLException>(
            () => new PiecewiseDefaultCurve<SurvivalProbability, LogLinear, IterativeBootstrap>(
                asof, instruments, tsDayCounter, context, bootstrap: ib).SurvivalProbability(testDate));
        Assert.Contains("1st iteration: failed at 3rd alive instrument", e.Message, StringComparison.Ordinal);

        // Create the default curve with an IterativeBootstrap that allows for 4 retries and does not throw.
        var ibNoThrow = new IterativeBootstrap(null, null, null, 5, 1.0, 10.0, true, 2);
        var dpts = new PiecewiseDefaultCurve<SurvivalProbability, LogLinear, IterativeBootstrap>(
            asof, instruments, tsDayCounter, context, bootstrap: ibNoThrow);
        _ = dpts.SurvivalProbability(testDate);
    }

    [Fact]
    public void TestJumps()
    {
        // Testing that credit-curve jumps behave like yield-curve jumps...

        var today = new Date(15, June, 2024);
        DayCounter dc = new Actual365Fixed();
        double tolerance = 1.0e-10;

        Date[] dates = [today, today + (10 * 365)];
        double[] rates = [0.02, 0.02];

        // for each scenario the yield curve is the reference: with a flat rate the
        // jump factor is discount(t) / exp(-0.02 t), and survival should match it.
        (double Y, double C) Ratio(double[] jumps, Date[] jumpDates, double t)
        {
            var yield = new InterpolatedZeroCurve<Linear>(dates, rates, dc, null, jumps, jumpDates);
            var credit = new InterpolatedHazardRateCurve<Linear>(dates, rates, dc, null, jumps, jumpDates);
            double bare = Math.Exp(-0.02 * t);
            return (yield.Discount(t) / bare, credit.SurvivalProbability(t) / bare);
        }

        double q = 0.99;

        // a jump whose date is already in the past must be ignored
        {
            (double y, double c) = Ratio([q], [today - 30], 1.0);
            AssertClose(y, 1.0, tolerance);
            AssertClose(c, y, tolerance);
        }

        // a jump on the reference date must be ignored
        {
            (double y, double c) = Ratio([q], [today], 1.0);
            AssertClose(y, 1.0, tolerance);
            AssertClose(c, y, tolerance);
        }

        // unsorted jump dates: the jump before t applies, the one after does not
        {
            double a = 0.90;
            double b = 0.95;
            (double y, double c) = Ratio([a, b], [today + (5 * 365), today + 365], 2.0);
            AssertClose(y, 0.95, tolerance);
            AssertClose(c, y, tolerance);
        }

        // a jump inside the interval still applies
        {
            (double y, double c) = Ratio([q], [today + 180], 1.0);
            AssertClose(y, 0.99, tolerance);
            AssertClose(c, y, tolerance);
        }
    }

    // QuantLib's evaluation date: the fixture's, today's date.
    private static EvaluationContext Context() => new() { EvaluationDate = RunnerDate };

    // BOOST_CHECK_CLOSE: the relative difference, in percent, within the tolerance from each side.
    private static void AssertClose(double a, double b, double tolerancePercent)
    {
        double diff = Math.Abs(a - b);
        Assert.True(
            diff <= tolerancePercent / 100.0 * Math.Abs(a) && diff <= tolerancePercent / 100.0 * Math.Abs(b),
            $"difference between {a} and {b} exceeds {tolerancePercent}%");
    }

    // Adapted: QuantLib's curve bootstraps lazily, at its first use inside the block that includes today's cash flows,
    // so under that setting; here it is built under the block's context.
    private static void TestBootstrapFromSpread<T, I>()
        where T : struct, IDefaultProbabilityTraits
        where I : struct, IInterpolator
    {
        Calendar calendar = new TARGET();

        EvaluationContext context = Context();
        Date today = context.EvaluationDate;

        int settlementDays = 1;

        double[] quote = [0.005, 0.006, 0.007, 0.009];
        int[] n = [1, 2, 3, 5];

        Frequency frequency = Frequency.Quarterly;
        BusinessDayConvention convention = BusinessDayConvention.Following;
        DateGenerationRule rule = DateGenerationRule.TwentiethIMM;
        DayCounter dayCounter = new Thirty360(Thirty360Convention.BondBasis);
        double recoveryRate = 0.4;

        var discountCurve = new FlatForward(today, 0.06, new Actual360());

        var helpers = new List<BootstrapHelper<DefaultProbabilityTermStructure>>(n.Length);
        for (int i = 0; i < n.Length; i++)
        {
            helpers.Add(new SpreadCdsHelper(
                quote[i],
                new Period(n[i], TimeUnit.Years),
                settlementDays,
                calendar,
                frequency,
                convention,
                rule,
                dayCounter,
                recoveryRate,
                discountCurve,
                context));
        }

        double notional = 1.0;
        double tolerance = 1.0e-6;

        {
            // ensure apple-to-apple comparison
            EvaluationContext restore = context with { IncludeTodaysCashFlows = true };

            var piecewiseCurve = new PiecewiseDefaultCurve<T, I, IterativeBootstrap>(
                today, helpers, new Thirty360(Thirty360Convention.BondBasis), restore);

            for (int i = 0; i < n.Length; i++)
            {
                Date protectionStart = today + settlementDays;
                Date startDate = calendar.Adjust(protectionStart, convention);
                Date endDate = today + new Period(n[i], TimeUnit.Years);

                var schedule = new Schedule(
                    startDate,
                    endDate,
                    new Period(frequency),
                    calendar,
                    convention,
                    BusinessDayConvention.Unadjusted,
                    rule,
                    false,
                    restore);

                var cds = new CreditDefaultSwap(
                    ProtectionSide.Buyer, notional, quote[i], schedule, convention, dayCounter, true, true, protectionStart);
                CreditDefaultSwapResults results =
                    MidPointCdsEngine.Calculate(cds, restore, piecewiseCurve, recoveryRate, discountCurve);

                // test
                double inputRate = quote[i];
                double computedRate = results.FairSpread!.Value;
                Assert.False(
                    Math.Abs(inputRate - computedRate) > tolerance,
                    $"\nFailed to reproduce fair spread for {n[i]}Y credit-default swaps\n"
                    + $"    computed rate: {computedRate:G10}\n    input rate:    {inputRate:G10}");
            }
        }
    }

    // Adapted: as TestBootstrapFromSpread, the curve is built under the block's context.
    private static void TestBootstrapFromUpfront<T, I>(EvaluationContext context)
        where T : struct, IDefaultProbabilityTraits
        where I : struct, IInterpolator
    {
        Calendar calendar = new TARGET();

        Date today = context.EvaluationDate;

        int settlementDays = 1;

        double[] quote = [0.01, 0.02, 0.04, 0.06];
        int[] n = [2, 3, 5, 7];

        double fixedRate = 0.05;
        Frequency frequency = Frequency.Quarterly;
        BusinessDayConvention convention = BusinessDayConvention.ModifiedFollowing;
        DateGenerationRule rule = DateGenerationRule.CDS;
        DayCounter dayCounter = new Actual360();
        double recoveryRate = 0.4;
        int upfrontSettlementDays = 3;

        var discountCurve = new FlatForward(today, 0.06, new Actual360());

        var helpers = new List<BootstrapHelper<DefaultProbabilityTermStructure>>(n.Length);
        for (int i = 0; i < n.Length; i++)
        {
            helpers.Add(new UpfrontCdsHelper(
                quote[i],
                fixedRate,
                new Period(n[i], TimeUnit.Years),
                settlementDays,
                calendar,
                frequency,
                convention,
                rule,
                dayCounter,
                recoveryRate,
                discountCurve,
                context,
                upfrontSettlementDays,
                true,
                true,
                null,
                new Actual360(true)));
        }

        double notional = 1.0;
        double tolerance = 1.0e-6;

        {
            // ensure apple-to-apple comparison
            EvaluationContext backup = context with { IncludeTodaysCashFlows = true };

            var piecewiseCurve = new PiecewiseDefaultCurve<T, I, IterativeBootstrap>(
                today, helpers, new Thirty360(Thirty360Convention.BondBasis), backup);

            for (int i = 0; i < n.Length; i++)
            {
                Date protectionStart = today + settlementDays;
                Date startDate = protectionStart;
                Date endDate = CreditDefaultSwapFunctions.CdsMaturity(today, new Period(n[i], TimeUnit.Years), rule)!.Value;
                Date upfrontDate = calendar.Advance(today, upfrontSettlementDays, TimeUnit.Days, convention);

                var schedule = new Schedule(
                    startDate,
                    endDate,
                    new Period(frequency),
                    calendar,
                    convention,
                    BusinessDayConvention.Unadjusted,
                    rule,
                    false,
                    backup);

                var cds = new CreditDefaultSwap(
                    ProtectionSide.Buyer,
                    notional,
                    quote[i],
                    fixedRate,
                    schedule,
                    convention,
                    dayCounter,
                    true,
                    true,
                    protectionStart,
                    upfrontDate,
                    null,
                    new Actual360(true),
                    true);
                CreditDefaultSwapResults results =
                    MidPointCdsEngine.Calculate(cds, backup, piecewiseCurve, recoveryRate, discountCurve, true);

                // test
                double inputUpfront = quote[i];
                double computedUpfront = results.FairUpfront!.Value;
                Assert.False(
                    Math.Abs(inputUpfront - computedUpfront) > tolerance,
                    $"\nFailed to reproduce fair upfront for {n[i]}Y credit-default swaps\n"
                    + $"    computed: {computedUpfront:G10}\n    expected: {inputUpfront:G10}");
            }
        }
    }
}
