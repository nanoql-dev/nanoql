// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Setting pricers on a leg. QuantLib's <c>setCouponPricer</c> and <c>setCouponPricers</c> change the coupons in place,
/// one after another (<c>couponpricer.cpp:461-466, 480-483</c>): a coupon that refuses the pricer leaves the earlier
/// ones holding it. Their undefined steps: an empty pricer list on a non-empty leg reads the pricer before the list's
/// start (<c>:481</c>), and a null cash flow is dereferenced (<c>:464, 482</c>; the first-matching forms swallow the
/// failure and skip it, <c>:446-456</c>). nanoql returns a new leg, all or nothing, and refuses both inputs.
/// </summary>
public class CouponPricerSettingTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.January, 2025) };

    private static readonly IborIndex Index = new Euribor6M(new FlatForward(Context.EvaluationDate, 0.03, new Actual365Fixed()));

    [Fact]
    public void ACouponRefusingThePricerLeavesNoNewLeg()
    {
        // QuantLib sets the floating-rate coupon's pricer, then refuses at the Ibor coupon.
        var plain = new FloatingRateCoupon(
            new Date(19, Month.January, 2026), 1e6, new Date(17, Month.July, 2025), new Date(19, Month.January, 2026), 2, Index);
        var ibor = new IborCoupon(
            new Date(17, Month.July, 2026), 1e6, new Date(19, Month.January, 2026), new Date(17, Month.July, 2026), 2, Index);
        ImmutableArray<CashFlow> leg = [plain, ibor];

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => CouponPricerFunctions.SetCouponPricer(leg, new AnyCouponPricer()));
        Assert.Equal("pricer", e.ParamName);
        Assert.Null(plain.Pricer);
        Assert.Null(((FloatingRateCoupon)leg[0]).Pricer);

        // The list form is all or nothing too.
        Assert.Throws<ArgumentException>(
            () => CouponPricerFunctions.SetCouponPricers(leg, [new AnyCouponPricer(), null]));
        Assert.Null(plain.Pricer);
    }

    [Fact]
    public void AnEmptyPricerListIsRefused()
    {
        var ibor = new IborCoupon(
            new Date(17, Month.July, 2026), 1e6, new Date(19, Month.January, 2026), new Date(17, Month.July, 2026), 2, Index);

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => CouponPricerFunctions.SetCouponPricers([ibor], []));
        Assert.Equal("pricers", e.ParamName);
        Assert.StartsWith("no pricers given", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullCashFlowInTheLegIsRefused()
    {
        var ibor = new IborCoupon(
            new Date(17, Month.July, 2026), 1e6, new Date(19, Month.January, 2026), new Date(17, Month.July, 2026), 2, Index);
        ImmutableArray<CashFlow> leg = [ibor, null!];
        var pricer = new BlackIborCouponPricer(Context);

        void AssertRefused(Func<object?> set)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(set);
            Assert.Equal("leg", e.ParamName);
            Assert.StartsWith("null cash flow in leg", e.Message, StringComparison.Ordinal);
        }

        AssertRefused(() => CouponPricerFunctions.SetCouponPricer(leg, pricer));
        AssertRefused(() => CouponPricerFunctions.SetCouponPricers(leg, [pricer]));

        // QuantLib's first-matching forms skip the null; nanoql's refuse it too.
        AssertRefused(() => CouponPricerFunctions.SetCouponPricers(leg, pricer, pricer));
        AssertRefused(() => CouponPricerFunctions.SetCouponPricers(leg, pricer, pricer, pricer));
        AssertRefused(() => CouponPricerFunctions.SetCouponPricers(leg, pricer, pricer, pricer, pricer));
        Assert.Null(ibor.Pricer);
    }

    // A pricer that isn't an Ibor pricer, which an Ibor coupon refuses.
    private sealed class AnyCouponPricer : FloatingRateCouponPricer
    {
        public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) => 0.0;

        public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) => 0.0;

        public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) => 0.0;

        public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) => 0.0;

        public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
            0.0;

        public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
            0.0;
    }
}
