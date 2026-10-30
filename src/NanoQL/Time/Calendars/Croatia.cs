// SPDX-FileCopyrightText: Copyright (C) 2026 SoftSolution srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/croatia.hpp ql/time/calendars/croatia.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Croatian calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Zagreb stock exchange (see https://zse.hr/en/non-trading-days/110):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Epiphany, January 6th</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>National Day May 30th</description></item>
/// <item><description>Corpus Christi, 60 days after Easter Sunday.</description></item>
/// <item><description>Anti-Fascist Struggle Day, June 22nd</description></item>
/// <item><description>Victory and Homeland Thanksgiving Day and the Day of Croatian Defenders August
/// 5th</description></item>
/// <item><description>Assumption of Mary, August 15th</description></item>
/// <item><description>Remembrance Day for the Victims of the Vukovar and Skabrnja War Memorials, November
/// 18th</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>St. Stephen, December 26th</description></item>
/// <item><description>New Year's Eve, December 31st</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Croatia : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl ZseImplInstance = new ZseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Croatia"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Croatia(CroatiaMarket market = CroatiaMarket.ZSE)
    {
        switch (market)
        {
            case CroatiaMarket.ZSE:
                impl = ZseImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Croatia.{market}"));
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

    private sealed class ZseImpl : Impl
    {
        public override string Name => "Zagreb stock exchange";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            // Croatian holidays
            if (WesternImpl.IsWeekend(w)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // Epiphany
                || (d == 6 && m == Month.January)
                // Good Friday
                || (dd == em - 3 && y >= 2016)
                // Easter Monday
                || (dd == em)
                // Labour Day
                || (d == 1 && m == Month.May)
                // National Day
                || (d == 30 && m == Month.May)
                // Corpus Christi
                || (dd == em + 59)
                // Anti-Fascist Struggle Day
                || (d == 22 && m == Month.June)
                // Victory and Homeland Thanksgiving Day and the Day of Croatian Defenders
                || (d == 5 && m == Month.August)
                // Assumption of Mary
                || (d == 15 && m == Month.August)
                // Remembrance Day for the Victims of the Vukovar and ï¿½kabrnja War Memorials
                || (d == 18 && m == Month.November)
                // Christmas Eve
                || (d == 24 && m == Month.December)
                // Christmas
                || (d == 25 && m == Month.December)
                // St. Stephen
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
