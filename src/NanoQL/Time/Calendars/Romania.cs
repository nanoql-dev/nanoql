// SPDX-FileCopyrightText: Copyright (C) 2010 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Riccardo Barone
// SPDX-FileCopyrightText: Copyright (C) 2018 Matthias Lungwitz
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/romania.hpp ql/time/calendars/romania.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Romanian calendars.</summary>
/// <remarks>
/// <para>
/// Public holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Day after New Year's Day, January 2nd</description></item>
/// <item><description>Unification Day, January 24th</description></item>
/// <item><description>Orthodox Easter (only Sunday and Monday)</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Pentecost with Monday (50th and 51st days after the Othodox Easter)</description></item>
/// <item><description>Children's Day, June 1st (since 2017)</description></item>
/// <item><description>St Marys Day, August 15th</description></item>
/// <item><description>Feast of St Andrew, November 30th</description></item>
/// <item><description>National Day, December 1st</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>2nd Day of Christmas, December 26th</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays for the Bucharest stock exchange (data from http://www.bvb.ro/Marketplace/TradingCalendar/index.aspx): all
/// public holidays, plus a few one-off closing days (2014 only).
/// </para>
/// </remarks>
public sealed class Romania : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl PublicImplInstance = new PublicImpl();
    private static readonly Impl BVBImplInstance = new BVBImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Romania"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Romania(RomaniaMarket market = RomaniaMarket.BVB)
    {
        switch (market)
        {
            case RomaniaMarket.Public:
                impl = PublicImplInstance;
                break;
            case RomaniaMarket.BVB:
                impl = BVBImplInstance;
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

    private class PublicImpl : Impl
    {
        public override string Name => "Romania";

        public override bool IsWeekend(Weekday w) => OrthodoxImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = OrthodoxImpl.EasterMonday(y);
            if (OrthodoxImpl.IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Day after New Year's Day
                || (d == 2 && m == Month.January)
                // Unification Day
                || (d == 24 && m == Month.January)
                // Orthodox Easter Monday
                || (dd == em)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Pentecost
                || (dd == em + 49)
                // Children's Day (since 2017)
                || (d == 1 && m == Month.June && y >= 2017)
                // St Marys Day
                || (d == 15 && m == Month.August)
                // Feast of St Andrew
                || (d == 30 && m == Month.November)
                // National Day
                || (d == 1 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // 2nd Day of Chritsmas
                || (d == 26 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }

    private sealed class BVBImpl : PublicImpl
    {
        public override string Name => "Bucharest stock exchange";

        public override bool IsBusinessDay(Date date)
        {
            if (!base.IsBusinessDay(date))
            {
                return false;
            }

            int d = date.DayOfMonth;
            Month m = date.Month;
            int y = date.Year;
            if (// one-off closing days
                (d == 24 && m == Month.December && y == 2014)
                || (d == 31 && m == Month.December && y == 2014))
            {
                return false;
            }

            return true;
        }
    }
}
