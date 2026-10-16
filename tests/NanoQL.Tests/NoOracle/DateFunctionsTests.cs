// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.NoOracle;

/// <summary>
/// <see cref="DateFunctions.ShortDate"/>, which no QuantLib test or wheel binding checks: its expected text is derived
/// from QuantLib's <c>operator&lt;&lt;(std::ostream&amp;, const short_date_holder&amp;)</c>,
/// <c>ql/time/date.cpp:928-944</c> at v1.44: month and day zero-padded to two digits, then the year, separated by
/// slashes, and "null date" for the null date.
/// </summary>
public class DateFunctionsTests
{
    [Theory]
    [InlineData(5, Month.March, 2026, "03/05/2026")]
    [InlineData(31, Month.December, 2199, "12/31/2199")]
    [InlineData(1, Month.January, 1901, "01/01/1901")]
    [InlineData(15, Month.October, 2026, "10/15/2026")]
    public void ShortDateIsMonthDayYear(int d, Month m, int y, string expected)
    {
        Assert.Equal(expected, DateFunctions.ShortDate(new Date(d, m, y)));
    }

    [Fact]
    public void ShortDateOfTheNullDate()
    {
        Assert.Equal("null date", DateFunctions.ShortDate(null));
    }
}
