// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/target.hpp ql/time/calendars/target.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The TARGET calendar.</summary>
/// <remarks>
/// Holidays (see http://www.ecb.int):
/// <list type="bullet">
/// <item><description>Saturdays</description></item>
/// <item><description>Sundays</description></item>
/// <item><description>New Year's Day, January 1st</description></item>
/// <item><description>Good Friday (since 2000)</description></item>
/// <item><description>Easter Monday (since 2000)</description></item>
/// <item><description>Labour Day, May 1st (since 2000)</description></item>
/// <item><description>Christmas, December 25th</description></item>
/// <item><description>Day of Goodwill, December 26th (since 2000)</description></item>
/// <item><description>December 31st (1998, 1999, and 2001)</description></item>
/// </list>
/// </remarks>
public sealed class TARGET : Calendar
{
    /// <summary>Initializes a new instance of the <see cref="TARGET"/> class.</summary>
    public TARGET()
    {
    }

    /// <inheritdoc/>
    public override string Name => "TARGET";

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
            // Good Friday
            || (dd == em - 3 && y >= 2000)
            // Easter Monday
            || (dd == em && y >= 2000)
            // Labour Day
            || (d == 1 && m == Month.May && y >= 2000)
            // Christmas
            || (d == 25 && m == Month.December)
            // Day of Goodwill
            || (d == 26 && m == Month.December && y >= 2000)
            // December 31st, 1998, 1999, and 2001 only
            || (d == 31 && m == Month.December
                && (y == 1998 || y == 1999 || y == 2001)))
        {
            return false;
        }

        return true;
    }
}
