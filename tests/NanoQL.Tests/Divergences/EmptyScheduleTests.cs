// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A leg builder given an empty schedule. QuantLib's conversion to a leg reserves <c>schedule_.size()-1</c> coupons, a
/// <c>Size</c> wrapping below zero, and reads <c>schedule_.date(0)</c> past the end of the schedule's dates
/// (<c>fixedratecoupon.cpp:182-185</c>), which is undefined. nanoql refuses the schedule when the builder is made, with
/// <see cref="ArgumentException"/>, "empty schedule", where the pilot's <see cref="FixedRateLeg"/> threw
/// <see cref="ArgumentOutOfRangeException"/> from the read.
/// </summary>
public class EmptyScheduleTests
{
    [Fact]
    public void AFixedRateLegRefusesAnEmptySchedule()
    {
        var empty = new Schedule([], new TARGET());

        ArgumentException e = Assert.Throws<ArgumentException>(() => new FixedRateLeg(empty));
        Assert.Equal("schedule", e.ParamName);
        Assert.IsNotType<ArgumentOutOfRangeException>(e);
    }
}
