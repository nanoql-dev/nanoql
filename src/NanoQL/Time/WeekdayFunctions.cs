// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Toyin Akin
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/weekday.hpp ql/time/weekday.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>QuantLib's output manipulators for weekdays (<c>weekday.hpp</c>).</summary>
/// <remarks>
/// <see cref="Weekday"/>'s aliases share their day's value, so <see cref="Enum.ToString()"/> may return either name;
/// <see cref="ToString(Weekday)"/> gives QuantLib's text.
/// </remarks>
public static class WeekdayFunctions
{
    /// <summary>A weekday's full name, e.g. "Wednesday".</summary>
    /// <param name="d">The weekday.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a weekday.</exception>
    public static string LongWeekday(Weekday d) => d switch
    {
        Weekday.Sunday => "Sunday",
        Weekday.Monday => "Monday",
        Weekday.Tuesday => "Tuesday",
        Weekday.Wednesday => "Wednesday",
        Weekday.Thursday => "Thursday",
        Weekday.Friday => "Friday",
        Weekday.Saturday => "Saturday",
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "unknown weekday"),
    };

    /// <summary>A weekday's three-letter name, e.g. "Wed".</summary>
    /// <param name="d">The weekday.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a weekday.</exception>
    public static string ShortWeekday(Weekday d) => d switch
    {
        Weekday.Sunday => "Sun",
        Weekday.Monday => "Mon",
        Weekday.Tuesday => "Tue",
        Weekday.Wednesday => "Wed",
        Weekday.Thursday => "Thu",
        Weekday.Friday => "Fri",
        Weekday.Saturday => "Sat",
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "unknown weekday"),
    };

    /// <summary>A weekday's two-letter name, e.g. "We".</summary>
    /// <param name="d">The weekday.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a weekday.</exception>
    public static string ShortestWeekday(Weekday d) => d switch
    {
        Weekday.Sunday => "Su",
        Weekday.Monday => "Mo",
        Weekday.Tuesday => "Tu",
        Weekday.Wednesday => "We",
        Weekday.Thursday => "Th",
        Weekday.Friday => "Fr",
        Weekday.Saturday => "Sa",
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "unknown weekday"),
    };

    /// <summary>A weekday's text, as QuantLib writes it: its full name.</summary>
    /// <param name="w">The weekday.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a weekday.</exception>
    public static string ToString(Weekday w)
    {
        EnumArguments.Check(w, nameof(w));
        return LongWeekday(w);
    }
}
