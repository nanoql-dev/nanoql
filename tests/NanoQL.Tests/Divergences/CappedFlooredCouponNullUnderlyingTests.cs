// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A capped coupon over a null underlying. QuantLib's constructor reads the underlying's terms in its base initializer
/// (<c>capflooredcoupon.cpp:30-43</c>), dereferencing the null pointer: a Boost assertion in the default build,
/// undefined under <c>std::shared_ptr</c>. nanoql refuses it with <see cref="ArgumentNullException"/>.
/// </summary>
public class CappedFlooredCouponNullUnderlyingTests
{
    [Fact]
    public void ANullUnderlyingIsRefused()
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => new CappedFlooredCoupon(null!, 0.03));
        Assert.Equal("underlying", e.ParamName);
    }
}
