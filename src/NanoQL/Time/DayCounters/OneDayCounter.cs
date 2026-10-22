// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/one.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>1/1 day count convention.</summary>
public sealed class OneDayCounter : DayCounter
{
    /// <inheritdoc/>
    public override string Name => "1/1";

    /// <inheritdoc/>
    public override int DayCount(Date d1, Date d2) =>
        // the sign is all we need
        d2 >= d1 ? 1 : -1;

    /// <inheritdoc/>
    public override double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null)
    {
        RequireDates(d1, d2, refPeriodStart, refPeriodEnd);
        return DayCount(d1, d2);
    }
}
