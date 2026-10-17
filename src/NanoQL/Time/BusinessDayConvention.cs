// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2014 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Jeff Yu
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/businessdayconvention.hpp ql/time/businessdayconvention.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>
/// Business-day conventions: the algorithm used to adjust a date in case it is not a valid business day.
/// </summary>
/// <remarks>
/// QuantLib's text, which differs from three members' names, is
/// <see cref="BusinessDayConventionFunctions.ToString(BusinessDayConvention)"/>.
/// </remarks>
public enum BusinessDayConvention
{
    /// <summary>Choose the first business day after the given holiday (ISDA).</summary>
    Following,

    /// <summary>
    /// Choose the first business day after the given holiday unless it belongs to a different month, in which case
    /// choose the first business day before the holiday (ISDA).
    /// </summary>
    ModifiedFollowing,

    /// <summary>Choose the first business day before the given holiday (ISDA).</summary>
    Preceding,

    /// <summary>
    /// Choose the first business day before the given holiday unless it belongs to a different month, in which case
    /// choose the first business day after the holiday.
    /// </summary>
    ModifiedPreceding,

    /// <summary>Do not adjust.</summary>
    Unadjusted,

    /// <summary>
    /// Choose the first business day after the given holiday unless that day crosses the mid-month (15th) or the end
    /// of month, in which case choose the first business day before the holiday.
    /// </summary>
    HalfMonthModifiedFollowing,

    /// <summary>
    /// Choose the nearest business day to the given holiday. If both the preceding and following business days are
    /// equally far away, default to the following business day.
    /// </summary>
    Nearest,
}
