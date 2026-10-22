// SPDX-FileCopyrightText: Copyright (C) 2004 FIMAT Group
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008, 2009, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2023 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008, 2009, 2010, 2011 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/taiwan.hpp ql/time/calendars/taiwan.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Taiwanese calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Taiwan stock exchange (data from https://www.twse.com.tw/en/trading/holiday.html):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Peace Memorial Day, February 28</description></item>
/// <item><description>Labor Day, May 1st</description></item>
/// <item><description>Double Tenth National Day, October 10th</description></item>
/// </list>
/// </para>
/// <para>
/// Other holidays for which no rule is given (data available for 2002-2026 only:)
/// <list type="bullet">
/// <item><description>Chinese Lunar New Year</description></item>
/// <item><description>Tomb Sweeping Day</description></item>
/// <item><description>Dragon Boat Festival</description></item>
/// <item><description>Moon Festival</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Taiwan : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl TsecImplInstance = new TsecImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Taiwan"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Taiwan(TaiwanMarket market = TaiwanMarket.TSEC)
    {
        switch (market)
        {
            case TaiwanMarket.TSEC:
                impl = TsecImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }
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

    private sealed class TsecImpl : Impl
    {
        public override string Name => "Taiwan stock exchange";

        public override bool IsWeekend(Weekday w)
        {
            return w == Weekday.Saturday || w == Weekday.Sunday;
        }

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;

            if (IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Peace Memorial Day
                || (d == 28 && m == Month.February)
                // Labor Day
                || (d == 1 && m == Month.May)
                // Double Tenth
                || (d == 10 && m == Month.October)
                )
            {
                return false;
            }

            if (y == 2002)
            {
                // Dragon Boat Festival and Moon Festival fall on Saturday
                if (// Chinese Lunar New Year
                    (d >= 9 && d <= 17 && m == Month.February)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    )
                {
                    return false;
                }
            }

            if (y == 2003)
            {
                // Tomb Sweeping Day falls on Saturday
                if (// Chinese Lunar New Year
                    ((d >= 31 && m == Month.January) || (d <= 5 && m == Month.February))
                    // Dragon Boat Festival
                    || (d == 4 && m == Month.June)
                    // Moon Festival
                    || (d == 11 && m == Month.September)
                    )
                {
                    return false;
                }
            }

            if (y == 2004)
            {
                // Tomb Sweeping Day falls on Sunday
                if (// Chinese Lunar New Year
                    (d >= 21 && d <= 26 && m == Month.January)
                    // Dragon Boat Festival
                    || (d == 22 && m == Month.June)
                    // Moon Festival
                    || (d == 28 && m == Month.September)
                    )
                {
                    return false;
                }
            }

            if (y == 2005)
            {
                // Dragon Boat and Moon Festival fall on Saturday or Sunday
                if (// Chinese Lunar New Year
                    (d >= 6 && d <= 13 && m == Month.February)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // make up for Labor Day, not seen in other years
                    || (d == 2 && m == Month.May)
                    )
                {
                    return false;
                }
            }

            if (y == 2006)
            {
                // Dragon Boat and Moon Festival fall on Saturday or Sunday
                if (// Chinese Lunar New Year
                    ((d >= 28 && m == Month.January) || (d <= 5 && m == Month.February))
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // Dragon Boat Festival
                    || (d == 31 && m == Month.May)
                    // Moon Festival
                    || (d == 6 && m == Month.October)
                    )
                {
                    return false;
                }
            }

            if (y == 2007)
            {
                if (// Chinese Lunar New Year
                    (d >= 17 && d <= 25 && m == Month.February)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // adjusted holidays
                    || (d == 6 && m == Month.April)
                    || (d == 18 && m == Month.June)
                    // Dragon Boat Festival
                    || (d == 19 && m == Month.June)
                    // adjusted holiday
                    || (d == 24 && m == Month.September)
                    // Moon Festival
                    || (d == 25 && m == Month.September)
                    )
                {
                    return false;
                }
            }

            if (y == 2008)
            {
                if (// Chinese Lunar New Year
                    (d >= 4 && d <= 11 && m == Month.February)
                    // Tomb Sweeping Day
                    || (d == 4 && m == Month.April)
                    )
                {
                    return false;
                }
            }

            if (y == 2009)
            {
                if (// Public holiday
                    (d == 2 && m == Month.January)
                    // Chinese Lunar New Year
                    || (d >= 24 && m == Month.January)
                    // Tomb Sweeping Day
                    || (d == 4 && m == Month.April)
                    // Dragon Boat Festival
                    || ((d == 28 || d == 29) && m == Month.May)
                    // Moon Festival
                    || (d == 3 && m == Month.October)
                    )
                {
                    return false;
                }
            }

            if (y == 2010)
            {
                if (// Chinese Lunar New Year
                       (d >= 13 && d <= 21 && m == Month.January)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // Dragon Boat Festival
                    || (d == 16 && m == Month.May)
                    // Moon Festival
                    || (d == 22 && m == Month.September)
                    )
                {
                    return false;
                }
            }

            if (y == 2011)
            {
                if (// Spring Festival
                       (d >= 2 && d <= 7 && m == Month.February)
                    // Children's Day
                    || (d == 4 && m == Month.April)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // Labour Day
                    || (d == 2 && m == Month.May)
                    // Dragon Boat Festival
                    || (d == 6 && m == Month.June)
                    // Mid-Autumn Festival
                    || (d == 12 && m == Month.September)
                    )
                {
                    return false;
                }
            }

            if (y == 2012)
            {
                if (// Spring Festival
                    (d >= 23 && d <= 27 && m == Month.January)
                    // Peace Memorial Day
                    || (d == 27 && m == Month.February)
                    // Children's Day
                    // Tomb Sweeping Day
                    || (d == 4 && m == Month.April)
                    // Labour Day
                    || (d == 1 && m == Month.May)
                    // Dragon Boat Festival
                    || (d == 23 && m == Month.June)
                    // Mid-Autumn Festival
                    || (d == 30 && m == Month.September)
                    // Memorial Day:
                    // Founding of the Republic of China
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2013)
            {
                if (// Spring Festival
                    (d >= 10 && d <= 15 && m == Month.February)
                    // Children's Day
                    || (d == 4 && m == Month.April)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // Labour Day
                    || (d == 1 && m == Month.May)
                    // Dragon Boat Festival
                    || (d == 12 && m == Month.June)
                    // Mid-Autumn Festival
                    || (d >= 19 && d <= 20 && m == Month.September)
                    )
                {
                    return false;
                }
            }

            if (y == 2014)
            {
                if (// Lunar New Year
                    (d >= 28 && d <= 30 && m == Month.January)
                    // Spring Festival
                    || ((d == 31 && m == Month.January) || (d <= 4 && m == Month.February))
                    // Children's Day
                    || (d == 4 && m == Month.April)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // Dragon Boat Festival
                    || (d == 2 && m == Month.June)
                    // Mid-Autumn Festival
                    || (d == 8 && m == Month.September)
                    )
                {
                    return false;
                }
            }

            if (y == 2015)
            {
                if (// adjusted holidays
                    (d == 2 && m == Month.January)
                    // Lunar New Year
                    || (d >= 18 && d <= 23 && m == Month.February)
                    // adjusted holidays
                    || (d == 27 && m == Month.February)
                    // adjusted holidays
                    || (d == 3 && m == Month.April)
                    // adjusted holidays
                    || (d == 6 && m == Month.April)
                    // adjusted holidays
                    || (d == 19 && m == Month.June)
                    // adjusted holidays
                    || (d == 28 && m == Month.September)
                    // adjusted holidays
                    || (d == 9 && m == Month.October)
                    )
                {
                    return false;
                }
            }

            if (y == 2016)
            {
                if (// Lunar New Year
                    (d >= 8 && d <= 12 && m == Month.February)
                    // adjusted holidays
                    || (d == 29 && m == Month.February)
                    // Children's Day
                    || (d == 4 && m == Month.April)
                    // adjusted holidays
                    || (d == 5 && m == Month.April)
                    // adjusted holidays
                    || (d == 2 && m == Month.May)
                    // Dragon Boat Festival
                    || (d == 9 && m == Month.June)
                    // adjusted holidays
                    || (d == 10 && m == Month.June)
                    // Mid-Autumn Festival
                    || (d == 15 && m == Month.September)
                    // adjusted holidays
                    || (d == 16 && m == Month.September)
                    )
                {
                    return false;
                }
            }

            if (y == 2017)
            {
                if (// adjusted holidays
                    (d == 2 && m == Month.January)
                    // Lunar New Year
                    || ((d >= 27 && m == Month.January) || (d == 1 && m == Month.February))
                    // adjusted holidays
                    || (d == 27 && m == Month.February)
                    // adjusted holidays
                    || (d == 3 && m == Month.April)
                    // Children's Day
                    || (d == 4 && m == Month.April)
                    // adjusted holidays
                    || (d == 29 && m == Month.May)
                    // Dragon Boat Festival
                    || (d == 30 && m == Month.May)
                    // Mid-Autumn Festival
                    || (d == 4 && m == Month.October)
                    // adjusted holidays
                    || (d == 9 && m == Month.October)
                    )
                {
                    return false;
                }
            }

            if (y == 2018)
            {
                if (// Lunar New Year
                    (d >= 15 && d <= 20 && m == Month.February)
                    // Children's Day
                    || (d == 4 && m == Month.April)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // adjusted holidays
                    || (d == 6 && m == Month.April)
                    // Dragon Boat Festival
                    || (d == 18 && m == Month.June)
                    // Mid-Autumn Festival
                    || (d == 24 && m == Month.September)
                    // adjusted holidays
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2019)
            {
                if (// Lunar New Year
                    (d >= 4 && d <= 8 && m == Month.February)
                    // adjusted holidays
                    || (d == 1 && m == Month.March)
                    // Children's Day
                    || (d == 4 && m == Month.April)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // Dragon Boat Festival
                    || (d == 7 && m == Month.June)
                    // Mid-Autumn Festival
                    || (d == 13 && m == Month.September)
                    // adjusted holidays
                    || (d == 11 && m == Month.October)
                    )
                {
                    return false;
                }
            }

            if (y == 2020)
            {
                if (// adjusted holiday
                    (d == 23 && m == Month.January)
                    // Lunar New Year
                    || (d >= 24 && d <= 29 && m == Month.January)
                    // adjusted holiday
                    || (d == 2 && m == Month.April)
                    // adjusted holiday
                    || (d == 3 && m == Month.April)
                    // Dragon Boat Festival
                    || (d == 25 && m == Month.June)
                    // adjusted holiday
                    || (d == 26 && m == Month.June)
                    // Mid-Autumn Festival
                    || (d == 1 && m == Month.October)
                    // adjusted holiday
                    || (d == 2 && m == Month.October)
                    // adjusted holiday
                    || (d == 9 && m == Month.October)
                    )
                {
                    return false;
                }
            }

            if (y == 2021)
            {
                // Tomb Sweeping Day falls on Sunday
                if (// adjusted holiday
                    (d == 10 && m == Month.February)
                    // Lunar New Year
                    || (d >= 11 && d <= 16 && m == Month.February)
                    // adjusted holiday
                    || (d == 1 && m == Month.March)
                    // Children's Day
                    || (d == 2 && m == Month.April)
                    // adjusted holiday
                    || (d == 5 && m == Month.April)
                    // adjusted holiday
                    || (d == 30 && m == Month.April)
                    // Dragon Boat Festival
                    || (d == 14 && m == Month.June)
                    // adjusted holiday
                    || (d == 20 && m == Month.September)
                    // Mid-Autumn Festival
                    || (d == 21 && m == Month.September)
                    // adjusted holiday
                    || (d == 11 && m == Month.October)
                    // adjusted holiday
                    || (d == 31 && m == Month.December)
                    )
                {
                    return false;
                }
            }

            if (y == 2022)
            {
                // Mid-Autumn Festival falls on Saturday
                if ( // Lunar New Year
                    ((d == 31 && m == Month.January) || (d <= 4 && m == Month.February))
                    // Children's Day
                    || (d == 4 && m == Month.April)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // adjusted holiday
                    || (d == 2 && m == Month.May)
                    // Dragon Boat Festival
                    || (d == 3 && m == Month.June)
                    // adjusted holiday
                    || (d == 9 && m == Month.September)
                    )
                {
                    return false;
                }
            }

            if (y == 2023)
            {
                if (// adjusted holiday
                    (d == 2 && m == Month.January)
                    // adjusted holiday
                    || (d == 20 && m == Month.January)
                    // Lunar New Year
                    || (d >= 21 && d <= 24 && m == Month.January)
                    // adjusted holiday
                    || (d >= 25 && d <= 27 && m == Month.January)
                    // adjusted holiday
                    || (d == 27 && m == Month.February)
                    // adjusted holiday
                    || (d == 3 && m == Month.April)
                    // Children's Day
                    || (d == 4 && m == Month.April)
                    // Tomb Sweeping Day
                    || (d == 5 && m == Month.April)
                    // Dragon Boat Festival
                    || (d == 22 && m == Month.June)
                    // adjusted holiday
                    || (d == 23 && m == Month.June)
                    // Mid-Autumn Festival
                    || (d == 29 && m == Month.September)
                    // adjusted holiday
                    || (d == 9 && m == Month.October)
                    )
                {
                    return false;
                }
            }

            if (y == 2024)
            {
                if ( // adjusted holiday
                    (d == 8 && m == Month.February)
                    // Lunar New Year
                    || (d >= 9 && d <= 12 && m == Month.February)
                    // adjusted holiday
                    || (d >= 13 && d <= 14 && m == Month.February)
                    // Children's Day
                    || (d == 4 && m == Month.April)
                    // Tomb-sweeping Day
                    || (d == 5 && m == Month.April)
                    // Dragon Boat Festival
                    || (d == 10 && m == Month.June)
                    // Mid-autumn/Moon Festival
                    || (d == 17 && m == Month.September)
                )
                {
                    return false;
                }
            }

            if (y == 2025)
            {
                // Dragon Boat Festival falls on Saturday
                if (// adjusted holiday
                    (d >= 23 && d <= 24 && m == Month.January)
                    // Lunar New Year
                    || (d >= 27 && d <= 31 && m == Month.January)
                    // adjusted holiday
                    || (d == 3 && m == Month.April)
                    // Children's Day & Tomb-sweeping Day
                    || (d == 4 && m == Month.April)
                    // adjusted holiday
                    || (d == 30 && m == Month.May)
                    // Mid-Autumn Festival
                    || (d == 6 && m == Month.October)
                    )
                {
                    return false;
                }
            }

            if (y == 2026)
            {
                if (// adjusted holiday
                    (d >= 12 && d <= 13 && m == Month.February)
                    // Lunar New Year
                    || (d >= 16 && d <= 20 && m == Month.February)
                    // adjusted holiday (Peace Memorial Day falls on Saturday)
                    || (d == 27 && m == Month.February)
                    // adjusted holiday
                    || (d == 3 && m == Month.April)
                    // adjusted holiday (Tomb-sweeping Day falls on Sunday)
                    || (d == 6 && m == Month.April)
                    // Dragon Boat Festival
                    || (d == 19 && m == Month.June)
                    // Mid-Autumn Festival
                    || (d == 25 && m == Month.September)
                    // adjusted holiday (National Day falls on Saturday)
                    || (d == 9 && m == Month.October)
                    )
                {
                    return false;
                }
            }

            return true;
        }
    }
}
