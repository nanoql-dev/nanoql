// SPDX-FileCopyrightText: Copyright (C) 2014 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/actual364.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Actual/364 day count convention.</summary>
public sealed class Actual364 : DayCounter
{
    /// <inheritdoc/>
    public override string Name => "Actual/364";

    /// <inheritdoc/>
    public override double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null)
    {
        RequireDates(d1, d2, refPeriodStart, refPeriodEnd);
        return DateFunctions.DaysBetween(d1, d2) / 364.0;
    }
}
