// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/czechrepublic.hpp ql/time/calendars/czechrepublic.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Czech calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Prague stock exchange (see http://www.pse.cz/):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Liberation Day, May 8th</description></item>
/// <item><description>SS. Cyril and Methodius, July 5th</description></item>
/// <item><description>Jan Hus Day, July 6th</description></item>
/// <item><description>Czech Statehood Day, September 28th</description></item>
/// <item><description>Independence Day, October 28th</description></item>
/// <item><description>Struggle for Freedom and Democracy Day, November 17th</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>St. Stephen, December 26th</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class CzechRepublic : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl PseImplInstance = new PseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="CzechRepublic"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public CzechRepublic(CzechRepublicMarket market = CzechRepublicMarket.PSE)
    {
        switch (market)
        {
            case CzechRepublicMarket.PSE:
                impl = PseImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"CzechRepublic.{market}"));
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

    private sealed class PseImpl : Impl
    {
        public override string Name => "Prague stock exchange";

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
                // Good Friday
                || (dd == em - 3 && y >= 2016)
                // Easter Monday
                || (dd == em)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Liberation Day
                || (d == 8 && m == Month.May)
                // SS. Cyril and Methodius
                || (d == 5 && m == Month.July)
                // Jan Hus Day
                || (d == 6 && m == Month.July)
                // Czech Statehood Day
                || (d == 28 && m == Month.September)
                // Independence Day
                || (d == 28 && m == Month.October)
                // Struggle for Freedom and Democracy Day
                || (d == 17 && m == Month.November)
                // Christmas Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // St. Stephen
                || (d == 26 && m == Month.December)
                // unidentified closing days for stock exchange
                || (d == 2 && m == Month.January && y == 2004)
                || (d == 31 && m == Month.December && y == 2004))
            {
                return false;
            }

            return true;
        }
    }
}
