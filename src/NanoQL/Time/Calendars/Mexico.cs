// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2024 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/mexico.hpp ql/time/calendars/mexico.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Mexican calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Mexican stock exchange (data from http://www.bmv.com.mx/):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Constitution Day, first Monday in February (February 5th before 2006)</description></item>
/// <item><description>Birthday of Benito Juarez, third Monday in March (March 21st before 2006)</description></item>
/// <item><description>Holy Thursday</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>National Day, September 16th</description></item>
/// <item><description>Inauguration Day, October 1st, every sixth year starting 2024</description></item>
/// <item><description>All Souls Day, November 2nd (bank holiday, not a public one)</description></item>
/// <item><description>Revolution Day, third Monday in November (November 20th before 2006)</description></item>
/// <item><description>Our Lady of Guadalupe, December 12th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Mexico : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl BmvImplInstance = new BmvImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Mexico"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Mexico(MexicoMarket market = MexicoMarket.BMV)
    {
        switch (market)
        {
            case MexicoMarket.BMV:
                impl = BmvImplInstance;
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

    private sealed class BmvImpl : Impl
    {
        public override string Name => "Mexican stock exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Constitution Day
                || (y <= 2005 && d == 5 && m == Month.February)
                || (y >= 2006 && d <= 7 && w == Weekday.Monday && m == Month.February)
                // Birthday of Benito Juarez
                || (y <= 2005 && d == 21 && m == Month.March)
                || (y >= 2006 && (d >= 15 && d <= 21) && w == Weekday.Monday && m == Month.March)
                // Holy Thursday
                || (dd == em - 4)
                // Good Friday
                || (dd == em - 3)
                // Labour Day
                || (d == 1 && m == Month.May)
                // National Day
                || (d == 16 && m == Month.September)
                // Inauguration Day
                || (d == 1 && m == Month.October && y >= 2024 && (y - 2024) % 6 == 0)
                // All Souls Day
                || (d == 2 && m == Month.November)
                // Revolution Day
                || (y <= 2005 && d == 20 && m == Month.November)
                || (y >= 2006 && (d >= 15 && d <= 21) && w == Weekday.Monday && m == Month.November)
                // Our Lady of Guadalupe
                || (d == 12 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
