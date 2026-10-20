// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An inflation index's last fixing date after a read of a date the store lacks. QuantLib's
/// <c>hasHistoricalFixing</c> reads the history through the index manager's non-const accessor, which inserts a null
/// entry at the date, and <c>ZeroInflationIndex::lastFixingDate</c> (<c>inflationindex.cpp:190-195</c>) then attributes
/// the last fixing to that entry's period: after probing 1 May 2024, UK RPI's last fixing date moves from 1 April to
/// 1 May 2024, a month with no fixing, measured on the wheel (853 entries for 851 stored). nanoql's reads never write
/// (the fixing store's divergence), so the last fixing date stays 1 April 2024, QuantLib's answer before the read.
/// </summary>
public class InflationLastFixingDateTests
{
    [Fact]
    public void AReadOfAMissingDateDoesntMoveTheLastFixingDate()
    {
        var index = new UKRPI();
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.June, 2024) };
        var d = new Date(15, Month.January, 2022);
        double value = 300.0;
        while (d <= new Date(15, Month.April, 2024))
        {
            context = context with { Fixings = context.Fixings.AddFixing(index, d, value) };
            value = (value * 1.0031) + 0.07;
            d += new Period(1, TimeUnit.Months);
        }

        Assert.Equal(851, index.TimeSeries(context).Dates().Length);
        Assert.Equal(new Date(1, Month.April, 2024), index.LastFixingDate(context));
        Assert.False(index.HasHistoricalFixing(new Date(1, Month.May, 2024), context));

        // QuantLib's answer here is May 1st, 2024
        Assert.Equal(new Date(1, Month.April, 2024), index.LastFixingDate(context));
        Assert.Equal(851, index.TimeSeries(context).Dates().Length);
    }
}
