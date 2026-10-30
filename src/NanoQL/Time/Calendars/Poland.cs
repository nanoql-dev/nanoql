// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/poland.hpp ql/time/calendars/poland.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Polish calendar.</summary>
/// <remarks>
/// <para>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Corpus Christi</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Epiphany, January 6th (since 2011)</description></item>
/// <item><description>May Day, May 1st</description></item>
/// <item><description>Constitution Day, May 3rd</description></item>
/// <item><description>Assumption of the Blessed Virgin Mary, August 15th</description></item>
/// <item><description>All Saints Day, November 1st</description></item>
/// <item><description>Independence Day, November 11th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>2nd Day of Christmas, December 26th</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Poland : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl WseImplInstance = new WseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Poland"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Poland(PolandMarket market = PolandMarket.Settlement)
    {
        switch (market)
        {
            case PolandMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case PolandMarket.WSE:
                impl = WseImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Poland.{market}"));
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

    private class SettlementImpl : Impl
    {
        public override string Name => "Poland Settlement";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // Easter Monday
                || (dd == em)
                // Corpus Christi
                || (dd == em + 59)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Epiphany
                || (d == 6 && m == Month.January && y >= 2011)
                // May Day
                || (d == 1 && m == Month.May)
                // Constitution Day
                || (d == 3 && m == Month.May)
                // Assumption of the Blessed Virgin Mary
                || (d == 15 && m == Month.August)
                // All Saints Day
                || (d == 1 && m == Month.November)
                // Independence Day
                || (d == 11 && m == Month.November)
                // Christmas
                || (d == 25 && m == Month.December)
                // 2nd Day of Christmas
                || (d == 26 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class WseImpl : SettlementImpl
    {
        public override string Name => "Warsaw stock exchange";

        public override bool IsBusinessDay(Date date)
        {
            // Additional holidays for Warsaw Stock Exchange
            // see https://www.gpw.pl/session-details
            int d = date.DayOfMonth;
            Month m = date.Month;

            if ((d == 24 && m == Month.December)
                || (d == 31 && m == Month.December))
            {
                return false;
            }

            return base.IsBusinessDay(date);
        }
    }
}
