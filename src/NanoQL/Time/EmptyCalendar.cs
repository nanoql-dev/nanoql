// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2020 Leonardo Arcari
// SPDX-FileCopyrightText: Copyright (C) 2020 Kline s.r.l.
// SPDX-FileCopyrightText: Copyright (C) 2004 Jeff Yu
// SPDX-FileCopyrightText: Copyright (C) 2014 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/calendar.hpp ql/time/calendar.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>
/// QuantLib's empty calendar, <c>Calendar()</c>, a calendar with no implementation: what a member reads where it is
/// given <see langword="null"/> for a calendar that QuantLib uses unchecked.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Calendar.Adjust"/> and <see cref="Calendar.Advance(Date, int, TimeUnit, BusinessDayConvention, bool)"/>
/// return before they read the implementation for <see cref="BusinessDayConvention.Unadjusted"/> and for moves in
/// weeks, months and years, as QuantLib's do, so those uses complete; every other use (a business-day check, a move in
/// days, any other convention, the name) throws QuantLib's "no calendar implementation provided"
/// (<c>calendar.hpp:213, 230</c>).
/// </para>
/// <para>
/// It is never returned: where QuantLib returns its empty calendar, nanoql returns <see langword="null"/>. Two empty
/// calendars are equal, and an empty calendar differs from every other, without reading a name
/// (<c>calendar.hpp:274-277</c>).
/// </para>
/// </remarks>
internal sealed class EmptyCalendar : Calendar
{
    private const string NoImplementation = "no calendar implementation provided";

    private EmptyCalendar()
    {
    }

    /// <summary>Gets the one empty calendar.</summary>
    public static EmptyCalendar Instance { get; } = new();

    /// <inheritdoc/>
    public override string Name => throw new NanoQLException(NoImplementation);

    /// <inheritdoc/>
    public override bool IsWeekend(Weekday w) => throw new NanoQLException(NoImplementation);

    /// <inheritdoc/>
    public override bool Equals(Calendar? other) => other is EmptyCalendar;

    /// <inheritdoc/>
    public override int GetHashCode() => 0;

    /// <inheritdoc/>
    protected override bool IsBusinessDayCore(Date d) => throw new NanoQLException(NoImplementation);
}
