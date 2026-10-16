// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2020 Leonardo Arcari
// SPDX-FileCopyrightText: Copyright (C) 2020 Kline s.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/date.hpp ql/time/date.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: io::formatted_date (not ported: a Date converts to DateOnly, whose ToString(format) does its job)
// Own-tested: ShortDate

using System.Globalization;
using NanoQL.Utilities;

namespace NanoQL.Time;

/// <summary>QuantLib's free functions and output manipulators for dates (<c>date.hpp</c>).</summary>
public static class DateFunctions
{
    /// <summary>Difference in days, including any fraction of a day, between two dates.</summary>
    /// <param name="d1">The earlier date.</param>
    /// <param name="d2">The later date.</param>
    /// <returns><c>d2 - d1</c>, in days.</returns>
    public static double DaysBetween(Date d1, Date d2) => d2 - d1;

    /// <summary>A date in short format, mm/dd/yyyy.</summary>
    /// <param name="d">The date, or <see langword="null"/>.</param>
    /// <returns>The text, or "null date".</returns>
    public static string ShortDate(Date? d)
    {
        if (d is not Date date)
        {
            return "null date";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{(int)date.Month:00}/{date.DayOfMonth:00}/{date.Year}");
    }

    /// <summary>A date in long format, e.g. "October 15th, 2026".</summary>
    /// <param name="d">The date, or <see langword="null"/>.</param>
    /// <returns>The text, or "null date".</returns>
    public static string LongDate(Date? d)
    {
        if (d is not Date date)
        {
            return "null date";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ToString(date.Month)} {DataFormattersFunctions.Ordinal(date.DayOfMonth)}, {date.Year}");
    }

    /// <summary>A date in ISO format, yyyy-mm-dd.</summary>
    /// <param name="d">The date, or <see langword="null"/>.</param>
    /// <returns>The text, or "null date".</returns>
    public static string IsoDate(Date? d)
    {
        if (d is not Date date)
        {
            return "null date";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{date.Year}-{(int)date.Month:00}-{date.DayOfMonth:00}");
    }

    /// <summary>A month's name, as QuantLib writes it.</summary>
    /// <param name="m">The month.</param>
    /// <returns>The month's full name.</returns>
    /// <exception cref="NanoQLException">The value is not a month.</exception>
    public static string ToString(Month m) => m switch
    {
        Month.January => "January",
        Month.February => "February",
        Month.March => "March",
        Month.April => "April",
        Month.May => "May",
        Month.June => "June",
        Month.July => "July",
        Month.August => "August",
        Month.September => "September",
        Month.October => "October",
        Month.November => "November",
        Month.December => "December",
        _ => throw new NanoQLException($"unknown month ({(int)m})"),
    };
}
