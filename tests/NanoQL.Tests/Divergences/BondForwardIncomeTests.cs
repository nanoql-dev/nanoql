// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The bond forward's income (the S-cms plan's CMR5 (xxv)): QuantLib's <c>BondForward::spotIncome</c> sums the coupons'
/// amounts, on the bond's face, paid between settlement and delivery (<c>bondforward.cpp:59-83</c>), while its spot
/// value is the dirty price, per 100 (<c>:86-87</c>). On the wheel, on QuantLib's test-suite bond (2.5%, 15 August
/// 2015 to 2046, annual on TARGET, Actual/Actual (ISDA)) and curve (flat 0.04977%) on 7 March 2022, the bond settling on
/// the curve's reference date, where QuantLib's spot value is a reference-date value (C4's review, C4-O1,
/// <see cref="BondForwardSpotDateTests"/>), delivering on 10 September 2022, after the August coupon: on a face of 100
/// the income is 2.492603415097658 and the forward value 158.4458479330134 (clean 158.2540671110956); on QuantLib's own
/// face, 1e5, the income is 2,492.6034150976584 and the forward value −2,332.3067807511693. Before the coupon, on 10
/// March, the income is 0 and the forward value 160.89828121347867 on either face. A face of 100 isn't enough: an
/// amortizing bond's dirty price is per 100 of its notional at settlement, so on a 4% bond of notionals 100, 75, 50 and
/// 25 (2022 to 2032, annual on TARGET), 50 on 15 January 2025, delivering on 3 April 2025 after the March coupon and
/// amortization, QuantLib's income is 26.872912556834198 and its forward value 81.3910471000986 on a flat 2.6% curve.
/// nanoql's income is per 100 of the bond's notional at its settlement date, as the spot value is. QuantLib's check: its
/// face-100 forward on the same bond, the forward being per 100; and its forward on the amortizing bond's notionals
/// doubled, 100 at settlement, 54.3684086579997. The clean forward follows the forward's basis (C4's review, C4-O2):
/// QuantLib's <c>cleanForwardPrice</c> subtracts the accrued amount at delivery per 100 of the notional at delivery
/// (<c>bondforward.cpp:49-51</c>), so on a 4% bond of notionals 100, 100, 50 and 50 (15 July 2024 to 2028, annual,
/// Actual/Actual (ISMA)) delivering on 15 October 2026, after its step to 50, on a flat 3% curve on 15 January 2025, its
/// clean forward is 50.278704784168255 from a dirty one of 51.286923962250434; nanoql's takes the accrued amount per 100
/// of the notional at settlement, 50.78281437320934. QuantLib's check: its forward on a face-100 bullet, whose flows
/// after delivery are twice the amortizing bond's, halved: 51.286923962250434 dirty and 50.78281437320934 clean.
/// </summary>
public class BondForwardIncomeTests
{
    private static readonly Date Today = new(7, Month.March, 2022);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.0004977, new Actual365Fixed());

    [Fact]
    public void AfterACouponTheForwardIsPerHundred()
    {
        BondForward forward = Forward(1e5, new Date(10, Month.September, 2022));
        ForwardResults results = forward.Calculate(Context, BondEngine);
        Assert.True(Math.Abs(results.SpotIncome!.Value - 2.492603415097658) <= 1e-12, $"{results.SpotIncome}");
        Assert.True(Math.Abs(results.ForwardValue!.Value - 158.4458479330134) <= 1e-10, $"{results.ForwardValue}");
        Assert.True(Math.Abs(forward.CleanForwardPrice(results, Context) - 158.2540671110956) <= 1e-10);
    }

    [Theory]
    [InlineData(100.0)]
    [InlineData(1e5)]
    public void BeforeACouponTheForwardIsQuantLibs(double face)
    {
        ForwardResults results = Forward(face, new Date(10, Month.March, 2022)).Calculate(Context, BondEngine);
        Assert.Equal(0.0, results.SpotIncome);
        Assert.True(Math.Abs(results.ForwardValue!.Value - 160.89828121347867) <= 1e-10, $"{results.ForwardValue}");
    }

    [Fact]
    public void AnAmortizedBondsForwardIsPerHundredOfItsNotional()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.026, new Actual365Fixed());
        var schedule = new Schedule(
            new Date(20, Month.March, 2022), new Date(20, Month.March, 2032), new Period(1, TimeUnit.Years), new TARGET(),
            BusinessDayConvention.Following, BusinessDayConvention.Following, DateGenerationRule.Backward, false, context);
        var bond = new AmortizingFixedRateBond(
            0, [100.0, 75.0, 50.0, 25.0], schedule, [0.04], new ActualActual(ActualActualConvention.ISDA));
        var forward = new BondForward(
            today, new Date(3, Month.April, 2025), PositionType.Long, 0.0, 2, new ActualActual(ActualActualConvention.ISDA),
            new TARGET(), BusinessDayConvention.Following, bond, curve, curve);
        ForwardResults results = forward.Calculate(context, (b, c) => DiscountingBondEngine.Calculate(b, c, curve));
        Assert.True(Math.Abs(results.ForwardValue!.Value - 54.3684086579997) <= 1e-10, $"{results.ForwardValue}");
    }

    [Fact]
    public void TheCleanForwardAcrossAStepIsOnTheForwardsBasis()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var schedule = new Schedule(
            new Date(15, Month.July, 2024), new Date(15, Month.July, 2028), new Period(Frequency.Annual), new TARGET(),
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false,
            context);
        var bond = new AmortizingFixedRateBond(
            0, [100.0, 100.0, 50.0, 50.0], schedule, [0.04], new ActualActual(ActualActualConvention.ISMA));
        var forward = new BondForward(
            today, new Date(15, Month.October, 2026), PositionType.Long, 0.0, 0, new Actual365Fixed(), new TARGET(),
            BusinessDayConvention.Following, bond, curve, curve);
        ForwardResults results = forward.Calculate(context, (b, c) => DiscountingBondEngine.Calculate(b, c, curve));

        // QuantLib's clean forward: 50.278704784168255
        Assert.True(Math.Abs(results.ForwardValue!.Value - 51.286923962250434) <= 1e-10, $"{results.ForwardValue}");
        double clean = forward.CleanForwardPrice(results, context);
        Assert.True(Math.Abs(clean - 50.78281437320934) <= 1e-10, $"{clean}");
    }

    private static BondResults BondEngine(Bond bond, EvaluationContext context) =>
        DiscountingBondEngine.Calculate(bond, context, Curve);

    private static BondForward Forward(double face, Date delivery)
    {
        var schedule = new Schedule(
            new Date(15, Month.August, 2015), new Date(15, Month.August, 2046), new Period(Frequency.Annual), new TARGET(),
            BusinessDayConvention.Following, BusinessDayConvention.Following, DateGenerationRule.Backward, false, Context);
        var bond = new FixedRateBond(0, face, schedule, [0.025], new ActualActual(ActualActualConvention.ISDA));
        return new BondForward(
            Today, delivery, PositionType.Long, 0.0, 2, new ActualActual(ActualActualConvention.ISDA), new TARGET(),
            BusinessDayConvention.Following, bond, Curve, Curve);
    }
}
