// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The tree swaption's snapping and payments (the S-cms plan's CMR4): QuantLib's discretized swaption snaps a swap date
/// within a week of an exercise date to it, either way (<c>discretizedswaption.cpp:93-111</c>), so a swap starting on
/// its usual two-day lag after the exercise is valued from the exercise date; it rebuilds the swap on its snapped
/// schedules' dates with a <c>NullCalendar</c> (<c>:113-119</c>), so its coupons pay on those dates, without the swap's
/// payment calendar, convention or lag; and its discretized swap values a floating coupon as a par floater to its
/// payment date (<c>discretizedswap.cpp:199-218</c>). On Hull-White (0.1, 0.01) on a flat 4% curve, 800 steps: a
/// European 5Y into 5Y payer on 1e6 at 4% errs against Jamshidian's engine by 50.521421552461106 on the lag and
/// 13.786750153689354 without it (at 5%, 11.848729083691069 and −6.138422108044324); the Bermudan exercised two
/// business days before each fixed reset, against the finite-difference engine (400 × 800), by 69.91132298134835 and
/// 12.102443341802427; a Bermudan on an ESTR swap paying two days late, against the finite-difference engine (400 ×
/// 400), by 82.16860415945848 and 71.98241275852342 at 3.5% (64.7080268507616 and 60.15433851566377 at 4.5%) with and
/// without the payment lag. QuantLib's tree swap engine, on a 5-year payer on 1e6 at 4% whose unadjusted floating
/// dates fall three times on a weekend and pay the next business day, prices 3,947.8257420389527 against the
/// discounting engine's 3,461.83299033012 (on the harness: the wheel has no tree swap engine). nanoql snaps no date
/// after an exercise date (the week before still snaps, with #1327's post-adjustment, which fixed #1143), pays each
/// coupon on the swap's own payment date, a snapped one on the swap's payment rule applied to its snapped date, and
/// values a floating coupon paid after its accrual end as its forward over its own accrual period paid on its payment
/// date; its tree swap is then within 1.34 of the discounting engine, at 50 steps as at 3,200, the model's adjustment
/// for the payment delay (1.26) and the par coupons' fixing periods (0.08, its value when the same coupons pay on their
/// accrual ends). QuantLib's checks: Jamshidian's and the finite-difference engines, within the tree's own error on the
/// unlagged swap; the discounting engine; and QuantLib 1.25's cached tree values, which
/// <c>TestSuite/BermudanSwaptionTests.cs</c> restates, with QuantLib's
/// <c>testTreeEngineTimeSnapping</c> there within its 1.0.
/// </summary>
public class TreeSwaptionSnappingTests
{
    private static readonly Date Today = new(22, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.04, new Actual365Fixed());
    private static readonly HullWhite Model = new(Curve, 0.1, 0.01);
    private static readonly Calendar Target = new TARGET();

    [Theory]
    [InlineData(0.04)]
    [InlineData(0.05)]
    public void AEuropeanOnALaggedSwapErrsAsTheUnlaggedOne(double strike)
    {
        double lagged = TreeError(Swaption(2, strike, false));
        double unlagged = TreeError(Swaption(0, strike, false));
        Assert.True(Math.Abs(lagged - unlagged) <= 1.0, $"{lagged} {unlagged}");

        double TreeError(Swaption s) =>
            TreeSwaptionEngine.Calculate(s, Context, Model, 800).NPV!.Value
            - JamshidianSwaptionEngine.Calculate(s, Context, Model).NPV!.Value;
    }

    [Fact]
    public void ABermudanOnALaggedSwapErrsAsTheUnlaggedOne()
    {
        double lagged = TreeError(Swaption(2, 0.04, true));
        double unlagged = TreeError(Swaption(0, 0.04, true));
        Assert.True(Math.Abs(lagged - unlagged) <= 1.0, $"{lagged} {unlagged}");

        static double TreeError(Swaption s) =>
            TreeSwaptionEngine.Calculate(s, Context, Model, 800).NPV!.Value
            - FdHullWhiteSwaptionEngine.Calculate(s, Context, Model, 400, 800).NPV!.Value;
    }

    [Theory]
    [InlineData(0.035)]
    [InlineData(0.045)]
    public void ALaggedOvernightBermudanErrsAsTheUnlaggedOne(double strike)
    {
        double lagged = TreeError(OvernightSwaption(2, strike));
        double unlagged = TreeError(OvernightSwaption(0, strike));
        Assert.True(Math.Abs(lagged - unlagged) <= 3.0, $"{lagged} {unlagged}");

        static double TreeError(Swaption s) =>
            TreeSwaptionEngine.Calculate(s, Context, Model, 800).NPV!.Value
            - FdHullWhiteSwaptionEngine.Calculate(s, Context, Model, 400, 400).NPV!.Value;
    }

    [Fact]
    public void TheTreeSwapEngineTakesALateCouponsForwardOverItsAccrual()
    {
        // a 5-year payer on 1e6 at 4% from 15 January 2026, on unadjusted schedules paying Following: its floating
        // dates of 15 January 2028, 15 July 2028 and 15 July 2029 fall on weekends
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.04, new Actual365Fixed());
        var model = new HullWhite(curve, 0.1, 0.01);
        var start = new Date(15, Month.January, 2026);
        Date end = start + new Period(5, TimeUnit.Years);
        Schedule fixedSchedule = UnadjustedSchedule(start, end, new Period(1, TimeUnit.Years), context);
        Schedule floatSchedule = UnadjustedSchedule(start, end, new Period(6, TimeUnit.Months), context);
        var index = new Euribor6M(curve);
        var swap = new VanillaSwap(
            SwapType.Payer, 1e6, fixedSchedule, 0.04, new Actual365Fixed(), floatSchedule, index, 0.0, new Actual360(), context,
            BusinessDayConvention.Following);

        double discounting = DiscountingSwapEngine.Calculate(swap, context, curve).NPV!.Value;
        double tree = TreeVanillaSwapEngine.Calculate(swap, context, model, 800).NPV!.Value;
        Assert.True(Math.Abs(discounting - 3461.83299033012) <= 1e-6, $"{discounting}");
        Assert.True(Math.Abs(tree - discounting) <= 2.0, $"{tree} {discounting}");
    }

    // A 5Y into 5Y payer on Euribor 6M on 1e6, its swap starting the lag's business days after the exercise: European
    // on the exercise date, or Bermudan the lag's business days before each fixed reset.
    private static Swaption Swaption(int lag, double strike, bool bermudan)
    {
        Date exerciseDate = Target.Advance(Today, new Period(5, TimeUnit.Years));
        Date start = Target.Advance(exerciseDate, lag, TimeUnit.Days);
        Date end = start + new Period(5, TimeUnit.Years);
        var fixedSchedule = new Schedule(
            start, end, new Period(1, TimeUnit.Years), Target, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        var floatSchedule = new Schedule(
            start, end, new Period(6, TimeUnit.Months), Target, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        var swap = new VanillaSwap(
            SwapType.Payer, 1e6, fixedSchedule, strike, new Actual365Fixed(), floatSchedule, new Euribor6M(Curve), 0.0,
            new Actual360(), Context);
        Exercise exercise = bermudan
            ? new BermudanExercise(fixedSchedule.Dates.Take(fixedSchedule.Dates.Length - 1).Select(d => Target.Advance(d, -lag, TimeUnit.Days)))
            : new EuropeanExercise(exerciseDate);
        return new Swaption(swap, exercise);
    }

    // A 1Y into 5Y payer on ESTR on 1e6, paying the lag's business days after each period, Bermudan two business days
    // before each reset.
    private static Swaption OvernightSwaption(int paymentLag, double strike)
    {
        Date start = Target.Advance(Today, new Period(1, TimeUnit.Years));
        Date end = start + new Period(5, TimeUnit.Years);
        var schedule = new Schedule(
            start, end, new Period(1, TimeUnit.Years), Target, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        var swap = new OvernightIndexedSwap(
            SwapType.Payer, 1e6, schedule, strike, new Actual360(), new Estr(Curve), 0.0, paymentLag,
            BusinessDayConvention.Following, Target);
        var exercise = new BermudanExercise(schedule.Dates.Take(schedule.Dates.Length - 1).Select(d => Target.Advance(d, -2, TimeUnit.Days)));
        return new Swaption(swap, exercise);
    }

    private static Schedule UnadjustedSchedule(Date start, Date end, Period tenor, EvaluationContext context) => new(
        start, end, tenor, new TARGET(), BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted,
        DateGenerationRule.Forward, false, context);
}
