// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// A swap index family's conventions at a tenor, which <see cref="ConventionProvider.ResolveSwapIndex"/> returns:
/// QuantLib's values for the family, which its class builds itself on.
/// </summary>
/// <remarks>
/// The Ibor index comes on the swap index's forwarding curve, which QuantLib's reference builds it on, so a provider
/// that names another index builds it on that curve (<see cref="IborIndex.ForwardingTermStructure"/>).
/// </remarks>
/// <param name="SettlementDays">The settlement days.</param>
/// <param name="Currency">The index's currency.</param>
/// <param name="FixingCalendar">The fixing calendar.</param>
/// <param name="FixedLegTenor">The fixed leg's tenor.</param>
/// <param name="FixedLegConvention">The fixed leg's business-day convention.</param>
/// <param name="FixedLegDayCounter">The fixed leg's day counter.</param>
/// <param name="IborIndex">The floating leg's Ibor index, on the forwarding curve.</param>
public sealed record SwapIndexConventions(
    int SettlementDays,
    Currency Currency,
    Calendar FixingCalendar,
    Period FixedLegTenor,
    BusinessDayConvention FixedLegConvention,
    DayCounter FixedLegDayCounter,
    IborIndex IborIndex);
