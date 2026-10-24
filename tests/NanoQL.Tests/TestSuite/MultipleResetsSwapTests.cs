// SPDX-FileCopyrightText: Copyright (C) 2026 Zain Mughal
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/multipleresetsswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// Adapted throughout: the fixture's relinkable handle is the curve itself, built first, and its index fixing and
// evaluation date are its context; the swap QuantLib's builder gives an engine is priced by the static
// DiscountingSwapEngine.Calculate on the index's forwarding curve without the settlement date's flows, the engine the
// builder sets; and a helper QuantLib re-dates when the evaluation date moves is a second helper built under that date.
[Trait("Oracle", "QuantLib")]
public class MultipleResetsSwapTests
{
    [Fact]
    public void TestFairRate()
    {
        // Testing fair rate of multiple-resets swap...
        var vars = new CommonVars();

        MultipleResetsSwap swap = vars.MakeSwap(0.06);
        FixedVsFloatingSwapResults results = vars.Npv(swap);

        Rate? fair = results.FairRate;
        Assert.NotNull(fair);

        // Rebuilding at the fair rate must give zero NPV.
        MultipleResetsSwap fairSwap = vars.MakeSwap(fair.Value);
        Assert.True(Math.Abs(vars.Npv(fairSwap).NPV!.Value) <= 1.0e-8);

        // Cross-check: fixed-leg NPV + floating-leg NPV equals total NPV.
        Real npvCheck = results.FixedLegNPV!.Value + results.FloatingLegNPV!.Value;
        Assert.True(Math.Abs(npvCheck - results.NPV!.Value) <= 1.0e-10);

        // Omitting withFixedRate triggers auto-computation; NPV must be zero.
        MultipleResetsSwap autoFair = new MakeMultipleResetsSwap(new Period(2, TimeUnit.Years), vars.Euribor3m, 2)
            .WithSettlementDays(0)
            .WithNominal(1.0e6)
            .ToSwap(vars.Context);
        Assert.True(Math.Abs(vars.Npv(autoFair).NPV!.Value) <= 1.0e-8);
    }

    [Fact]
    public void TestConsistencyWithLeg()
    {
        // Testing that multiple-resets swap NPV is consistent with legs NPV...
        var vars = new CommonVars();

        foreach (SwapType type in new[] { SwapType.Payer, SwapType.Receiver })
        {
            MultipleResetsSwap swap = new MakeMultipleResetsSwap(new Period(2, TimeUnit.Years), vars.Euribor3m, 2)
                .WithFixedRate(0.05)
                .WithSettlementDays(0)
                .WithNominal(1.0e6)
                .WithType(type)
                .ToSwap(vars.Context);

            FixedVsFloatingSwapResults results = vars.Npv(swap);
            Real legSum = results.FixedLegNPV!.Value + results.FloatingLegNPV!.Value;
            Assert.True(Math.Abs(legSum - results.NPV!.Value) <= 1.0e-10);
        }
    }

    [Fact]
    public void TestAveragingVsCompounding()
    {
        // Testing averaging vs compounding in multiple-resets swaps...
        var vars = new CommonVars();

        const Rate fixedRate = 0.05;
        MultipleResetsSwap swapCompound = vars.MakeSwap(fixedRate, RateAveragingType.Compound);
        MultipleResetsSwap swapAverage = vars.MakeSwap(fixedRate, RateAveragingType.Simple);

        Assert.True(Math.Abs(vars.Npv(swapCompound).FairRate!.Value - vars.Npv(swapAverage).FairRate!.Value) > 1.0e-10);
    }

    [Fact]
    public void TestPaymentLagPreservedOnConversion()
    {
        // Testing payment lag preservation on conversion to a nonstandard swap...
        var vars = new CommonVars();
        MultipleResetsSwap baseSwap = vars.MakeSwap(0.05);
        const int paymentLag = 3;
        var laggedSwap = new MultipleResetsSwap(
            SwapType.Payer,
            1.0e6,
            baseSwap.FixedSchedule,
            0.05,
            baseSwap.FixedDayCount,
            baseSwap.FullResetSchedule,
            vars.Euribor3m,
            baseSwap.ResetsPerCoupon,
            0.0,
            RateAveragingType.Compound,
            null,
            paymentLag,
            vars.Calendar);

        var converted = new NonstandardSwap(laggedSwap, vars.Context);
        Assert.Equal(paymentLag, converted.PaymentLag);
        Assert.Equal(vars.Calendar, converted.PaymentCalendar);
        Assert.Equal(laggedSwap.FixedLeg.Length, converted.FixedLeg.Length);
        Assert.Equal(laggedSwap.FloatingLeg.Length, converted.FloatingLeg.Length);
        for (int i = 0; i < converted.FixedLeg.Length; ++i)
        {
            Assert.Equal(laggedSwap.FixedLeg[i].Date, converted.FixedLeg[i].Date);
        }

        for (int i = 0; i < converted.FloatingLeg.Length; ++i)
        {
            Assert.Equal(laggedSwap.FloatingLeg[i].Date, converted.FloatingLeg[i].Date);
        }
    }

    [Fact]
    public void TestRateHelper()
    {
        // Testing bootstrapping using multiple-resets swap helpers...
        var vars = new CommonVars();

        // Build a flat curve from multiple-resets swap quotes at 1Y, 2Y, 3Y.
        // A flat 5% input should bootstrap to a flat 5% output.
        const Rate inputRate = 0.05;
        Period[] tenors = [new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years)];
        var helpers = new List<BootstrapHelper<YieldTermStructure>>();
        foreach (Period tenor in tenors)
        {
            helpers.Add(new MultipleResetsSwapRateHelper(0, tenor, inputRate, vars.Euribor3m, 2, vars.Context));
        }

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(vars.Today, helpers, vars.DayCount, vars.Context);

        var indexOnCurve = new Euribor3M(curve);

        const Real tolerance = 1.0e-6;
        foreach (Period tenor in tenors)
        {
            MultipleResetsSwap check = new MakeMultipleResetsSwap(tenor, indexOnCurve, 2)
                .WithFixedRate(0.0)
                .WithSettlementDays(0)
                .WithNominal(1.0e6)
                .WithDiscountingTermStructure(curve)
                .ToSwap(vars.Context);
            Rate implied = DiscountingSwapEngine.Calculate((FixedVsFloatingSwap)check, vars.Context, curve, includeSettlementDateFlows: false)
                .FairRate!.Value;
            Assert.True(Math.Abs(implied - inputRate) <= tolerance, $"{tenor}: {implied}");
        }
    }

    [Fact]
    public void TestRateHelperSwap()
    {
        // Testing the swap underlying a multiple-resets swap helper...
        var vars = new CommonVars();

        MultipleResetsSwapRateHelper Helper(EvaluationContext on) =>
            new(0, new Period(2, TimeUnit.Years), 0.05, vars.Euribor3m, 2, on);
        MultipleResetsSwapRateHelper helper = Helper(vars.Context);

        MultipleResetsSwap swap = helper.Swap;
        Assert.NotNull(swap);
        Assert.Equal(helper.EarliestDate, swap.StartDate());
        Assert.Equal(4, swap.FloatingLeg.Length);

        MultipleResetsSwapRateHelper later =
            Helper(vars.Context with { EvaluationDate = vars.Calendar.Advance(vars.Today, new Period(1, TimeUnit.Years)) });
        Assert.NotSame(swap, later.Swap);
        Assert.Equal(later.EarliestDate, later.Swap.StartDate());
    }

    [Fact]
    public void TestMaturityOnHoliday()
    {
        // Testing multiple-resets swap schedules when the maturity is a holiday...
        var vars = new CommonVars();

        // Four years from January 15th, 2024 is Saturday, January 15th, 2028.
        // The schedules must still start on the start date, without a stub.
        MultipleResetsSwap swap = new MakeMultipleResetsSwap(new Period(4, TimeUnit.Years), vars.Euribor3m, 2)
            .WithFixedRate(0.05)
            .WithSettlementDays(0)
            .ToSwap(vars.Context);

        Assert.Equal(8, swap.FloatingLeg.Length);
        Assert.Equal(8, swap.FixedLeg.Length);
        Assert.Equal(vars.Today, swap.FixedSchedule.StartDate());
        Assert.Equal(vars.Today, swap.FloatingSchedule.StartDate());
        Assert.Equal(new Date(17, January, 2028), swap.MaturityDate());

        // With one reset per coupon no error is raised, so a stub would go unnoticed
        MultipleResetsSwap single = new MakeMultipleResetsSwap(new Period(4, TimeUnit.Years), vars.Euribor3m, 1)
            .WithFixedRate(0.05)
            .WithSettlementDays(0)
            .ToSwap(vars.Context);

        Assert.Equal(16, single.FloatingLeg.Length);
        Assert.Equal(16, single.FixedLeg.Length);
        Assert.Equal(vars.Today, single.FixedSchedule.StartDate());
        Assert.Equal(new Date(15, April, 2024), single.FixedSchedule.Date(1));
    }

    [Fact]
    public void TestRateHelperLastRelevantDate()
    {
        // Testing multiple-resets swap helper last relevant date...

        // The last reset starts on Monday, October 10th, 2016, since the 8th
        // is a Saturday, so its fixing forecasts Euribor until January 10th, 2017;
        // that is one day after the end of the swap.  The curve must reach that
        // date, or the bootstrap fails since the helper's pillar is too early.
        var today = new Date(6, January, 2016);
        var context = new EvaluationContext { EvaluationDate = today };

        var euribor3m = new Euribor3M();
        var helper = new MultipleResetsSwapRateHelper(2, new Period(1, TimeUnit.Years), 0.02, euribor3m, 2, context);

        var lastCoupon = helper.Swap.FloatingLeg[^1] as MultipleResetsCoupon;
        Assert.NotNull(lastCoupon);
        Date lastFixingEndDate = euribor3m.MaturityDate(euribor3m.ValueDate(lastCoupon.FixingDates[^1]));
        Assert.False(
            helper.LatestRelevantDate < lastFixingEndDate,
            $"latest relevant date ({helper.LatestRelevantDate}) is earlier than the end of the last fixing period "
            + $"({lastFixingEndDate})");
        Assert.Equal(helper.Swap.MaturityDate(), helper.MaturityDate);

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, ImmutableArray.Create<BootstrapHelper<YieldTermStructure>>(helper), new Actual365Fixed(), context);
        Exception? e = Record.Exception(() => curve.Discount(1.0));
        Assert.Null(e);
    }

    private sealed class CommonVars
    {
        // Flat 5% curve, 3M Euribor, 2 resets per semiannual coupon.
        public CommonVars()
        {
            Calendar = new TARGET();
            Today = Calendar.Adjust(new Date(15, January, 2024));
            DayCount = new Actual365Fixed();
            TermStructure = new FlatForward(Today, 0.05, DayCount);
            Euribor3m = new Euribor3M(TermStructure);
            var context = new EvaluationContext { EvaluationDate = Today };
            Context = context with { Fixings = context.Fixings.AddFixing(Euribor3m, new Date(11, January, 2024), 0.05) };
        }

        public Date Today { get; }

        public Calendar Calendar { get; }

        public DayCounter DayCount { get; }

        public YieldTermStructure TermStructure { get; }

        public IborIndex Euribor3m { get; }

        public EvaluationContext Context { get; }

        public MultipleResetsSwap MakeSwap(Rate fixedRate, RateAveragingType method = RateAveragingType.Compound) =>
            new MakeMultipleResetsSwap(new Period(2, TimeUnit.Years), Euribor3m, 2)
                .WithFixedRate(fixedRate)
                .WithSettlementDays(0)
                .WithNominal(1.0e6)
                .WithAveragingMethod(method)
                .ToSwap(Context);

        // The engine QuantLib's builder sets: the discounting engine on the index's forwarding curve, without the
        // settlement date's flows.
        public FixedVsFloatingSwapResults Npv(MultipleResetsSwap swap) =>
            DiscountingSwapEngine.Calculate(swap, Context, TermStructure, includeSettlementDateFlows: false);
    }
}
