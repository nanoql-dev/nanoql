// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A year-on-year pricer with no volatility. QuantLib's <c>optionletRate</c> reads
/// <c>capletVolatility()-&gt;baseDate()</c> before anything else (<c>inflationcouponpricer.cpp:99</c>), so its "missing
/// optionlet volatility" check (<c>:112</c>) is unreachable and an empty handle throws "empty Handle cannot be
/// dereferenced" on the wheel, for a fixed optionlet too (QuantLib's defect, R8). nanoql checks the volatility first,
/// refusing every optionlet with QuantLib's own "missing optionlet volatility".
/// </summary>
public class YoYPricerMissingVolatilityTests
{
    [Fact]
    public void TheMissingVolatilityIsNamed()
    {
        EvaluationContext context = InflationFixture.Context();
        var index = new YoYInflationIndex(
            new UKRPI(), InflationFixture.YoYCurve([new(1, Month.April, 2024), new(1, Month.April, 2034)], [0.03, 0.03]));
        var pricer = new BlackYoYInflationCouponPricer(null, InflationFixture.Nominal());
        foreach ((Date start, Date end) in new[]
        {
            (new Date(1, Month.July, 2023), new Date(1, Month.July, 2024)),
            (new Date(15, Month.July, 2024), new Date(15, Month.July, 2025)),
        })
        {
            var coupon = new YoYInflationCoupon(
                end, 1e6, start, end, 0, index, new Period(3, TimeUnit.Months), CPIInterpolationType.Flat, new Actual365Fixed());

            // QuantLib's: "empty Handle cannot be dereferenced"
            NanoQLException e = Assert.Throws<NanoQLException>(() => pricer.CapletRate(coupon, 0.032, context));
            Assert.Equal("missing optionlet volatility", e.Message);
        }
    }
}
