// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// France's Ascension and Whit Monday. QuantLib 1.44 holds them on May 10th and May 21st (<c>france.cpp:60-63</c>),
/// right only when Easter Monday is April 2nd; QuantLib's master moves them with Easter, Easter Monday plus 38 and 49
/// days (#2893, <c>16414224f</c>), with five new test lines, which nanoql takes ahead of the pin, as a fix. The lines
/// below are upstream's; QuantLib 1.44's answers are recorded beside them.
/// </summary>
public class FranceMoveableFeastTests
{
    [Fact]
    public void AscensionAndWhitMondayFollowEaster()
    {
        Calendar settlement = new France(FranceMarket.Settlement);

        // QuantLib's master: Ascension and Whit Monday holidays (1.44: business days)
        Assert.True(settlement.IsHoliday(new Date(9, Month.May, 2024)));
        Assert.True(settlement.IsHoliday(new Date(20, Month.May, 2024)));
        Assert.True(settlement.IsHoliday(new Date(29, Month.May, 2025)));
        Assert.True(settlement.IsHoliday(new Date(9, Month.June, 2025)));

        // QuantLib's master: May 10th and 21st business days (1.44: holidays)
        Assert.True(settlement.IsBusinessDay(new Date(10, Month.May, 2024)));
        Assert.True(settlement.IsBusinessDay(new Date(21, Month.May, 2024)));

        // the year 1.44's dates fit: Easter Monday April 2nd, 2018
        Assert.True(settlement.IsHoliday(new Date(10, Month.May, 2018)));
        Assert.True(settlement.IsHoliday(new Date(21, Month.May, 2018)));
    }
}
