// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/iceland.hpp ql/time/calendars/iceland.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Icelandic calendars.</summary>
/// <remarks>
/// <para>
/// Holidays for the Iceland stock exchange (data from http://www.icex.is/is/calendar?languageID=1):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Holy Thursday</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>First day of Summer (third or fourth Thursday in April)</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Ascension Thursday</description></item>
/// <item><description>Pentecost Monday</description></item>
/// <item><description>Independence Day, June 17th</description></item>
/// <item><description>Commerce Day, first Monday in August</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Boxing Day, December 26th</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Iceland : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl IcexImplInstance = new IcexImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Iceland"/> class for the given market.</summary>
    /// <param name="market">The market whose holidays the calendar follows.</param>
    /// <exception cref="ArgumentOutOfRangeException">The market isn't a member of the enum.</exception>
    public Iceland(IcelandMarket market = IcelandMarket.ICEX)
    {
        switch (market)
        {
            case IcelandMarket.ICEX:
                impl = IcexImplInstance;
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

    private sealed class IcexImpl : Impl
    {
        public override string Name => "Iceland stock exchange";

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
                // Easter Monday
                || (dd == em)
                // First day of Summer
                || (d >= 19 && d <= 25 && w == Weekday.Thursday && m == Month.April)
                // Ascension Thursday
                || (dd == em + 38)
                // Pentecost Monday
                || (dd == em + 49)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Independence Day
                || (d == 17 && m == Month.June)
                // Commerce Day
                || (d <= 7 && w == Weekday.Monday && m == Month.August)
                // Christmas
                || (d == 25 && m == Month.December)
                // Boxing Day
                || (d == 26 && m == Month.December))
            {
                return false;
            }

            return true;
        }
    }
}
