// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="CPICouponPricer"/> given a coupon that isn't a CPI coupon. QuantLib's <c>initialize</c> casts the coupon
/// without a check and then reads its fixed rate through the null pointer (<c>cpicouponpricer.cpp:111-112</c>), which
/// is undefined; the wheel can't reach it. nanoql refuses with "CPI coupon needed", after the year-on-year pricer's
/// "year-on-year inflation coupon needed" (<c>inflationcouponpricer.cpp:138</c>).
/// </summary>
public class CpiPricerCouponTypeTests
{
    [Fact]
    public void AYearOnYearCouponIsRefused()
    {
        EvaluationContext context = InflationFixture.Context();
        var index = new YoYInflationIndex(
            new UKRPI(), InflationFixture.YoYCurve([new(1, Month.April, 2024), new(1, Month.April, 2034)], [0.03, 0.03]));
        var coupon = new YoYInflationCoupon(
            new Date(15, Month.July, 2025),
            1e6,
            new Date(15, Month.July, 2024),
            new Date(15, Month.July, 2025),
            0,
            index,
            new Period(3, TimeUnit.Months),
            CPIInterpolationType.Flat,
            new Actual365Fixed());

        ArgumentException e = Assert.Throws<ArgumentException>(() => new CPICouponPricer().SwapletRate(coupon, context));
        Assert.StartsWith("CPI coupon needed", e.Message, StringComparison.Ordinal);
    }
}
