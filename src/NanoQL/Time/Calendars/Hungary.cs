// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/hungary.hpp ql/time/calendars/hungary.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Hungarian calendar.</summary>
/// <remarks>
/// <para>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>Good Friday (since 2017)</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Whit(Pentecost) Monday</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>National Day, March 15th</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>Constitution Day, August 20th</description></item>
/// <item><description>Republic Day, October 23rd</description></item>
/// <item><description>All Saints Day, November 1st</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>2nd Day of Christmas, December 26th</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Hungary : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl HungaryImplInstance = new HungaryImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Hungary"/> class.</summary>
    public Hungary()
    {
        impl = HungaryImplInstance;
        ApplyEdits(ConventionProvider.ReadCalendarEdits("Hungary"));
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

    private sealed class HungaryImpl : Impl
    {
        public override string Name => "Hungary";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // Good Friday (since 2017)
                || (dd == em - 3 && y >= 2017)
                // Easter Monday
                || (dd == em)
                // Whit Monday
                || (dd == em + 49)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // National Day
                || (d == 15 && m == Month.March)
                // Labour Day
                || (d == 1 && m == Month.May)
                // Constitution Day
                || (d == 20 && m == Month.August)
                // Republic Day
                || (d == 23 && m == Month.October)
                // All Saints Day
                || (d == 1 && m == Month.November)
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
}
