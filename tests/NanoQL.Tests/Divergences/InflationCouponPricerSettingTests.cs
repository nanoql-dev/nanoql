// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Setting an inflation pricer on a leg. QuantLib's <c>setCouponPricer</c> for inflation pricers casts each cash flow
/// to an inflation coupon and sets the pricer on the coupons in place, one after another
/// (<c>inflationcouponpricer.cpp:27-34</c>): a null cash flow casts to null and is skipped, and a coupon refusing the
/// pricer, "pricer given is wrong type", leaves the earlier ones holding it. Both answers are defined. The wheel shows
/// the in-place change: given a Black year-on-year pricer and a leg of a year-on-year coupon then a CPI coupon, it
/// refuses "pricer given is wrong type", and the year-on-year coupon, which refused "pricer not set" before, then
/// prices at 0.03 on <see cref="InflationFixture"/>'s flat curve. The null skip alone is off the wheel, which refuses
/// <c>None</c> in a <c>Leg</c>. nanoql returns a new leg, all or nothing, as its floating-rate setter does, and refuses
/// a null cash flow, as every nanoql leg does, with <see cref="ArgumentException"/>, "null cash flow in leg", naming
/// the leg.
/// </summary>
public class InflationCouponPricerSettingTests
{
    [Fact]
    public void ANullCashFlowIsRefused()
    {
        ImmutableArray<CashFlow> leg = [YoYCoupon(), null!];

        Assert.Throws<ArgumentException>(() => InflationCouponPricerFunctions.SetCouponPricer(leg, new YoYInflationCouponPricer()));
    }

    [Fact]
    public void ACouponRefusingThePricerLeavesNoNewLeg()
    {
        // QuantLib sets the pricer on the year-on-year coupon, then refuses at the CPI coupon.
        YoYInflationCoupon yoy = YoYCoupon();
        CPICoupon cpi = new(
            305.0,
            new Date(15, Month.July, 2025),
            1e6,
            new Date(15, Month.July, 2024),
            new Date(15, Month.July, 2025),
            new UKRPI(InflationFixture.ZeroCurve()),
            new Period(3, TimeUnit.Months),
            CPIInterpolationType.Flat,
            new Actual365Fixed(),
            0.02);
        ImmutableArray<CashFlow> leg = [yoy, cpi];

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => InflationCouponPricerFunctions.SetCouponPricer(leg, new YoYInflationCouponPricer()));
        Assert.StartsWith("pricer given is wrong type", e.Message, StringComparison.Ordinal);
        Assert.Null(yoy.Pricer);
        Assert.Null(((YoYInflationCoupon)leg[0]).Pricer);
    }

    private static YoYInflationCoupon YoYCoupon() =>
        new(
            new Date(15, Month.July, 2025),
            1e6,
            new Date(15, Month.July, 2024),
            new Date(15, Month.July, 2025),
            0,
            new YoYInflationIndex(
                new UKRPI(),
                InflationFixture.YoYCurve([new(1, Month.April, 2024), new(1, Month.April, 2034)], [0.03, 0.03])),
            new Period(3, TimeUnit.Months),
            CPIInterpolationType.Flat,
            new Actual365Fixed());
}
