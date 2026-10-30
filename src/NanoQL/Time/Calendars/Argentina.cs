// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/argentina.hpp ql/time/calendars/argentina.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Argentinian calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Buenos Aires stock exchange (data from http://www.merval.sba.com.ar/):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Holy Thursday</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>May Revolution, May 25th</description></item>
/// <item><description>Death of General Manuel Belgrano, third Monday of June</description></item>
/// <item><description>Independence Day, July 9th</description></item>
/// <item><description>Death of General José de San Martín, third Monday of August</description></item>
/// <item><description>Columbus Day, October 12th (moved to preceding Monday if on Tuesday or Wednesday and to following
/// if on Thursday or Friday)</description></item>
/// <item><description>Immaculate Conception, December 8th</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>New Year's Eve, December 31st</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Argentina : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl MervalImplInstance = new MervalImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Argentina"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Argentina(ArgentinaMarket market = ArgentinaMarket.Merval)
    {
        switch (market)
        {
            case ArgentinaMarket.Merval:
                impl = MervalImplInstance;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(market), market, "unknown market");
        }

        ApplyEdits(ConventionProvider.ReadCalendarEdits($"Argentina.{market}"));
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

    private sealed class MervalImpl : Impl
    {
        public override string Name => "Buenos Aires stock exchange";

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
                // Holy Thursday
                || (dd == em - 4)
                // Good Friday
                || (dd == em - 3)
                // Labour Day
                || (d == 1 && m == Month.May)
                // May Revolution
                || (d == 25 && m == Month.May)
                // Death of General Manuel Belgrano
                || (d >= 15 && d <= 21 && w == Weekday.Monday && m == Month.June)
                // Independence Day
                || (d == 9 && m == Month.July)
                // Death of General José de San Martín
                || (d >= 15 && d <= 21 && w == Weekday.Monday && m == Month.August)
                // Columbus Day
                || ((d == 10 || d == 11 || d == 12 || d == 15 || d == 16)
                    && w == Weekday.Monday && m == Month.October)
                // Immaculate Conception
                || (d == 8 && m == Month.December)
                // Christmas Eve
                || (d == 24 && m == Month.December)
                // New Year's Eve
                || ((d == 31 || (d == 30 && w == Weekday.Friday)) && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
