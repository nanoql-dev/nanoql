// SPDX-FileCopyrightText: Copyright (C) 2022 Ignacio Anguita
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/actual366.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>Actual/366 day count convention, also known as "Act/366".</summary>
public sealed class Actual366 : DayCounter
{
    private readonly bool includeLastDay;

    /// <summary>Initializes a new instance of the <see cref="Actual366"/> class.</summary>
    /// <param name="includeLastDay">Whether the last day of the period is counted, adding one day.</param>
    public Actual366(bool includeLastDay = false)
    {
        this.includeLastDay = includeLastDay;
    }

    /// <inheritdoc/>
    public override string Name => includeLastDay ? "Actual/366 (inc)" : "Actual/366";

    /// <inheritdoc/>
    public override int DayCount(Date d1, Date d2) => (d2 - d1) + (includeLastDay ? 1 : 0);

    /// <inheritdoc/>
    public override double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null)
    {
        RequireDates(d1, d2, refPeriodStart, refPeriodEnd);
        return (DateFunctions.DaysBetween(d1, d2) + (includeLastDay ? 1.0 : 0.0)) / 366.0;
    }
}
