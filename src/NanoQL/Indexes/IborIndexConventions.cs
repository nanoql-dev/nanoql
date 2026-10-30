// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// An Ibor, Libor, overnight or BMA index preset's conventions, which <see cref="ConventionProvider.ResolveIborIndex"/>,
/// <see cref="ConventionProvider.ResolveIborFamily"/> and <see cref="ConventionProvider.ResolveOvernightIndex"/>
/// return: QuantLib's values for the preset, which its class builds itself on.
/// </summary>
/// <remarks>
/// A Libor's fixing calendar is London's, and its financial-centre calendar the currency's, which its class joins to
/// London's for its value and maturity dates, as QuantLib's does; a EUR Libor's is TARGET. An overnight index and the
/// BMA index fix their convention and end of month, so a preset refuses conventions that change them.
/// </remarks>
/// <param name="FixingDays">The fixing (settlement) days.</param>
/// <param name="Currency">The index's currency.</param>
/// <param name="FixingCalendar">The fixing calendar; a Libor's, London's.</param>
/// <param name="Convention">The business-day convention.</param>
/// <param name="EndOfMonth">Whether the end-of-month rule applies.</param>
/// <param name="DayCounter">The day counter.</param>
/// <param name="FinancialCenterCalendar">A Libor's financial-centre calendar, or <see langword="null"/> for any other
/// index.</param>
public sealed record IborIndexConventions(
    int FixingDays,
    Currency Currency,
    Calendar FixingCalendar,
    BusinessDayConvention Convention,
    bool EndOfMonth,
    DayCounter DayCounter,
    Calendar? FinancialCenterCalendar = null);
