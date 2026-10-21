// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/ibor/libor.hpp ql/indexes/ibor/libor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// QuantLib's <c>liborConvention</c> and <c>liborEOM</c> (<c>libor.cpp</c>'s anonymous namespace), which
/// <see cref="Libor"/> and <see cref="DailyTenorLibor"/> share.
/// </summary>
internal static class LiborConventions
{
    // QuantLib's liborConvention: Following for days and weeks, ModifiedFollowing for months and years.
    internal static BusinessDayConvention Convention(Period tenor) => tenor.Units switch
    {
        TimeUnit.Days or TimeUnit.Weeks => BusinessDayConvention.Following,
        TimeUnit.Months or TimeUnit.Years => BusinessDayConvention.ModifiedFollowing,
        _ => throw new ArgumentException("invalid time units", nameof(tenor)),
    };

    // QuantLib's liborEOM: false for days and weeks, true for months and years.
    internal static bool EndOfMonth(Period tenor) => tenor.Units switch
    {
        TimeUnit.Days or TimeUnit.Weeks => false,
        TimeUnit.Months or TimeUnit.Years => true,
        _ => throw new ArgumentException("invalid time units", nameof(tenor)),
    };
}
