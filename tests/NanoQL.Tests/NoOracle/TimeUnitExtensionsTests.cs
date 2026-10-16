// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.NoOracle;

/// <summary>
/// <c>TimeUnit * int</c>, which no ported QuantLib test calls and the wheel can't check, since its time units are
/// plain integers. Expected values are derived from QuantLib v1.44: <c>operator*(TimeUnit units, T n)</c>,
/// <c>ql/time/period.hpp:151-154</c>, returns <c>Period(n, units)</c>, as <c>operator*(T n, TimeUnit units)</c> at
/// lines 146-149 does.
/// </summary>
public class TimeUnitExtensionsTests
{
    [Theory]
    [InlineData(TimeUnit.Days, 3)]
    [InlineData(TimeUnit.Weeks, -2)]
    [InlineData(TimeUnit.Months, 0)]
    [InlineData(TimeUnit.Years, 30)]
    public void TimeUnitTimesAnIntegerIsThePeriod(TimeUnit units, int n)
    {
        Period p = units * n;

        Assert.Equal(n, p.Length);
        Assert.Equal(units, p.Units);
        Assert.Equal(n * units, p);
    }
}
