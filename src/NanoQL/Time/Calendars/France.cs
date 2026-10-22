// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/france.hpp ql/time/calendars/france.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The French calendars.</summary>
/// <remarks>
/// <para>
/// Public holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Armistice 1945, May 8th</description></item>
/// <item><description>Ascension Thursday</description></item>
/// <item><description>Whit Monday (Lundi de Pentecôte)</description></item>
/// <item><description>Fête nationale, July 14th</description></item>
/// <item><description>Assumption, August 15th</description></item>
/// <item><description>All Saint's Day, November 1st</description></item>
/// <item><description>Armistice 1918, November 11th</description></item>
/// <item><description>Christmas Day, December 25th</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the stock exchange (data from
/// https://www.stockmarketclock.com/exchanges/euronext-paris/market-holidays/):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas Day, December 25th</description></item>
/// <item><description>Boxing Day, December 26th</description></item>
/// <item><description>New Year's Eve, December 31st</description></item>
/// </list>
/// </para>
/// <para>
/// Ascension and Whit Monday follow Easter, as QuantLib's master has them since 1.44 (#2893); QuantLib 1.44 holds them
/// on May 10th and May 21st, right only when Easter Monday is April 2nd: a documented divergence.
/// </para>
/// </remarks>
public sealed class France : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();
    private static readonly Impl ExchangeImplInstance = new ExchangeImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="France"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public France(FranceMarket market = FranceMarket.Settlement)
    {
        switch (market)
        {
            case FranceMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            case FranceMarket.Exchange:
                impl = ExchangeImplInstance;
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

    private sealed class SettlementImpl : Impl
    {
        public override string Name => "French settlement";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // Jour de l'An
                || (d == 1 && m == Month.January)
                // Lundi de Paques
                || (dd == em)
                // Fete du Travail
                || (d == 1 && m == Month.May)
                // Victoire 1945
                || (d == 8 && m == Month.May)
                // Ascension
                || (dd == em + 38)
                // Lundi de Pentecote
                || (dd == em + 49)
                // Fete nationale
                || (d == 14 && m == Month.July)
                // Assomption
                || (d == 15 && m == Month.August)
                // Toussaint
                || (d == 1 && m == Month.November)
                // Armistice 1918
                || (d == 11 && m == Month.November)
                // Noel
                || (d == 25 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class ExchangeImpl : Impl
    {
        public override string Name => "Paris stock exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // Jour de l'An
                || (d == 1 && m == Month.January)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Labor Day
                || (d == 1 && m == Month.May)
                // Christmas Eve
                || (d == 24 && m == Month.December)
                // Christmas Day
                || (d == 25 && m == Month.December)
                // Boxing Day
                || (d == 26 && m == Month.December)
                // New Year's Eve
                || (d == 31 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
