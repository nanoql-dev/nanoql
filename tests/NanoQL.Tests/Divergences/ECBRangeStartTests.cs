// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// ECB's functions at the start of Date's range. QuantLib's <c>isECBdate(d)</c> is <c>nextDate(d - 1) == d</c>, and a
/// month's date is <c>nextDate(Date(1, m, y) - 1)</c> (<c>ecb.hpp:42</c>, <c>:81-84</c>; <c>ecb.cpp:200</c>), so on 1
/// January 1901 both step out of the range and throw "Date's serial number (366) outside allowed range". nanoql reads
/// the first known date on or after the month's first day, and membership in the known dates up to the last one,
/// which are QuantLib's answers on every other date; QuantLib's own <c>nextDate</c> for the following month is the
/// check.
/// </summary>
public class ECBRangeStartTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.October, 2026) };

    [Fact]
    public void IsECBdateAnswersOnDatesFirstDay()
    {
        ECB ecb = ECB.Default;

        // QuantLib: throws "Date's serial number (366) outside allowed range [367-109574], i.e. [January 1st,
        // 1901-December 31st, 2199]"; on the next day, false
        Assert.False(ecb.IsECBdate(Date.MinDate));
        Assert.False(ecb.IsECBdate(Date.MinDate + 1));

        // a known date added on Date's first day is one
        Assert.True(ecb.AddDate(Date.MinDate).IsECBdate(Date.MinDate));
        Assert.Equal(Date.MinDate, ecb.AddDate(Date.MinDate).Date(Month.January, 1901));
    }

    [Fact]
    public void AMonthsDateIsFoundFromDatesFirstDay()
    {
        ECB ecb = ECB.Default;
        Date first = new(19, Month.January, 2005);

        // QuantLib: date(January, 1901) and date("JAN01", January 1st, 1950) throw as above; date(February, 1901) and
        // date("FEB01", January 1st, 1950) are January 19th, 2005, its first known date
        Assert.Equal(first, ecb.Date(Month.January, 1901));
        Assert.Equal(first, ecb.Date("JAN01", Context, new Date(1, Month.January, 1950)));
        Assert.Equal(ecb.Date(Month.February, 1901), ecb.Date(Month.January, 1901));
        Assert.Equal(ecb.Date("FEB01", Context, new Date(1, Month.January, 1950)), ecb.Date("JAN01", Context, new Date(1, Month.January, 1950)));

        // QuantLib's own: JAN00 in the 1900s is before the range, nextDate(minDate)
        Assert.Equal(first, ecb.Date("JAN00", Context, new Date(1, Month.January, 1950)));
    }
}
