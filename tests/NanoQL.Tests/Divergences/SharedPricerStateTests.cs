// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Two capped coupons sharing one pricer. QuantLib's capped coupon prices its caplet with whatever per-coupon state the
/// last <c>initialize(coupon)</c> left in the shared pricer (<c>capflooredcoupon.cpp:86-96</c>,
/// <c>couponpricer.cpp:96-113, 139-148</c>): once both underlyings' rates have been read, the first capped coupon's
/// caplet reads the second underlying's dates, gearing and spread, and its rate is 0.02266598964839617 against
/// 0.030228654513094377 alone. nanoql's pricers hold no per-coupon state, so each capped coupon prices as it would
/// alone, whatever was read before.
/// </summary>
public class SharedPricerStateTests
{
    [Fact]
    public void ACappedCouponPricesAsItWouldAloneWithASharedPricer()
    {
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        var index = new Euribor6M(new FlatForward(today, 0.03, new Actual360()));
        var vol = new ConstantOptionletVolatility(
            today, new TARGET(), BusinessDayConvention.Following, 0.20, new Actual365Fixed());

        ImmutableArray<CashFlow> Build(bool shared)
        {
            var u1 = new IborCoupon(
                new Date(17, Month.July, 2024), 1e6, new Date(17, Month.January, 2024), new Date(17, Month.July, 2024), 2, index);
            var u2 = new IborCoupon(
                new Date(17, Month.July, 2029), 2e6, new Date(17, Month.January, 2029), new Date(17, Month.July, 2029), 2, index,
                1.5, 0.004);
            ImmutableArray<CashFlow> leg = [new CappedFlooredCoupon(u1, 0.031), new CappedFlooredCoupon(u2, 0.031)];
            var p = new BlackIborCouponPricer(context, vol);
            return shared
                ? CouponPricerFunctions.SetCouponPricer(leg, p)
                : [CouponPricerFunctions.SetCouponPricer([leg[0]], p)[0],
                   CouponPricerFunctions.SetCouponPricer([leg[1]], new BlackIborCouponPricer(context, vol))[0]];
        }

        // Each capped coupon on its own, and with its own pricer, after both underlyings' rates are read.
        var alone = (CappedFlooredCoupon)Build(shared: true)[0];
        double reference = alone.Rate(context);
        ImmutableArray<CashFlow> own = Build(shared: false);
        _ = ((CappedFlooredCoupon)own[0]).Underlying.Rate(context);
        _ = ((CappedFlooredCoupon)own[1]).Underlying.Rate(context);

        // The shared pricer, both underlyings' rates read first.
        ImmutableArray<CashFlow> shared = Build(shared: true);
        var c1 = (CappedFlooredCoupon)shared[0];
        var c2 = (CappedFlooredCoupon)shared[1];
        _ = c1.Underlying.Rate(context);
        _ = c2.Underlying.Rate(context);

        Assert.Same(c1.Pricer, c2.Pricer);
        Assert.Equal(reference, c1.Rate(context));
        Assert.Equal(((CappedFlooredCoupon)own[0]).Rate(context), c1.Rate(context));
        Assert.Equal(0.030228654513094377, c1.Rate(context), 1e-15);
        Assert.True(Math.Abs(c1.Rate(context) - 0.02266598964839617) > 1e-3);
    }
}
