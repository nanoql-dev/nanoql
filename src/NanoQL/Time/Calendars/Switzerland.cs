// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/switzerland.hpp ql/time/calendars/switzerland.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The Swiss calendar.</summary>
/// <remarks>
/// Holidays:
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Berchtoldstag, January 2nd</description></item>
/// <item><description>Good Friday</description></item>
/// <item><description>Easter Monday</description></item>
/// <item><description>Ascension Day</description></item>
/// <item><description>Whit Monday</description></item>
/// <item><description>Labour Day, May 1st</description></item>
/// <item><description>National Day, August 1st</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>St. Stephen's Day, December 26th</description></item>
/// </list>
/// </remarks>
public sealed class Switzerland : Calendar
{
    /// <summary>Initializes a new instance of the <see cref="Switzerland"/> class.</summary>
    public Switzerland()
    {
        ApplyEdits(ConventionProvider.ReadCalendarEdits("Switzerland"));
    }

    /// <inheritdoc/>
    public override string Name => "Switzerland";

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date)
    {
        Weekday w = date.Weekday;
        int d = date.DayOfMonth, dd = date.DayOfYear;
        Month m = date.Month;
        int y = date.Year;
        int em = WesternImpl.EasterMonday(y);
        if (WesternImpl.IsWeekend(w)
            // New Year's Day
            || (d == 1 && m == Month.January)
            // Berchtoldstag
            || (d == 2 && m == Month.January)
            // Good Friday
            || (dd == em - 3)
            // Easter Monday
            || (dd == em)
            // Ascension Day
            || (dd == em + 38)
            // Whit Monday
            || (dd == em + 49)
            // Labour Day
            || (d == 1 && m == Month.May)
            // National Day
            || (d == 1 && m == Month.August)
            // Christmas
            || (d == 25 && m == Month.December)
            // St. Stephen's Day
            || (d == 26 && m == Month.December))
        {
            return false;
        }

        return true;
    }
}
