// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A capped year-on-year coupon's effective strike without its option (found while porting the S-cms plan's C3):
/// QuantLib's <c>CappedFlooredYoYInflationCoupon::effectiveCap</c> returns (C − b) / a whether or not the fixing is
/// capped (<c>capflooredinflationcoupon.cpp:126-128</c>), and its constructor sets C only for a cap it was given
/// (<c>:27-49</c>, the member declared at <c>.hpp:135</c>), so the effective cap of a coupon with no cap reads a member
/// nothing set, undefined behaviour; its <c>effectiveFloor</c> likewise. On the wheel, the vectors' at-the-money
/// coupons gave such values as 1.69e-306 and 1.43e-311, and 0.029 for the effective floor of the coupon with neither
/// option, the floor of a coupon built before it. nanoql's is null, as QuantLib's <c>CappedFlooredCoupon</c>'s
/// <c>Null&lt;Rate&gt;()</c> on the same members (<c>capflooredcoupon.cpp:123-135</c>). QuantLib's check: its effective
/// strikes where the coupon has the option (the vectors), and its capped Ibor coupon's null where it hasn't.
/// </summary>
public class CappedYoYEffectiveStrikeTests
{
    [Fact]
    public void AnEffectiveStrikeIsNullWithoutItsOption()
    {
        var index = new YoYInflationIndex(new UKRPI());
        var capped = Coupon(index, 0.031, null);
        var floored = Coupon(index, null, 0.029);
        var plain = Coupon(index, null, null);
        var collared = Coupon(index, 0.031, 0.029);

        Assert.Equal(0.031, capped.EffectiveCap);
        Assert.Null(capped.EffectiveFloor);
        Assert.Null(floored.EffectiveCap);
        Assert.Equal(0.029, floored.EffectiveFloor);
        Assert.Null(plain.EffectiveCap);
        Assert.Null(plain.EffectiveFloor);
        Assert.Equal(0.031, collared.EffectiveCap);
        Assert.Equal(0.029, collared.EffectiveFloor);

        // QuantLib's capped Ibor coupon: Null<Rate>() without the option
        var curve = new FlatForward(new Date(15, January, 2025), 0.03, new Actual365Fixed());
        var ibor = new IborCoupon(
            new Date(15, July, 2025), 1e6, new Date(15, January, 2025), new Date(15, July, 2025), 2, new Euribor6M(curve));
        Assert.Null(new CappedFlooredCoupon(ibor, cap: 0.04).EffectiveFloor);
        Assert.Null(new CappedFlooredCoupon(ibor, floor: 0.01).EffectiveCap);
    }

    private static CappedFlooredYoYInflationCoupon Coupon(YoYInflationIndex index, double? cap, double? floor) => new(
        new Date(17, June, 2025),
        1e6,
        new Date(15, June, 2024),
        new Date(15, June, 2025),
        0,
        index,
        new Period(2, TimeUnit.Months),
        CPIInterpolationType.Flat,
        new Actual365Fixed(),
        1.0,
        0.0,
        cap,
        floor);
}
