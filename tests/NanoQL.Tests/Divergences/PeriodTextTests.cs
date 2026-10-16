// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's short period text throws "unknown time unit" for milliseconds, microseconds and values outside the
/// enum (<c>period.cpp</c>). <see cref="PeriodFunctions.ShortPeriod"/> keeps that throw, while
/// <see cref="Period.ToString"/>, which .NET expects never to throw, falls back to a text of its own.
/// </summary>
public class PeriodTextTests
{
    [Fact]
    public void ToStringNeverThrows()
    {
        Assert.Equal("6M", new Period(6, TimeUnit.Months).ToString());
        Assert.Equal("3h", new Period(3, TimeUnit.Hours).ToString());
        Assert.Equal("1ms", new Period(1, TimeUnit.Milliseconds).ToString());
        Assert.Equal("-2us", new Period(-2, TimeUnit.Microseconds).ToString());
        Assert.Equal("5 (time unit 42)", new Period(5, (TimeUnit)42).ToString());
    }

    [Fact]
    public void ShortPeriodThrowsAsQuantLibDoes()
    {
        Assert.Equal(
            "unknown time unit (7)",
            Assert.Throws<NanoQLException>(() => PeriodFunctions.ShortPeriod(new Period(1, TimeUnit.Milliseconds))).Message);
        Assert.Throws<NanoQLException>(() => PeriodFunctions.ShortPeriod(new Period(1, TimeUnit.Microseconds)));
        Assert.Throws<NanoQLException>(() => PeriodFunctions.ShortPeriod(new Period(5, (TimeUnit)42)));
    }
}
