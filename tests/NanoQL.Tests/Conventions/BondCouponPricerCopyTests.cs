// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="CouponPricerFunctions.SetCouponPricer{T}(T, FloatingRateCouponPricer?)"/> returns a copy of a built bond
/// where QuantLib's <c>setCouponPricer(bond.cashflows(), pricer)</c> changes the coupons the bond holds: the copy has
/// the bond's own type and terms, the pricer on its floating coupons, and its notional schedule and redemptions, which
/// a re-priced leg keeps; the original is unchanged. A capped floating-rate bond, built with no pricer as in QuantLib,
/// prices once one is set.
/// </summary>
public class BondCouponPricerCopyTests
{
    [Fact]
    public void SetCouponPricerReturnsACopyOfTheBondsType()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.031, new Actual365Fixed());
        var schedule = new Schedule(
            new Date(17, Month.January, 2025), new Date(17, Month.January, 2030), new Period(6, TimeUnit.Months),
            new TARGET(), BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward, false, context);
        var capped = new FloatingRateBond(
            2, 100.0, schedule, new Euribor6M(curve), new Actual360(), context, caps: [0.04]);
        var vol = new ConstantOptionletVolatility(
            today, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed());
        var pricer = new BlackIborCouponPricer(context, vol);

        // Built with no pricer, as QuantLib's capped bond, it can't price.
        Assert.ThrowsAny<Exception>(() => DiscountingBondEngine.Calculate(capped, context, curve));

        FloatingRateBond priced = CouponPricerFunctions.SetCouponPricer(capped, pricer);

        // A copy of the bond's own type, with its terms, notional schedule and redemptions.
        Assert.NotSame(capped, priced);
        Assert.IsType<FloatingRateBond>(priced);
        Assert.Equal(capped.MaturityDate(), priced.MaturityDate());
        Assert.Equal(capped.SettlementDate(context), priced.SettlementDate(context));
        Assert.True(priced.Notionals == capped.Notionals);
        Assert.True(priced.Redemptions == capped.Redemptions);
        Assert.Same(capped.Redemption(), priced.Redemption());
        Assert.Contains(priced.Redemption(), priced.Cashflows);

        // The pricer is on the copy's floating coupons; the original's have none, and it still can't price.
        Assert.All(priced.Cashflows.OfType<FloatingRateCoupon>(), c => Assert.Same(pricer, c.Pricer));
        Assert.All(capped.Cashflows.OfType<FloatingRateCoupon>(), c => Assert.Null(c.Pricer));
        Assert.True(DiscountingBondEngine.Calculate(priced, context, curve).NPV > 0.0);
        Assert.ThrowsAny<Exception>(() => DiscountingBondEngine.Calculate(capped, context, curve));

        // Seen through its base, the bond keeps its runtime type.
        Bond asBase = capped;
        Assert.IsType<FloatingRateBond>(CouponPricerFunctions.SetCouponPricer(asBase, pricer));
    }
}
