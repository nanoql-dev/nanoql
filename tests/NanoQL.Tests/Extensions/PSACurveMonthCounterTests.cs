// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// <see cref="PSACurve"/>'s month counter, SF-6's MONTH written out from the day, month and year, against an
/// independent count: the largest k whose k-month anniversary of the start, by <see cref="Date"/>'s arithmetic, is on
/// or before the date, plus one, and 1 before the start. The month is read through the CPR at 100% PSA, over starts on
/// every day of four years and ends from 400 days before each to 960 after, every 29th to 31st and February end among
/// them.
/// </summary>
public class PSACurveMonthCounterTests
{
    [Fact]
    public void TheMonthIsTheWholeCalendarMonthsFromTheStartPlusOne()
    {
        int compared = 0;
        for (Date start = new(1, Month.January, 2007); start < new Date(1, Month.January, 2011); start++)
        {
            var curve = new PSACurve(start);
            var anniversaries = Enumerable.Range(-14, 47)
                .Select(k => (k, start + new Period(k, TimeUnit.Months))).ToList();
            for (int offset = -400; offset <= 960; offset += start.DayOfMonth % 3 == 0 ? 1 : 7)
            {
                Date end = start + offset;
                int k = anniversaries.Last(a => a.Item2 <= end).k;
                int month = Math.Max(1, k + 1);
                double expected = month <= 30 ? 0.06 * (month / 30d) : 0.06;
                Assert.Equal(expected, curve.GetCPR(end));
                compared++;
            }
        }

        Assert.True(compared > 500_000, $"{compared} pairs");
    }
}
