// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A digital coupon's ex-coupon date and fixing convention: QuantLib's <c>DigitalCoupon</c> builds its floating coupon
/// from twelve of the underlying's terms but not its ex-coupon date or its fixing convention, which take their
/// defaults, no date and Preceding (<c>digitalcoupon.cpp:39-50</c>, <c>floatingratecoupon.hpp:60-61</c>), so a digital
/// coupon on an underlying that goes ex-coupon accrues to its payment as if it didn't, and one on an underlying whose
/// fixing convention isn't Preceding can fix on another date than its underlying. nanoql's keeps both of the
/// underlying's. QuantLib's check: the underlying coupon's ex-coupon date and accrued amounts, which a digital coupon
/// with neither option reproduces (the wheel): a 6-month Euribor coupon from 19 January 2026, paid 20 July 2026 and
/// ex-coupon on 13 July, on the harness vectors' 3.1% curve and 20% lognormal volatility, accrues 14,892.889329889716
/// on 1e6 by 12 July and −599.1392259151036 on 13 July; and the capped coupon on the same underlying, which passes both
/// terms (<c>capflooredcoupon.cpp:30-43</c>): on a coupon from Saturday 18 January 2025 with 0 fixing days and a
/// Modified Following fixing convention, QuantLib's digital coupon fixes on 17 January where its underlying fixes on 20
/// January (the C++ harness).
/// </summary>
public class DigitalCouponExCouponTests
{
    private static readonly Date ExCoupon = new(13, Month.July, 2026);

    [Fact]
    public void TheUnderlyingsExCouponDateIsKept()
    {
        (IborCoupon underlying, FloatingRateCouponPricer pricer, _) = Underlying();
        DigitalCoupon digital = new DigitalCoupon(underlying, 0.05, callDigitalPayoff: 0.01).SetPricer(pricer);
        Assert.Equal(ExCoupon, digital.ExCouponDate);
        Assert.Equal(ExCoupon, digital.Underlying.ExCouponDate);
    }

    [Fact]
    public void TheUnderlyingsFixingConventionIsKept()
    {
        (IborCoupon template, FloatingRateCouponPricer pricer, _) = Underlying();
        var underlying = new IborCoupon(
            new Date(18, Month.July, 2025),
            1e6,
            new Date(18, Month.January, 2025),
            new Date(18, Month.July, 2025),
            0,
            (IborIndex)template.Index,
            fixingConvention: BusinessDayConvention.ModifiedFollowing);
        DigitalCoupon digital = new DigitalCoupon(underlying, 0.05, callDigitalPayoff: 0.01).SetPricer(pricer);
        Assert.Equal(new Date(20, Month.January, 2025), underlying.FixingDate());
        Assert.Equal(underlying.FixingDate(), digital.FixingDate());
        Assert.Equal(new CappedFlooredCoupon(underlying, 0.05).FixingDate(), digital.FixingDate());
    }

    [Fact]
    public void WithNeitherOptionItAccruesAsItsUnderlying()
    {
        (IborCoupon underlying, FloatingRateCouponPricer pricer, EvaluationContext context) = Underlying();
        DigitalCoupon digital = new DigitalCoupon(underlying).SetPricer(pricer);
        Assert.True(Math.Abs(digital.Rate(context) - 0.030812874475633897) <= 1e-15, $"{digital.Rate(context)}");
        foreach ((Date d, double quantLib) in new[]
                 {
                     (new Date(10, Month.July, 2026), 14721.706693913973),
                     (new Date(12, Month.July, 2026), 14892.889329889716),
                     (ExCoupon, -599.1392259151036),
                     (new Date(14, Month.July, 2026), -513.5479079272316),
                     (new Date(19, Month.July, 2026), -85.59131798787193),
                 })
        {
            double accrued = digital.AccruedAmount(d, context);
            Assert.True(Math.Abs(accrued - quantLib) <= 1e-12 * Math.Abs(quantLib), $"{d}: {accrued}");
        }
    }

    // The coupon on the digital Ibor harness vectors' index and pricer, with their evaluation date.
    private static (IborCoupon Coupon, FloatingRateCouponPricer Pricer, EvaluationContext Context) Underlying()
    {
        VectorCase c = VectorFile.Load("cxx/cashflows/digitaliborcoupon").Cases.Single(x => x.Name == "cash calls at 5%");
        EvaluationContext context = c.Context();
        JsonElement leg = c.Input("leg");
        var coupon = new IborCoupon(
            new Date(20, Month.July, 2026),
            1e6,
            new Date(19, Month.January, 2026),
            new Date(20, Month.July, 2026),
            2,
            IndexInputs.IborIndexOf(leg.GetProperty("index")),
            exCouponDate: ExCoupon);
        return (coupon, CouponInputs.PricerOf(leg.GetProperty("pricer"), context)!, context);
    }
}
