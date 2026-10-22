// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/ukraine.hpp ql/time/calendars/ukraine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Ukrainian calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Ukrainian stock exchange (data from http://www.ukrse.kiev.ua/eng/):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Orthodox Christmas, January 7th</description></item>
/// <item><description>International Women's Day, March 8th</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Holy Trinity Day, 50 days after Easter</description></item>
/// <item><description>International Workers' Solidarity Days, May 1st and 2nd</description></item>
/// <item><description>Victory Day, May 9th</description></item>
/// <item><description>Constitution Day, June 28th</description></item>
/// <item><description>Independence Day, August 24th</description></item>
/// <item><description>Defender's Day, October 14th (since 2015)</description></item>
/// </list>
/// </para>
/// <para>
/// Holidays falling on a Saturday or Sunday might be moved to the following Monday.
/// </para>
/// </remarks>
public sealed class Ukraine : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl UseImplInstance = new UseImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Ukraine"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Ukraine(UkraineMarket market = UkraineMarket.USE)
    {
        switch (market)
        {
            case UkraineMarket.USE:
                impl = UseImplInstance;
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

    private sealed class UseImpl : Impl
    {
        public override string Name => "Ukrainian stock exchange";

        public override bool IsWeekend(Weekday w) => OrthodoxImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = OrthodoxImpl.EasterMonday(y);
            if (OrthodoxImpl.IsWeekend(w)
                // New Year's Day (possibly moved to Monday)
                || ((d == 1 || ((d == 2 || d == 3) && w == Weekday.Monday))
                    && m == Month.January)
                // Orthodox Christmas
                || ((d == 7 || ((d == 8 || d == 9) && w == Weekday.Monday))
                    && m == Month.January)
                // Women's Day
                || ((d == 8 || ((d == 9 || d == 10) && w == Weekday.Monday))
                    && m == Month.March)
                // Orthodox Easter Monday
                || (dd == em)
                // Holy Trinity Day
                || (dd == em + 49)
                // Workers' Solidarity Days
                || ((d == 1 || d == 2 || (d == 3 && w == Weekday.Monday)) && m == Month.May)
                // Victory Day
                || ((d == 9 || ((d == 10 || d == 11) && w == Weekday.Monday)) && m == Month.May)
                // Constitution Day
                || (d == 28 && m == Month.June)
                // Independence Day
                || (d == 24 && m == Month.August)
                // Defender's Day (since 2015)
                || (d == 14 && m == Month.October && y >= 2015))
            {
                return false;
            }

            return true;
        }
    }
}
