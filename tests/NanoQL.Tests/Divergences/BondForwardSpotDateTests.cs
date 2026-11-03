// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The bond forward's spot value and its date: QuantLib's forward value is the spot value less the income over the
/// discount at delivery (<c>forward.cpp:61-65</c>), its spot the bond's dirty price, a value at the bond's settlement
/// date (<c>bondforward.cpp:86-87</c>), its income and both discounts from the curve's reference date, so a bond that
/// doesn't settle on that date gives a forward high by 1/P(ref, settlement). QuantLib's own
/// <c>testThatForwardValueIsEqualToSpotValueIfNoIncome</c> asserts the identity the factor breaks, within 1e-2 on a
/// 0.05% curve three days out. On the wheel, on a 5-year 4% bullet from 17 January 2025 (annual on TARGET,
/// Actual/Actual (ISMA)) settling in 2 days, on a flat 3% curve on 15 January 2025, its dirty price is
/// 104.35728008689668; QuantLib's forward delivering on its settlement date, the forward settling the same day, is
/// 104.37443611832695, and 104.41733853717292 delivering on 22 January, no coupon between, 1/P(ref, settlement) =
/// 1.0001643970733618 high. nanoql carries the spot back to the reference date on the forward's discount curve before
/// the income is netted, so its forward is the dirty price grown from settlement. QuantLib's check, its own forwards on
/// the same bullet settling on the reference date, where its spot is a reference-date value: 104.35728008689668 to 17
/// January, the 2-day bond's dirty price (the identity), and 104.40017545386986 to 22 January; nanoql's on the bond
/// settling in 2 days give the same.
/// </summary>
public class BondForwardSpotDateTests
{
    [Theory]
    [InlineData(0, 17, 104.35728008689668)]
    [InlineData(0, 22, 104.40017545386986)]
    [InlineData(2, 22, 104.40017545386986)]
    public void TheSpotIsCarriedBackFromTheBondsSettlement(int forwardSettlementDays, int deliveryDay, double quantLibs)
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var schedule = new Schedule(
            new Date(17, Month.January, 2025), new Date(17, Month.January, 2030), new Period(Frequency.Annual),
            new TARGET(), BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward,
            false, context);
        var bond = new FixedRateBond(2, 100.0, schedule, [0.04], new ActualActual(ActualActualConvention.ISMA));
        var delivery = new Date(deliveryDay, Month.January, 2025);
        var forward = new BondForward(
            today, delivery, PositionType.Long, 0.0, forwardSettlementDays, new Actual365Fixed(), new TARGET(),
            BusinessDayConvention.Following, bond, curve, curve);
        ForwardResults results = forward.Calculate(context, (b, c) => DiscountingBondEngine.Calculate(b, c, curve));

        Assert.Equal(new Date(17, Month.January, 2025), bond.SettlementDate(context));
        Assert.Equal(0.0, results.SpotIncome);
        Assert.True(Math.Abs(results.SpotValue!.Value - 104.35728008689668) <= 1e-11, $"{results.SpotValue}");

        // the dirty price grown from the bond's settlement, as QuantLib's forward on the bond settling on the reference
        // date is
        Assert.True(Math.Abs(results.ForwardValue!.Value - quantLibs) <= 1e-13, $"{results.ForwardValue:R}");
    }
}
