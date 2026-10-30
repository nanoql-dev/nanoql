// SPDX-FileCopyrightText: Copyright (C) 2021 Anubhav Pandey
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/chile.hpp ql/time/calendars/chile.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Chilean calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Santiago Stock Exchange (data from https://en.wikipedia.org/wiki/Public_holidays_in_Chile):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>January 2nd, when falling on a Monday (since 2017)</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Saturday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Navy Day, May 21st</description></item>
/// <item><description>Day of Aboriginal People, around June 21st (observed on each Winter Solstice) (since
/// 2021)</description></item>
/// <item><description>Saint Peter and Saint Paul, June 29th (moved to the nearest Monday if it falls on a
/// weekday)</description></item>
/// <item><description>Our Lady of Mount Carmel, July 16th</description></item>
/// <item><description>Assumption Day, August 15th</description></item>
/// <item><description>Independence Day, September 18th (also the 17th if the latter falls on a Monday or
/// Friday)</description></item>
/// <item><description>Army Day, September 19th (also the 20th if the latter falls on a Friday)</description></item>
/// <item><description>Discovery of Two Worlds, October 12th (moved to the nearest Monday if it falls on a
/// weekday)</description></item>
/// <item><description>Reformation Day, October 31st (since 2008; moved to the preceding Friday if it falls on a
/// Tuesday, or to the following Friday if it falls on a Wednesday)</description></item>
/// <item><description>All Saints' Day, November 1st</description></item>
/// <item><description>Immaculate Conception, December 8th</description></item>
/// <item><description>Christmas Day, December 25th</description></item>
/// <item><description>New Year's Eve, December 31st; (see
/// https://www.cmfchile.cl/portal/prensa/615/w3-article-49984.html)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Chile : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SseImplInstance = new SseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Chile"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Chile(ChileMarket market = ChileMarket.SSE)
    {
        switch (market)
        {
            case ChileMarket.SSE:
                impl = SseImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Chile.{market}"));
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => impl.IsWeekend(w);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => impl.IsBusinessDay(date);

    // The market-specific rules; QuantLib's Calendar::Impl.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public abstract bool IsWeekend(Weekday w);

        public abstract bool IsBusinessDay(Date date);
    }

    private sealed class SseImpl : Impl
    {
        public override string Name => "Santiago Stock Exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;
            int dd = date.DayOfYear;
            int em = WesternImpl.EasterMonday(y);

            if (WesternImpl.IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                || (d == 2 && m == Month.January && w == Weekday.Monday && y > 2016)
                // Papal visit in 2018
                || (d == 16 && m == Month.January && y == 2018)
                // Good Friday
                || (dd == em - 3)
                // Easter Saturday
                || (dd == em - 2)
                // Census Day in 2017
                || (d == 19 && m == Month.April && y == 2017)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Navy Day
                || (d == 21 && m == Month.May)
                // Day of Aboriginal People
                || ChileHelpers.IsAboriginalPeopleDay(d, m, y)
                // St. Peter and St. Paul
                || (d >= 26 && d <= 29 && m == Month.June && w == Weekday.Monday)
                || (d == 2 && m == Month.July && w == Weekday.Monday)
                // Our Lady of Mount Carmel
                || (d == 16 && m == Month.July)
                // Assumption Day
                || (d == 15 && m == Month.August)
                // Independence Day
                || (d == 16 && m == Month.September && y == 2022)
                || (d == 17 && m == Month.September && ((w == Weekday.Monday && y >= 2007) || (w == Weekday.Friday && y > 2016)))
                || (d == 18 && m == Month.September)
                // Army Day
                || (d == 19 && m == Month.September)
                || (d == 20 && m == Month.September && w == Weekday.Friday && y >= 2007)
                // Discovery of Two Worlds
                || (d >= 9 && d <= 12 && m == Month.October && w == Weekday.Monday)
                || (d == 15 && m == Month.October && w == Weekday.Monday)
                // Reformation Day
                || (((d == 27 && m == Month.October && w == Weekday.Friday)
                     || (d == 31 && m == Month.October && w != Weekday.Tuesday && w != Weekday.Wednesday)
                     || (d == 2 && m == Month.November && w == Weekday.Friday)) && y >= 2008)
                // All Saints' Day
                || (d == 1 && m == Month.November)
                // Immaculate Conception
                || (d == 8 && m == Month.December)
                // Christmas Day
                || (d == 25 && m == Month.December)
                // New Year's Eve
                || (d == 31 && m == Month.December)
                )
            {
                return false;
            }

            return true;
        }
    }
}

// QuantLib's anonymous-namespace helper in chile.cpp.
file static class ChileHelpers
{
    private static readonly byte[] AboriginalPeopleDays =
    [
            21, 21, 21, 20, 20, 21, 21, 20, 20, // 2021-2029
        21, 21, 20, 20, 21, 21, 20, 20, 21, 21, // 2030-2039
        20, 20, 21, 21, 20, 20, 21, 21, 20, 20, // 2040-2049
        20, 21, 20, 20, 20, 21, 20, 20, 20, 21, // 2050-2059
        20, 20, 20, 21, 20, 20, 20, 21, 20, 20, // 2060-2069
        20, 21, 20, 20, 20, 21, 20, 20, 20, 20, // 2070-2079
        20, 20, 20, 20, 20, 20, 20, 20, 20, 20, // 2080-2089
        20, 20, 20, 20, 20, 20, 20, 20, 20, 20, // 2090-2099
        21, 21, 21, 21, 21, 21, 21, 21, 20, 21, // 2100-2109
        21, 21, 20, 21, 21, 21, 20, 21, 21, 21, // 2110-2119
        20, 21, 21, 21, 20, 21, 21, 21, 20, 21, // 2120-2129
        21, 21, 20, 21, 21, 21, 20, 20, 21, 21, // 2130-2139
        20, 20, 21, 21, 20, 20, 21, 21, 20, 20, // 2140-2149
        21, 21, 20, 20, 21, 21, 20, 20, 21, 21, // 2150-2159
        20, 20, 21, 21, 20, 20, 21, 21, 20, 20, // 2160-2169
        20, 21, 20, 20, 20, 21, 20, 20, 20, 21, // 2170-2179
        20, 20, 20, 21, 20, 20, 20, 21, 20, 20, // 2180-2189
        20, 21, 20, 20, 20, 21, 20, 20, 20, 20, // 2190-2199
    ];

    // Celebrated on the Winter Solstice day, except in 2021, when it was the day after.
    public static bool IsAboriginalPeopleDay(int d, Month m, int y) =>
        m == Month.June && y >= 2021 && d == AboriginalPeopleDays[y - 2021];
}
