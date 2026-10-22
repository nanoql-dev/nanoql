// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/eurlibor.hpp ql/indexes/ibor/eurlibor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// The conventions EUR Libor's tenors follow, shared by <see cref="EURLibor"/> and <see cref="DailyTenorEURLibor"/>.
/// </summary>
internal static class EURLiborConventions
{
    /// <summary>Following for days and weeks, modified following for months and years.</summary>
    /// <param name="p">The tenor.</param>
    /// <returns>The convention.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The tenor's unit is shorter than a day.</exception>
    internal static BusinessDayConvention Convention(Period p) => p.Units switch
    {
        TimeUnit.Days or TimeUnit.Weeks => BusinessDayConvention.Following,
        TimeUnit.Months or TimeUnit.Years => BusinessDayConvention.ModifiedFollowing,
        _ => throw new ArgumentOutOfRangeException("tenor", p, "invalid time units"),
    };

    /// <summary>No end-of-month rule for days and weeks; the rule for months and years.</summary>
    /// <param name="p">The tenor.</param>
    /// <returns>Whether the end-of-month rule applies.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The tenor's unit is shorter than a day.</exception>
    internal static bool EndOfMonth(Period p) => p.Units switch
    {
        TimeUnit.Days or TimeUnit.Weeks => false,
        TimeUnit.Months or TimeUnit.Years => true,
        _ => throw new ArgumentOutOfRangeException("tenor", p, "invalid time units"),
    };
}
