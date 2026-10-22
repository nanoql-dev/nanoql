// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2007 Richard Gomes
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/brazil.hpp ql/time/calendars/brazil.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Brazilian calendars.</summary>
/// <remarks>
/// <para>
/// Banking holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Tiradentes's Day, April 21th</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Independence Day, September 7th</description></item>
/// <item><description>Nossa Sra. Aparecida Day, October 12th</description></item>
/// <item><description>All Souls Day, November 2nd</description></item>
/// <item><description>Republic Day, November 15th</description></item>
/// <item><description>Black Awareness Day, November 20th (since 2024)</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Passion of Christ</description></item>
/// <item><description>Carnival</description></item>
/// <item><description>Corpus Christi</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Bovespa stock exchange:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Sao Paulo City Day, January 25th (up to 2021 included)</description></item>
/// <item><description>Tiradentes's Day, April 21th</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Revolution Day, July 9th (up to 2021 included)</description></item>
/// <item><description>Independence Day, September 7th</description></item>
/// <item><description>Nossa Sra. Aparecida Day, October 12th</description></item>
/// <item><description>All Souls Day, November 2nd</description></item>
/// <item><description>Republic Day, November 15th</description></item>
/// <item><description>Black Consciousness Day, November 20th (since 2007, except 2022 and 2023)</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Passion of Christ</description></item>
/// <item><description>Carnival</description></item>
/// <item><description>Corpus Christi</description></item>
/// <item><description>the last business day of the year</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Brazil : Calendar
{
    // all calendar instances on the same market share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl ExchangeImplInstance = new ExchangeImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Brazil"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows; the generic settlement calendar by
    /// default.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Brazil(BrazilMarket market = BrazilMarket.Settlement)
    {
        switch (market)
        {
            case BrazilMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case BrazilMarket.Exchange:
                impl = ExchangeImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => impl.IsBusinessDay(date);

    // The market-specific rules; QuantLib's Calendar::Impl, with Calendar::WesternImpl's weekend.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public abstract bool IsBusinessDay(Date date);
    }

    private sealed class SettlementImpl : Impl
    {
        public override string Name => "Brazil";

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
                // Tiradentes Day
                || (d == 21 && m == Month.April)
                // Labor Day
                || (d == 1 && m == Month.May)
                // Independence Day
                || (d == 7 && m == Month.September)
                // Nossa Sra. Aparecida Day
                || (d == 12 && m == Month.October)
                // All Souls Day
                || (d == 2 && m == Month.November)
                // Republic Day
                || (d == 15 && m == Month.November)
                // Black Awareness Day
                || (d == 20 && m == Month.November && y >= 2024)
                // Christmas
                || (d == 25 && m == Month.December)
                // Passion of Christ
                || (dd == em - 3)
                // Carnival
                || (dd == em - 49 || dd == em - 48)
                // Corpus Christi
                || (dd == em + 59))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class ExchangeImpl : Impl
    {
        public override string Name => "BOVESPA";

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
                // Sao Paulo City Day
                || (d == 25 && m == Month.January && y < 2022)
                // Tiradentes Day
                || (d == 21 && m == Month.April)
                // Labor Day
                || (d == 1 && m == Month.May)
                // Revolution Day
                || (d == 9 && m == Month.July && y < 2022)
                // Independence Day
                || (d == 7 && m == Month.September)
                // Nossa Sra. Aparecida Day
                || (d == 12 && m == Month.October)
                // All Souls Day
                || (d == 2 && m == Month.November)
                // Republic Day
                || (d == 15 && m == Month.November)
                // Black Consciousness Day
                || (d == 20 && m == Month.November && y >= 2007 && y != 2022 && y != 2023)
                // Christmas Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // Passion of Christ
                || (dd == em - 3)
                // Carnival
                || (dd == em - 49 || dd == em - 48)
                // Corpus Christi
                || (dd == em + 59)
                // last business day of the year
                || (m == Month.December && (d == 31 || (d >= 29 && w == Weekday.Friday))))
            {
                return false;
            }

            return true;
        }
    }
}
