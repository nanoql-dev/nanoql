// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="PSACurve"/>'s MONTH is SF-6's, "the accrual period during which the age of the mortgage loans increases
/// from MONTH – 1 to MONTH": the whole calendar months from the curve's start, plus one. QLNet counts the 30/360 Bond
/// Basis days from the start over 30, plus one (<c>PSACurve.cs:37-38</c>). From a start on the 29th to the 31st,
/// February's anniversary is under 30 such days after January's, so QLNet repeats the month there and skips the next
/// (from 31 January 2007, MONTH 1 on 28 February and 3 on 31 March; MONTH 13 on 29 February 2008); from a start on
/// the 1st it counts a 31st a month ahead (MONTH 2 on 31 March 2007 from 1 March), and from a 31st the 30th of a 31-day
/// month (MONTH 3 on 30 March 2007). The check is SIFMA's formula (<c>Reference/SifmaPrepaymentTests</c>); QLNet's
/// bits hold wherever the two counts agree (the vectors, which leave out the dates where they don't). QLNet's values
/// are QLNet 1.13.2's.
/// </summary>
public class PsaMonthCountTests
{
    [Theory]
    // QLNet's CPR at 100% PSA: 0.002, 0.026, 0.002, 0.002, 0.004 and 0.006, months 1, 13, 1, 1, 2 and 3.
    [InlineData(2007, 1, 31, 2007, 2, 28, 2)]
    [InlineData(2007, 1, 31, 2008, 2, 29, 14)]
    [InlineData(2007, 1, 29, 2007, 2, 28, 2)]
    [InlineData(2009, 1, 30, 2009, 2, 28, 2)]
    [InlineData(2007, 3, 1, 2007, 3, 31, 1)]
    [InlineData(2007, 1, 31, 2007, 3, 30, 2)]
    public void TheMonthIsTheCalendarMonthsFromTheStart(int y1, int m1, int d1, int y2, int m2, int d2, int month)
    {
        var curve = new PSACurve(new Date(d1, (Month)m1, y1));

        Assert.Equal(0.06 * (month / 30d), curve.GetCPR(new Date(d2, (Month)m2, y2)));
    }

    [Fact]
    public void WhereTheCountsAgreeTheRatesAreQLNets()
    {
        // QLNet from 2007-01-31 at 2007-03-31: CPR 0.006, SMM 0.0005013802940021517.
        var curve = new PSACurve(new Date(31, January, 2007));

        Assert.Equal(0.006, curve.GetCPR(new Date(31, March, 2007)));
        Assert.Equal(0.0005013802940021517, curve.GetSMM(new Date(31, March, 2007)));
    }
}
