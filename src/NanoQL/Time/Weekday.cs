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

/// <summary>A day's serial number modulo 7; Excel's WEEKDAY function is the same except for Sunday = 7.</summary>
/// <remarks>
/// The aliases share their day's value, so <see cref="Enum.ToString()"/> may return either name; QuantLib's text is
/// <see cref="WeekdayFunctions.ToString(Weekday)"/>.
/// </remarks>
public enum Weekday
{
    /// <summary>Sunday.</summary>
    Sunday = 1,

    /// <summary>Monday.</summary>
    Monday = 2,

    /// <summary>Tuesday.</summary>
    Tuesday = 3,

    /// <summary>Wednesday.</summary>
    Wednesday = 4,

    /// <summary>Thursday.</summary>
    Thursday = 5,

    /// <summary>Friday.</summary>
    Friday = 6,

    /// <summary>Saturday.</summary>
    Saturday = 7,

    /// <summary>Sunday.</summary>
    Sun = 1,

    /// <summary>Monday.</summary>
    Mon = 2,

    /// <summary>Tuesday.</summary>
    Tue = 3,

    /// <summary>Wednesday.</summary>
    Wed = 4,

    /// <summary>Thursday.</summary>
    Thu = 5,

    /// <summary>Friday.</summary>
    Fri = 6,

    /// <summary>Saturday.</summary>
    Sat = 7,
}
