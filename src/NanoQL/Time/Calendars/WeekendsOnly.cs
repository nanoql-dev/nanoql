// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/weekendsonly.hpp ql/time/calendars/weekendsonly.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>The weekends-only calendar.</summary>
/// <remarks>
/// This calendar has no bank holidays except for weekends (Saturdays and Sundays) as required by ISDA for calculating
/// conventional CDS spreads.
/// </remarks>
public sealed class WeekendsOnly : Calendar
{
    /// <summary>Initializes a new instance of the <see cref="WeekendsOnly"/> class.</summary>
    public WeekendsOnly()
    {
        ApplyEdits(ConventionProvider.ReadCalendarEdits("WeekendsOnly"));
    }

    /// <inheritdoc/>
    public override string Name => "weekends only";

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => WesternImpl.IsWeekend(w);

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date date) => !WesternImpl.IsWeekend(date.Weekday);
}
