// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2008, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/period.hpp ql/time/period.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Own-tested: Days, LongPeriod

using System.Globalization;

namespace NanoQL.Time;

/// <summary>QuantLib's free functions and output manipulators for periods (<c>period.hpp</c>).</summary>
public static class PeriodFunctions
{
    /// <summary>The period's length in years.</summary>
    /// <param name="p">The period.</param>
    /// <returns>The length in years.</returns>
    /// <exception cref="NanoQLException">The period is in days or weeks.</exception>
    public static double Years(Period p)
    {
        if (p.Length == 0)
        {
            return 0.0;
        }

        return p.Units switch
        {
            TimeUnit.Days => throw new NanoQLException("cannot convert Days into Years"),
            TimeUnit.Weeks => throw new NanoQLException("cannot convert Weeks into Years"),
            TimeUnit.Months => p.Length / 12.0,
            TimeUnit.Years => p.Length,
            _ => throw new NanoQLException($"unknown time unit ({(int)p.Units})"),
        };
    }

    /// <summary>The period's length in months.</summary>
    /// <param name="p">The period.</param>
    /// <returns>The length in months.</returns>
    /// <exception cref="NanoQLException">The period is in days or weeks.</exception>
    public static double Months(Period p)
    {
        if (p.Length == 0)
        {
            return 0.0;
        }

        return p.Units switch
        {
            TimeUnit.Days => throw new NanoQLException("cannot convert Days into Months"),
            TimeUnit.Weeks => throw new NanoQLException("cannot convert Weeks into Months"),
            TimeUnit.Months => p.Length,
            TimeUnit.Years => p.Length * 12.0,
            _ => throw new NanoQLException($"unknown time unit ({(int)p.Units})"),
        };
    }

    /// <summary>The period's length in weeks.</summary>
    /// <param name="p">The period.</param>
    /// <returns>The length in weeks.</returns>
    /// <exception cref="NanoQLException">The period is in months or years.</exception>
    public static double Weeks(Period p)
    {
        if (p.Length == 0)
        {
            return 0.0;
        }

        return p.Units switch
        {
            TimeUnit.Days => p.Length / 7.0,
            TimeUnit.Weeks => p.Length,
            TimeUnit.Months => throw new NanoQLException("cannot convert Months into Weeks"),
            TimeUnit.Years => throw new NanoQLException("cannot convert Years into Weeks"),
            _ => throw new NanoQLException($"unknown time unit ({(int)p.Units})"),
        };
    }

    /// <summary>The period's length in days.</summary>
    /// <param name="p">The period.</param>
    /// <returns>The length in days.</returns>
    /// <exception cref="NanoQLException">The period is in months or years.</exception>
    public static double Days(Period p)
    {
        if (p.Length == 0)
        {
            return 0.0;
        }

        return p.Units switch
        {
            TimeUnit.Days => p.Length,
            TimeUnit.Weeks => p.Length * 7.0,
            TimeUnit.Months => throw new NanoQLException("cannot convert Months into Days"),
            TimeUnit.Years => throw new NanoQLException("cannot convert Years into Days"),
            _ => throw new NanoQLException($"unknown time unit ({(int)p.Units})"),
        };
    }

    /// <summary>A period in long format, e.g. "6 months".</summary>
    /// <param name="p">The period.</param>
    /// <returns>The text.</returns>
    /// <exception cref="NanoQLException">The time unit is not a calendar unit.</exception>
    public static string LongPeriod(Period p)
    {
        int n = p.Length;
        string unit = p.Units switch
        {
            TimeUnit.Days => n == 1 ? " day" : " days",
            TimeUnit.Weeks => n == 1 ? " week" : " weeks",
            TimeUnit.Months => n == 1 ? " month" : " months",
            TimeUnit.Years => n == 1 ? " year" : " years",
            _ => throw new NanoQLException($"unknown time unit ({(int)p.Units})"),
        };
        return n.ToString(CultureInfo.InvariantCulture) + unit;
    }

    /// <summary>A period in short format, e.g. "6M".</summary>
    /// <param name="p">The period.</param>
    /// <returns>The text.</returns>
    /// <exception cref="NanoQLException">The time unit is milliseconds or microseconds.</exception>
    public static string ShortPeriod(Period p)
    {
        string unit = p.Units switch
        {
            TimeUnit.Hours => "h",
            TimeUnit.Minutes => "m",
            TimeUnit.Seconds => "s",
            TimeUnit.Days => "D",
            TimeUnit.Weeks => "W",
            TimeUnit.Months => "M",
            TimeUnit.Years => "Y",
            _ => throw new NanoQLException($"unknown time unit ({(int)p.Units})"),
        };
        return p.Length.ToString(CultureInfo.InvariantCulture) + unit;
    }
}
