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
/// <see cref="CouponPricerFunctions.SetCouponPricer{T}(T, int, FloatingRateCouponPricer?)"/> returns a copy of a built
/// swap where QuantLib's <c>setCouponPricer(swap->leg(j), pricer)</c> changes the coupons the swap holds: the copy has
/// the swap's own type and terms, the pricer on that leg's coupons and the other leg as it was, and the original is
/// unchanged.
/// </summary>
public class SwapCouponPricerCopyTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly FlatForward Curve = new(Today, 0.031, new Actual365Fixed());

    [Fact]
    public void SetCouponPricerReturnsACopyOfTheSwapsType()
    {
        VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve))
            .WithFixedRate(0.03)
            .WithFloatingLegTenor(new Period(3, TimeUnit.Months))
            .ToVanillaSwap(Context);
        var vol = new ConstantOptionletVolatility(
            Today, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed());
        var pricer = new BlackIborCouponPricer(
            vol, BlackIborCouponPricerTimingAdjustment.BivariateLognormal, 0.8, useIndexedCoupon: true);

        VanillaSwap priced = CouponPricerFunctions.SetCouponPricer(swap, 1, pricer);

        // A copy of the swap's own type, with its terms.
        Assert.NotSame(swap, priced);
        Assert.IsType<VanillaSwap>(priced);
        Assert.Equal(swap.FixedRate, priced.FixedRate);
        Assert.Same(swap.FloatingSchedule, priced.FloatingSchedule);
        Assert.Same(swap.IborIndex, priced.IborIndex);

        // The pricer is on the copy's floating coupons; the original's keep theirs, and the fixed leg is shared.
        Assert.All(priced.FloatingLeg, cf => Assert.Same(pricer, ((FloatingRateCoupon)cf).Pricer));
        Assert.All(swap.FloatingLeg, cf => Assert.NotSame(pricer, ((FloatingRateCoupon)cf).Pricer));
        Assert.True(priced.FixedLeg == swap.FixedLeg);
        Assert.NotEqual(
            DiscountingSwapEngine.Calculate(swap, Context, Curve).FloatingLegNPV,
            DiscountingSwapEngine.Calculate(priced, Context, Curve).FloatingLegNPV);

        // Seen through its base, an overnight-indexed swap keeps its runtime type and its own terms.
        FixedVsFloatingSwap ois = new MakeOIS(new Period(2, TimeUnit.Years), new Estr(Curve))
            .WithFixedRate(0.03)
            .WithAveragingMethod(RateAveragingType.Simple)
            .WithOvernightLegSpread(0.001)
            .WithPaymentLag(2)
            .ToOvernightIndexedSwap(Context);
        var averaging = new ArithmeticAveragedOvernightIndexedCouponPricer(byApprox: true);
        FixedVsFloatingSwap pricedOis = CouponPricerFunctions.SetCouponPricer(ois, 1, averaging);
        OvernightIndexedSwap copy = Assert.IsType<OvernightIndexedSwap>(pricedOis);
        Assert.Equal(0.001, copy.Spread);
        Assert.Equal(2, copy.PaymentLag);
        Assert.Equal(RateAveragingType.Simple, copy.AveragingMethod);
        Assert.All(copy.OvernightLeg, cf => Assert.Same(averaging, ((FloatingRateCoupon)cf).Pricer));
        Assert.All(
            ((OvernightIndexedSwap)ois).OvernightLeg, cf => Assert.NotSame(averaging, ((FloatingRateCoupon)cf).Pricer));

        // A leg the swap doesn't have is refused with QuantLib's text: that of swap->leg(j), the accessor QuantLib's
        // call reads the leg through (swap.hpp:112, "leg #", where legBPS at swap.hpp:83 writes "leg# "). A coupon's
        // refusal leaves no copy.
        ArgumentException e =
            Assert.Throws<ArgumentException>(() => CouponPricerFunctions.SetCouponPricer(swap, 2, pricer));
        Assert.Equal("leg", e.ParamName);
        Assert.StartsWith("leg #2 doesn't exist!", e.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => CouponPricerFunctions.SetCouponPricer(ois, 1, pricer));
    }
}
