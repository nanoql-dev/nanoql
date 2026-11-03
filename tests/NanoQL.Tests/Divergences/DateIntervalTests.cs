// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="DateInterval.IsDateBetween"/> with its flags as named (fixed). QuantLib's <c>else if</c> follows the
/// <c>includeFirst</c> test, so a start date that passes <c>date &gt;= startDate</c> then fails <c>date &gt;
/// startDate</c>, and the end likewise (<c>dateinterval.hpp:51-63</c>): on 1 to 31 January 2025 both ends are excluded
/// under all four flag pairs, the middle included. nanoql includes an end exactly when its flag says. Check: every date
/// of the interval and a day either side, under the four pairs.
/// </summary>
public class DateIntervalTests
{
    private static readonly DateInterval January = new(new Date(1, Month.January, 2025), new Date(31, Month.January, 2025));

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void EachEndIsIncludedAsItsFlagSays(bool includeFirst, bool includeLast)
    {
        Assert.Equal(includeFirst, January.IsDateBetween(January.StartDate, includeFirst, includeLast));
        Assert.Equal(includeLast, January.IsDateBetween(January.EndDate, includeFirst, includeLast));
        for (Date d = January.StartDate + 1; d < January.EndDate; d++)
        {
            Assert.True(January.IsDateBetween(d, includeFirst, includeLast));
        }

        Assert.False(January.IsDateBetween(January.StartDate - 1, includeFirst, includeLast));
        Assert.False(January.IsDateBetween(January.EndDate + 1, includeFirst, includeLast));
    }

    [Fact]
    public void TheDefaultFlagsIncludeBothEnds()
    {
        // QuantLib's defaults, (true, true), exclude both
        Assert.True(January.IsDateBetween(January.StartDate));
        Assert.True(January.IsDateBetween(January.EndDate));
    }
}
