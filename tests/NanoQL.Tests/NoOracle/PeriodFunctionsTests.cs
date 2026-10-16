// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.NoOracle;

/// <summary>
/// <see cref="PeriodFunctions.Days"/> and <see cref="PeriodFunctions.LongPeriod"/>, which no QuantLib test or wheel
/// binding checks. Expected values are derived from QuantLib v1.44: <c>days(const Period&amp;)</c>,
/// <c>ql/time/period.cpp:333-348</c> (zero for a zero length; days as is; weeks times 7; months and years throw);
/// and <c>operator&lt;&lt;(std::ostream&amp;, const long_period_holder&amp;)</c>, <c>ql/time/period.cpp:407-422</c> (the
/// length, a space, and the unit's name, singular only for a length of exactly 1).
/// </summary>
public class PeriodFunctionsTests
{
    [Theory]
    [InlineData(0, TimeUnit.Months, 0.0)]
    [InlineData(0, TimeUnit.Years, 0.0)]
    [InlineData(5, TimeUnit.Days, 5.0)]
    [InlineData(-3, TimeUnit.Days, -3.0)]
    [InlineData(2, TimeUnit.Weeks, 14.0)]
    public void DaysConvertsDaysAndWeeks(int n, TimeUnit units, double expected)
    {
        Assert.Equal(expected, PeriodFunctions.Days(new Period(n, units)));
    }

    [Theory]
    [InlineData(1, TimeUnit.Months, "cannot convert Months into Days")]
    [InlineData(2, TimeUnit.Years, "cannot convert Years into Days")]
    public void DaysThrowsForMonthsAndYears(int n, TimeUnit units, string message)
    {
        var e = Assert.Throws<NanoQLException>(() => PeriodFunctions.Days(new Period(n, units)));
        Assert.Equal(message, e.Message);
    }

    [Theory]
    [InlineData(1, TimeUnit.Days, "1 day")]
    [InlineData(2, TimeUnit.Days, "2 days")]
    [InlineData(1, TimeUnit.Weeks, "1 week")]
    [InlineData(0, TimeUnit.Weeks, "0 weeks")]
    [InlineData(1, TimeUnit.Months, "1 month")]
    [InlineData(-1, TimeUnit.Months, "-1 months")]
    [InlineData(1, TimeUnit.Years, "1 year")]
    [InlineData(30, TimeUnit.Years, "30 years")]
    public void LongPeriodSpellsTheUnit(int n, TimeUnit units, string expected)
    {
        Assert.Equal(expected, PeriodFunctions.LongPeriod(new Period(n, units)));
    }

    [Fact]
    public void LongPeriodThrowsForAUnitBelowADay()
    {
        Assert.Throws<NanoQLException>(() => PeriodFunctions.LongPeriod(new Period(3, TimeUnit.Hours)));
    }
}
