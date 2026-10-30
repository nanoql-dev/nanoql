// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/italy.hpp ql/time/calendars/italy.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Italian calendars.</summary>
/// <remarks>
/// <para>
/// Public holidays, also used for MOT (Mercato Obbligazionario Telematico):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Epiphany, January 6th</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Liberation Day, April 25th</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Republic Day, June 2nd (since 2000)</description></item>
/// <item><description>Assumption, August 15th</description></item>
/// <item><description>Saint Francis of Assisi, October 4th (since 2026)</description></item>
/// <item><description>All Saint's Day, November 1st</description></item>
/// <item><description>Immaculate Conception Day, December 8th</description></item>
/// <item><description>Christmas Day, December 25th</description></item>
/// <item><description>St. Stephen's Day, December 26th</description></item>
/// <item><description>December 31st, 1999 only (not a public holiday; market holiday to allow millennium-bug
/// deployments)</description></item>
/// </list>
/// </para>
/// <para>
/// The stock exchange (data from https://www.borsaitaliana.it/) follows the TARGET calendar instead. QuantLib's old
/// Milan stock-exchange market, Italy::Exchange, deprecated in version 1.44, isn't ported.
/// </para>
/// </remarks>
public sealed class Italy : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl SettlementImplInstance = new SettlementImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Italy"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Italy(ItalyMarket market = ItalyMarket.Settlement)
    {
        switch (market)
        {
            case ItalyMarket.Settlement:
                impl = SettlementImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Italy.{market}"));
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
        public override string Name => "Italian settlement";

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
                // Liberation Day
                || (d == 25 && m == Month.April)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Republic Day
                || (d == 2 && m == Month.June && y >= 2000)
                // Assumption
                || (d == 15 && m == Month.August)
                // Saint Francis of Assisi
                || (d == 4 && m == Month.October && y >= 2026)
                // All Saints' Day
                || (d == 1 && m == Month.November)
                // Immaculate Conception
                || (d == 8 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // St. Stephen
                || (d == 26 && m == Month.December)
                // December 31st, 1999 only
                || (d == 31 && m == Month.December && y == 1999))
            {
                return false;
            }

            return true;
        }
    }
}
