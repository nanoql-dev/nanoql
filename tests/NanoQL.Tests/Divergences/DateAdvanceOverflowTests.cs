// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Advancing a date by a length whose product overflows <see cref="int"/>: QuantLib computes <c>7*n</c> weeks,
/// <c>month + n</c> months and <c>year + n</c> years in <c>Integer</c> (<c>date.cpp:172-201</c>), which overflows for
/// lengths near the type's range, undefined. nanoql computes in <see cref="long"/> and refuses the out-of-range result
/// with its range messages, so no change was needed; this test pins it.
/// </summary>
public class DateAdvanceOverflowTests
{
    [Fact]
    public void AnOverflowingAdvanceIsRefusedByTheDateRange()
    {
        var d = new Date(15, Month.January, 2024);

        Assert.Throws<ArgumentOutOfRangeException>(() => d + new Period(int.MaxValue, TimeUnit.Weeks));
        Assert.Throws<ArgumentOutOfRangeException>(() => d + new Period(int.MinValue, TimeUnit.Weeks));
        Assert.Throws<ArgumentOutOfRangeException>(() => d + new Period(int.MaxValue, TimeUnit.Days));
        Assert.Contains("out of bounds", Assert.Throws<NanoQLException>(() => d + new Period(int.MaxValue, TimeUnit.Months)).Message, StringComparison.Ordinal);
        Assert.Contains("out of bounds", Assert.Throws<NanoQLException>(() => d + new Period(int.MinValue, TimeUnit.Months)).Message, StringComparison.Ordinal);
        Assert.Contains("out of bounds", Assert.Throws<NanoQLException>(() => d + new Period(int.MaxValue, TimeUnit.Years)).Message, StringComparison.Ordinal);
    }
}
