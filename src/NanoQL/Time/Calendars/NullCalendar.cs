// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendars/nullcalendar.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>
/// A calendar for reproducing theoretical calculations: it has no holidays and no weekends, so dates at whole-month
/// distances have the same day of month.
/// </summary>
public sealed class NullCalendar : Calendar
{
    /// <inheritdoc/>
    public override string Name => "Null";

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => false;

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date d) => true;
}
