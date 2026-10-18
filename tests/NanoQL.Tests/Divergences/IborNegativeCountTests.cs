// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Ibor coupons' fixing days are QuantLib's <c>Natural</c>, an unsigned type, which can't hold a negative value;
/// nanoql takes an <see cref="int"/> (or <see cref="Nullable{T}"/> for QuantLib's <c>Null&lt;Natural&gt;()</c>) and
/// refuses a negative one on entry. From C++ a negative wraps to a large unsigned value, which QuantLib negates back
/// where it advances by it (<c>floatingratecoupon.cpp:84-85</c>, <c>couponpricer.cpp:71-72</c>,
/// <c>stubiborcoupon.cpp:214-215</c>): the wheel's coupon with -1 fixing days looks a day forward for its fixing, and the
/// wrap of 2^31 negates <c>INT_MIN</c>, which is undefined.
/// </summary>
public class IborNegativeCountTests
{
    [Fact]
    public void NegativeFixingDaysAreRefused()
    {
        var index = new Euribor6M();
        var start = new Date(17, Month.July, 2025);
        var end = new Date(19, Month.January, 2026);
        var schedule = new Schedule([start, end], new TARGET());
        var selection = new StubIndexSelection(StubIndexSelectionConvention.ClosestIndex, [new Euribor3M(), index]);

        AssertRefused(() => new FloatingRateCoupon(end, 1e6, start, end, -1, index));
        AssertRefused(() => new IborCoupon(end, 1e6, start, end, -1, index));
        AssertRefused(() => new CappedFlooredIborCoupon(end, 1e6, start, end, -1, index, cap: 0.05));
        AssertRefused(() => new StubIborCoupon(end, 1e6, start, end, -1, selection));
        AssertRefused(() => new IborLeg(schedule, index).WithFixingDays(-1));
        AssertRefused(() => new IborLeg(schedule, index).WithFixingDays([2, null, -1]));
        AssertRefused(() => CashFlowVectorsFunctions.FloatingLeg<IborIndex>(
            (pd, n, s, e, fd, i, g, sp, rs, re, dc, ia, ex, fc) => new IborCoupon(pd, n, s, e, fd, i, g, sp, rs, re, dc, ia, ex, fc),
            (pd, n, s, e, fd, i, g, sp, cap, floor, rs, re, dc, ia, ex, fc) =>
                new CappedFlooredIborCoupon(pd, n, s, e, fd, i, g, sp, cap, floor, rs, re, dc, ia, ex, fc),
            schedule,
            [1e6],
            index,
            null,
            BusinessDayConvention.Following,
            [-1],
            [],
            [],
            [],
            [],
            false,
            false));

        // Zero and null stay QuantLib's.
        Assert.Equal(0, new IborCoupon(end, 1e6, start, end, 0, index).FixingDays);
        Assert.Equal(2, new IborCoupon(end, 1e6, start, end, null, index).FixingDays);
    }

    private static void AssertRefused(Func<object> create) =>
        Assert.Equal("fixingDays", Assert.Throws<ArgumentOutOfRangeException>(create).ParamName);
}
