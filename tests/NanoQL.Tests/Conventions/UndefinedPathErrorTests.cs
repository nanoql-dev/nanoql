// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of QuantLib's undefined paths in the pilot's members, by type, message and parameter (playbook §2):
/// a value already stored is refused with <see cref="NanoQLException"/>, an argument with
/// <see cref="ArgumentException"/>.
/// </summary>
public class UndefinedPathErrorTests
{
    [Fact]
    public void ThePeriodsRefusals()
    {
        var p = new Period(int.MinValue, TimeUnit.Weeks);
        const string Text = "period length -2147483648 can't be negated";

        Assert.Equal(Text, Assert.Throws<NanoQLException>(() => -p).Message);
        Assert.Equal(Text, Assert.Throws<NanoQLException>(() => p.Frequency()).Message);
    }

    [Fact]
    public void TheFixedRateLegsRefusal()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new FixedRateLeg(new Schedule([], new TARGET())));

        Assert.Equal(typeof(ArgumentException), e.GetType());
        Assert.Equal("schedule", e.ParamName);
        Assert.StartsWith("empty schedule", e.Message, StringComparison.Ordinal);
    }
}
