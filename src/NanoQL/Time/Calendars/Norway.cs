// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/norway.hpp ql/time/calendars/norway.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Norwegian calendar.</summary>
/// <remarks>
/// <para>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>Holy Thursday</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Ascension</description></item>
/// <item><description>Whit(Pentecost) Monday</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>May Day, May 1st</description></item>
/// <item><description>National Independence Day, May 17th</description></item>
/// <item><description>Christmas Eve, December 24th</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Boxing Day, December 26th</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class Norway : Calendar
{
    // all calendar instances share the same implementation instance
    private static readonly Impl NorwayImplInstance = new NorwayImpl();

    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Norway"/> class.</summary>
    public Norway()
    {
        impl = NorwayImplInstance;
        ApplyEdits(ConventionProvider.ReadCalendarEdits("Norway"));
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

    private sealed class NorwayImpl : Impl
    {
        public override string Name => "Norway";

        public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

        public override bool IsBusinessDay(Date date)
        {
            Weekday w = date.Weekday;
            int d = date.DayOfMonth, dd = date.DayOfYear;
            Month m = date.Month;
            int y = date.Year;
            int em = WesternImpl.EasterMonday(y);
            if (WesternImpl.IsWeekend(w)
                // Holy Thursday
                || (dd == em - 4)
                // Good Friday
                || (dd == em - 3)
                // Easter Monday
                || (dd == em)
                // Ascension Thursday
                || (dd == em + 38)
                // Whit Monday
                || (dd == em + 49)
                // New Year's Day
                || (d == 1 && m == Month.January)
                // May Day
                || (d == 1 && m == Month.May)
                // National Independence Day
                || (d == 17 && m == Month.May)
                // Christmas Eve
                || (d == 24 && m == Month.December && y >= 2002)
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
