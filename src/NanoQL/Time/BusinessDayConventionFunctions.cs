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

/// <summary>QuantLib's free functions for business-day conventions (<c>businessdayconvention.hpp</c>).</summary>
public static class BusinessDayConventionFunctions
{
    /// <summary>
    /// A convention's text, as QuantLib writes it; it differs from the member name for three members ("Modified
    /// Following", "Half-Month Modified Following", "Modified Preceding").
    /// </summary>
    /// <param name="b">The convention.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value isn't a business-day convention.</exception>
    public static string ToString(BusinessDayConvention b) => b switch
    {
        BusinessDayConvention.Following => "Following",
        BusinessDayConvention.ModifiedFollowing => "Modified Following",
        BusinessDayConvention.HalfMonthModifiedFollowing => "Half-Month Modified Following",
        BusinessDayConvention.Preceding => "Preceding",
        BusinessDayConvention.ModifiedPreceding => "Modified Preceding",
        BusinessDayConvention.Unadjusted => "Unadjusted",
        BusinessDayConvention.Nearest => "Nearest",
        _ => throw new ArgumentOutOfRangeException(nameof(b), b, $"unknown BusinessDayConvention ({(int)b})"),
    };
}
