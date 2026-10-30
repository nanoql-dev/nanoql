// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/austria.hpp ql/time/calendars/austria.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Austrian calendars.</summary>
/// <remarks>
/// <para>
/// Public holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Epiphany, January 6th</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Ascension Thursday</description></item>
/// <item><description>Whit Monday</description></item>
/// <item><description>Corpus Christi</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Assumption Day, August 15th</description></item>
/// <item><description>National Holiday, October 26th, since 1967</description></item>
/// <item><description>All Saints Day, November 1st</description></item>
/// <item><description>National Holiday, November 12th, 1919-1934</description></item>
/// <item><description>Immaculate Conception Day, December 8th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>St. Stephen, December 26th</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the stock exchange (data from
/// https://www.wienerborse.at/en/trading/trading-information/trading-calendar/):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Whit Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>National Holiday, October 26th, since 1967</description></item>
/// <item><description>National Holiday, November 12th, 1919-1934</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>St. Stephen, December 26th</description></item>
/// <item><description>Exchange Holiday</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Austria : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl ExchangeImplInstance = new ExchangeImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Austria"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Austria(AustriaMarket market = AustriaMarket.Settlement)
    {
        switch (market)
        {
            case AustriaMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case AustriaMarket.Exchange:
                impl = ExchangeImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Austria.{market}"));
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

    private sealed class SettlementImpl : Impl
    {
        public override string Name => "Austrian settlement";

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
                // Easter Monday
                || (dd == em)
                // Ascension Thurday
                || (dd == em + 38)
                // Whit Monday
                || (dd == em + 49)
                // Corpus Christi
                || (dd == em + 59)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Assumption
                || (d == 15 && m == Month.August)
                // National Holiday since 1967
                || (d == 26 && m == Month.October && y >= 1967)
                // National Holiday 1919-1934
                || (d == 12 && m == Month.November && y >= 1919 && y <= 1934)
                // All Saints' Day
                || (d == 1 && m == Month.November)
                // Immaculate Conception
                || (d == 8 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // St. Stephen
                || (d == 26 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class ExchangeImpl : Impl
    {
        public override string Name => "Vienna stock exchange";

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
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Whit Monay
                || (dd == em + 49)
                // Labour Day
                || (d == 1 && m == Month.May)
                // National Holiday since 1967
                || (d == 26 && m == Month.October && y >= 1967)
                // National Holiday 1919-1934
                || (d == 12 && m == Month.November && y >= 1919 && y <= 1934)
                // Christmas' Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // St. Stephen
                || (d == 26 && m == Month.December)
                // Exchange Holiday
                || (d == 31 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
