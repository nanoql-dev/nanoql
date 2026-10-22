// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/slovakia.hpp ql/time/calendars/slovakia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Slovak calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Bratislava stock exchange (data from http://www.bsse.sk/):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Epiphany, January 6th</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>May Day, May 1st</description></item>
/// <item><description>Liberation of the Republic, May 8th</description></item>
/// <item><description>SS. Cyril and Methodius, July 5th</description></item>
/// <item><description>Slovak National Uprising, August 29th</description></item>
/// <item><description>Constitution of the Slovak Republic, September 1st</description></item>
/// <item><description>Our Lady of the Seven Sorrows, September 15th</description></item>
/// <item><description>All Saints Day, November 1st</description></item>
/// <item><description>Freedom and Democracy of the Slovak Republic, November 17th</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>St. Stephen, December 26th</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Slovakia : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl BsseImplInstance = new BsseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Slovakia"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Slovakia(SlovakiaMarket market = SlovakiaMarket.BSSE)
    {
        switch (market)
        {
            case SlovakiaMarket.BSSE:
                impl = BsseImplInstance;
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

    private sealed class BsseImpl : Impl
    {
        public override string Name => "Bratislava stock exchange";

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
                // Epiphany
                || (d == 6 && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // May Day
                || (d == 1 && m == Month.May)
                // Liberation of the Republic
                || (d == 8 && m == Month.May)
                // SS. Cyril and Methodius
                || (d == 5 && m == Month.July)
                // Slovak National Uprising
                || (d == 29 && m == Month.August)
                // Constitution of the Slovak Republic
                || (d == 1 && m == Month.September)
                // Our Lady of the Seven Sorrows
                || (d == 15 && m == Month.September)
                // All Saints Day
                || (d == 1 && m == Month.November)
                // Freedom and Democracy of the Slovak Republic
                || (d == 17 && m == Month.November)
                // Christmas Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // St. Stephen
                || (d == 26 && m == Month.December)
                // unidentified closing days for stock exchange
                || (d >= 24 && d <= 31 && m == Month.December && y == 2004)
                || (d >= 24 && d <= 31 && m == Month.December && y == 2005))
            {
                return false;
            }

            return true;
        }
    }
}
