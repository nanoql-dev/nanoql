// SPDX-FileCopyrightText: Copyright (C) 2023 Paul Xi Cao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/prices.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class PricesTests
{
    [Fact]
    public void TestMidEquivalent()
    {
        // Testing midEquivalent()...
        Assert.Equal(1.5, PricesFunctions.MidEquivalent(1, 2, 3, 4), 1e-14);
        Assert.Equal(1.5, PricesFunctions.MidEquivalent(1, 2, 0, 4), 1e-14);
        Assert.Equal(1.5, PricesFunctions.MidEquivalent(1, 2, 3, 0), 1e-14);
        Assert.Equal(1.5, PricesFunctions.MidEquivalent(1, 2, 0, 0), 1e-14);

        Assert.Equal(1, PricesFunctions.MidEquivalent(1, 0, 3, 4));
        Assert.Equal(1, PricesFunctions.MidEquivalent(1, 0, 0, 4));
        Assert.Equal(1, PricesFunctions.MidEquivalent(1, 0, 3, 0));
        Assert.Equal(1, PricesFunctions.MidEquivalent(1, 0, 0, 0));

        Assert.Equal(2, PricesFunctions.MidEquivalent(0, 2, 3, 4));
        Assert.Equal(2, PricesFunctions.MidEquivalent(0, 2, 0, 4));
        Assert.Equal(2, PricesFunctions.MidEquivalent(0, 2, 3, 0));
        Assert.Equal(2, PricesFunctions.MidEquivalent(0, 2, 0, 0));

        Assert.Equal(3, PricesFunctions.MidEquivalent(0, 0, 3, 4));
        Assert.Equal(4, PricesFunctions.MidEquivalent(0, 0, 0, 4));
        Assert.Equal(3, PricesFunctions.MidEquivalent(0, 0, 3, 0));
        Assert.Throws<NanoQLException>(() => PricesFunctions.MidEquivalent(0, 0, 0, 0));
    }

    [Fact]
    public void TestMidSafe()
    {
        // Testing midSafe()...
        Assert.Equal(1.5, PricesFunctions.MidSafe(1, 2), 1e-14);

        Assert.Throws<NanoQLException>(() => PricesFunctions.MidSafe(0, 0));
        Assert.Throws<NanoQLException>(() => PricesFunctions.MidSafe(1, 0));
        Assert.Throws<NanoQLException>(() => PricesFunctions.MidSafe(0, 2));
    }

    [Fact]
    public void TestIntervalPriceInspectors()
    {
        // Testing IntervalPrice inspectors...
        var p = new IntervalPrice(1, 2, 3, 4);

        Assert.Equal(1, p.Open);
        Assert.Equal(1, p.Value(IntervalPriceType.Open));
        Assert.Equal(2, p.Close);
        Assert.Equal(2, p.Value(IntervalPriceType.Close));
        Assert.Equal(3, p.High);
        Assert.Equal(3, p.Value(IntervalPriceType.High));
        Assert.Equal(4, p.Low);
        Assert.Equal(4, p.Value(IntervalPriceType.Low));
    }

    // Adapted: nanoql's IntervalPrice is immutable, so each modifier's copy replaces p, as QuantLib's change p.
    [Fact]
    public void TestIntervalPriceModifiers()
    {
        // Testing IntervalPrice modifiers...
        var p = new IntervalPrice(1, 2, 3, 4);

        p = p.SetValue(11, IntervalPriceType.Open);
        TestEquality(p, new IntervalPrice(11, 2, 3, 4));

        p = p.SetValue(12, IntervalPriceType.Close);
        TestEquality(p, new IntervalPrice(11, 12, 3, 4));

        p = p.SetValue(13, IntervalPriceType.High);
        TestEquality(p, new IntervalPrice(11, 12, 13, 4));

        p = p.SetValue(14, IntervalPriceType.Low);
        TestEquality(p, new IntervalPrice(11, 12, 13, 14));

        p = p.SetValues(21, 22, 23, 24);
        TestEquality(p, new IntervalPrice(21, 22, 23, 24));
    }

    [Fact]
    public void TestIntervalPriceMakeSeries()
    {
        // Testing creation of IntervalPrice series...
        TimeSeries<IntervalPrice> priceSeries = CreateSeries();

        Assert.Equal(3, priceSeries.Size);
        Assert.True(priceSeries.TryGetValue(new Date(1, January, 2001), out IntervalPrice? p1));
        TestEquality(p1!, new IntervalPrice(11, 21, 31, 41));
        Assert.True(priceSeries.TryGetValue(new Date(2, February, 2002), out IntervalPrice? p2));
        TestEquality(p2!, new IntervalPrice(12, 22, 32, 42));
        Assert.True(priceSeries.TryGetValue(new Date(3, March, 2003), out IntervalPrice? p3));
        TestEquality(p3!, new IntervalPrice(13, 23, 33, 43));
    }

    [Fact]
    public void TestIntervalPriceExtractComponent()
    {
        // Testing extraction of IntervalPrice values...
        TimeSeries<Real> openSeries = IntervalPrice.ExtractComponent(CreateSeries(), IntervalPriceType.Open);
        TimeSeries<Real> closeSeries = IntervalPrice.ExtractComponent(CreateSeries(), IntervalPriceType.Close);
        TimeSeries<Real> highSeries = IntervalPrice.ExtractComponent(CreateSeries(), IntervalPriceType.High);
        TimeSeries<Real> lowSeries = IntervalPrice.ExtractComponent(CreateSeries(), IntervalPriceType.Low);

        foreach (TimeSeries<Real> series in (TimeSeries<Real>[])[openSeries, closeSeries, highSeries, lowSeries])
        {
            Assert.Equal(3, series.Size);
        }

        Date[] expectedDates = [new(1, January, 2001), new(2, February, 2002), new(3, March, 2003)];
        IntervalPrice[] expectedPrices = [new(11, 21, 31, 41), new(12, 22, 32, 42), new(13, 23, 33, 43)];

        KeyValuePair<Date, Real>[] open = [.. openSeries], close = [.. closeSeries], high = [.. highSeries], low = [.. lowSeries];
        for (int i = 0; i < open.Length; ++i)
        {
            foreach (Date date in (Date[])[open[i].Key, close[i].Key, high[i].Key, low[i].Key])
            {
                Assert.Equal(expectedDates[i], date);
            }

            TestEquality(expectedPrices[i], new IntervalPrice(open[i].Value, close[i].Value, high[i].Value, low[i].Value));
        }
    }

    private static void TestEquality(IntervalPrice lhs, IntervalPrice rhs)
    {
        foreach (IntervalPriceType t in (IntervalPriceType[])[IntervalPriceType.Open, IntervalPriceType.Close, IntervalPriceType.High, IntervalPriceType.Low])
        {
            Assert.Equal(lhs.Value(t), rhs.Value(t));
        }
    }

    private static TimeSeries<IntervalPrice> CreateSeries()
    {
        Date[] d = [new(1, January, 2001), new(3, March, 2003), new(2, February, 2002)];
        Real[] open = [11, 13, 12];
        Real[] close = [21, 23, 22];
        Real[] high = [31, 33, 32];
        Real[] low = [41, 43, 42];
        return IntervalPrice.MakeSeries(d, open, close, high, low);
    }
}
