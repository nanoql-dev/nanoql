// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of two refusals the period vectors record only as a throw, which accepts either an
/// <see cref="ArgumentException"/> or a <see cref="NanoQLException"/>; both keep QuantLib's <c>QL_FAIL</c> as a
/// <see cref="NanoQLException"/> (playbook §2):
/// <list type="bullet">
/// <item><see cref="PeriodFunctions.Days"/> of a month, year or intraday count (<c>ql/time/period.cpp:333-348</c>):
/// the unit is refused only together with another argument, the length, since a zero length gives 0.</item>
/// <item><see cref="PeriodFunctions.LongPeriod"/> of a unit below a day (<c>ql/time/period.cpp:407-422</c>): the unit
/// is a field of its <see cref="Period"/> argument, not an enum argument.</item>
/// </list>
/// </summary>
public class PeriodFunctionsErrorTests
{
    [Theory]
    [InlineData(1, TimeUnit.Months, "cannot convert Months into Days")]
    [InlineData(2, TimeUnit.Years, "cannot convert Years into Days")]
    [InlineData(3, TimeUnit.Hours, "unknown time unit (4)")]
    public void DaysThrowsForAUnitItCannotConvert(int n, TimeUnit units, string message)
    {
        var e = Assert.Throws<NanoQLException>(() => PeriodFunctions.Days(new Period(n, units)));
        Assert.Equal(message, e.Message);
    }

    [Fact]
    public void LongPeriodThrowsForAUnitBelowADay()
    {
        var e = Assert.Throws<NanoQLException>(() => PeriodFunctions.LongPeriod(new Period(3, TimeUnit.Hours)));
        Assert.Equal("unknown time unit (4)", e.Message);
    }
}
