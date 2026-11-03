// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A compounded overnight coupon's spread compounded daily over the forecast range (fixed). Where the telescopic
/// formula applies, QuantLib's compounding pricer multiplies both compound factors, with and without the spread, by
/// the same discount ratio (<c>overnightindexedcouponpricer.cpp:191-195</c>), so the spread is dropped: a SOFR coupon
/// with a 1.23456% spread on a flat 3.12345% curve forecasts 0.030928216305566813, the no-spread rate, while the same
/// coupon with the same fixings published compounds the spread, 0.043388368686242844 (−124.6bp). nanoql compounds the
/// spread day by day on the forecast fixings, as QuantLib does outside the range and once the fixings are published; a
/// spread added rather than compounded, a zero spread and a lookback that leaves the formula aside keep QuantLib's
/// bits. QuantLib's check: its own published route (QuantLib-Python 1.44).
/// </summary>
public class OvernightCompoundedSpreadForecastTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.0312345, new Actual365Fixed());

    [Fact]
    public void ADailyCompoundedSpreadIsCompoundedOverTheForecastRange()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        OvernightIndexedCoupon coupon = Coupon(0.0123456, true);

        // QuantLib: 0.030928216305566813, effective spread 0
        Assert.Equal(0.043388368686242844, coupon.Rate(context), 1e-16);

        // the index fixing QuantLib's own forecast without the spread, its telescopic formula, which QuantLib's
        // published route, compounding day by day, gives as 0.030928216305565942: the effective spread is QuantLib's
        // published 0.012460152380676902 within that gap
        Assert.Equal(0.030928216305566813, coupon.EffectiveIndexFixing(context));
        Assert.Equal(0.012460152380676902, coupon.EffectiveSpread(context), 1e-15);
    }

    [Fact]
    public void TheOtherSpreadsKeepQuantLibsBits()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Assert.Equal(0.043273816305566815, Coupon(0.0123456, false).Rate(context));
        Assert.Equal(0.030928216305566813, Coupon(0.0, true).Rate(context));
        Assert.Equal(0.043387412969911554, Coupon(0.0123456, true, lookbackDays: 2).Rate(context));
    }

    // A SOFR coupon from 19 October 2026 to 19 January 2027 on 1,000,000, priced by QuantLib's compounding pricer.
    private static OvernightIndexedCoupon Coupon(double spread, bool compoundSpreadDaily, int? lookbackDays = null)
    {
        var end = new Date(19, January, 2027);
        var coupon = new OvernightIndexedCoupon(
            end,
            1e6,
            new Date(19, October, 2026),
            end,
            new Sofr(Curve),
            spread: spread,
            lookbackDays: lookbackDays,
            compoundSpreadDaily: compoundSpreadDaily);
        return (OvernightIndexedCoupon)CouponPricerFunctions.SetCouponPricer(
            [coupon], new CompoundingOvernightIndexedCouponPricer())[0];
    }
}
