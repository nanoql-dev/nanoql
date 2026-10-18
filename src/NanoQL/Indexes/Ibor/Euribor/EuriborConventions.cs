// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Katiuscia Manzoni
// SPDX-FileCopyrightText: Copyright (C) 2006 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2009 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/euribor.hpp ql/indexes/ibor/euribor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>The conventions Euribor's tenors follow, shared by <see cref="Euribor"/> and <see cref="Euribor365"/>.</summary>
internal static class EuriborConventions
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

    /// <summary>Refuses a daily tenor, which QuantLib's dedicated daily-tenor constructor serves.</summary>
    /// <param name="index">The index, with its normalized tenor.</param>
    /// <exception cref="ArgumentException">The tenor is in days.</exception>
    internal static void CheckTenor(InterestRateIndex index)
    {
        if (index.Tenor.Units == TimeUnit.Days)
        {
            throw new ArgumentException(
                $"for daily tenors ({index.Tenor}) dedicated DailyTenor constructor must be used", "tenor");
        }
    }
}
