// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <c>IntervalPrice</c> is an immutable value object: <c>SetValue</c> and <c>SetValues</c> return a copy and leave the
/// receiver unchanged, where QuantLib's change the object; two prices are equal, with equal hashes, when their four
/// values are, and a price type outside the enum is refused with QuantLib's "Unknown price type".
/// </summary>
public class IntervalPriceCopyTests
{
    [Fact]
    public void TheModifiersReturnCopies()
    {
        var p = new IntervalPrice(1, 2, 3, 4);
        IntervalPrice opened = p.SetValue(11, IntervalPriceType.Open);
        IntervalPrice all = p.SetValues(21, 22, 23, 24);
        Assert.NotSame(p, opened);
        Assert.Equal(new IntervalPrice(1, 2, 3, 4), p);
        Assert.Equal(new IntervalPrice(11, 2, 3, 4), opened);
        Assert.Equal(new IntervalPrice(21, 22, 23, 24), all);
    }

    [Fact]
    public void PricesAreEqualByTheirValues()
    {
        var p = new IntervalPrice(1, 2, 3, 4);
        Assert.Equal(new IntervalPrice(1, 2, 3, 4).GetHashCode(), p.GetHashCode());
        Assert.NotEqual(new IntervalPrice(1, 2, 3, 5), p);
        Assert.True(p.Equals((object)new IntervalPrice(1, 2, 3, 4)));
        Assert.False(p.Equals((object)"1 2 3 4"));
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => p.Value((IntervalPriceType)4));
        Assert.Equal("type", e.ParamName);
        Assert.StartsWith("Unknown price type", e.Message, StringComparison.Ordinal);
    }
}
