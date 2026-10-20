// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="InflationCoupon"/> as an extension point: a coupon deriving from it that keeps its index fixing reads the
/// index on the coupon's fixing date, the reference period's end less the observation lag, as QuantLib's
/// <c>InflationCoupon::indexFixing</c> does. The year-on-year and CPI coupons override it with their own observations.
/// On <see cref="InflationFixture"/>'s market, a coupon ending on 1 July 2024 observed three months back fixes on
/// 1 April 2024 at UK RPI's 312.25550633376184, QuantLib's on the wheel.
/// </summary>
public class InflationCouponBaseTests
{
    [Fact]
    public void ADerivedCouponReadsTheIndexOnItsFixingDate()
    {
        var coupon = new PlainInflationCoupon(
            new Date(1, Month.July, 2024),
            1e6,
            new Date(1, Month.July, 2023),
            new Date(1, Month.July, 2024),
            new UKRPI(),
            new Period(3, TimeUnit.Months));

        double fixing = coupon.IndexFixing(InflationFixture.Context());

        Assert.Equal(new Date(1, Month.April, 2024), coupon.FixingDate());
        Assert.True(Tolerance.Within(312.25550633376184, fixing, 300.0), $"{fixing}");
    }

    private sealed class PlainInflationCoupon(
        Date paymentDate, double nominal, Date startDate, Date endDate, InflationIndex index, Period observationLag)
        : InflationCoupon(paymentDate, nominal, startDate, endDate, 0, index, observationLag, new Actual365Fixed())
    {
        protected override bool CheckPricerImpl(InflationCouponPricer? pricer) => true;
    }
}
