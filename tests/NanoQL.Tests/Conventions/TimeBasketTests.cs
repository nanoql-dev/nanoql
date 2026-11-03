// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="TimeBasket"/>: QuantLib's basket is a map its <c>+=</c>, <c>-=</c> and indexer change in place, the
/// indexer inserting a zero for a date it doesn't hold (<c>timebasket.hpp</c>, its <c>std::map</c> base); nanoql's is
/// immutable: the operators and <see cref="TimeBasket.Rebin"/> return new baskets, and the indexer reads 0 for an
/// absent date, QuantLib's value, without adding the date.
/// </summary>
public class TimeBasketTests
{
    private static readonly Date[] Dates = [new(1, February, 2025), new(15, March, 2025), new(1, September, 2025)];

    [Fact]
    public void TheOperatorsAndRebinLeaveTheirOperands()
    {
        var first = new TimeBasket(Dates, [100.0, 50.0, -20.0]);
        var second = new TimeBasket([Dates[1], new Date(1, December, 2025)], [25.0, 7.5]);
        KeyValuePair<Date, double>[] before = [.. first], otherBefore = [.. second];

        TimeBasket sum = first + second, difference = first - second;
        TimeBasket rebinned = first.Rebin([new Date(1, January, 2025), new Date(1, June, 2025)]);

        Assert.Equal(before, [.. first]);
        Assert.Equal(otherBefore, [.. second]);
        Assert.Equal(75.0, sum[Dates[1]]);
        Assert.Equal(25.0, difference[Dates[1]]);
        Assert.Equal(4, sum.Count);
        Assert.Equal(2, rebinned.Count);
        Assert.Equal(3, first.Count);
    }

    [Fact]
    public void AnAbsentDateReadsZeroAndStaysAbsent()
    {
        var basket = new TimeBasket(Dates, [100.0, 50.0, -20.0]);
        var absent = new Date(2, February, 2025);

        Assert.Equal(0.0, basket[absent]);
        Assert.False(basket.HasDate(absent));
        Assert.Equal(3, basket.Count);
        Assert.DoesNotContain(basket, x => x.Key == absent);
    }
}
