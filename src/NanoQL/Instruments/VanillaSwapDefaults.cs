// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// The defaults <see cref="MakeVanillaSwap"/> takes for an option it isn't given, on an index of a currency, which
/// <see cref="ConventionProvider.ResolveVanillaSwapDefaults"/> returns: QuantLib's by the currency, which the builder
/// fixes. The floating leg's calendar, tenor, conventions and day counter are the index's, and the fixed leg's and the
/// settlement calendar the floating leg's, so they aren't defaults.
/// </summary>
/// <param name="FixedLegTenor">The fixed leg's tenor, or <see langword="null"/> where it is unknown for the currency,
/// which the builder refuses when it needs one.</param>
/// <param name="FixedLegTenorThreshold">The fixed leg's tenor at the swap tenors on one side of a threshold, in place of
/// <paramref name="FixedLegTenor"/>, or <see langword="null"/> for none. The builder compares the swap's tenor with it
/// when it needs its fixed leg's tenor, as QuantLib compares.</param>
/// <param name="FixedLegDayCounter">The fixed leg's day counter, or <see langword="null"/> where it is unknown for the
/// currency, which the builder refuses when it needs one.</param>
/// <param name="FixedLegConvention">The fixed leg's business-day convention.</param>
/// <param name="FixedLegTerminationDateConvention">The fixed leg's termination-date convention.</param>
/// <param name="FixedLegRule">The fixed leg's date-generation rule.</param>
/// <param name="FixedLegEndOfMonth">Whether the fixed leg's dates stay at month end.</param>
/// <param name="FloatingLegRule">The floating leg's date-generation rule.</param>
/// <param name="FloatingLegEndOfMonth">Whether the floating leg's dates stay at month end; the maturity's default
/// too.</param>
public sealed record VanillaSwapDefaults(
    Period? FixedLegTenor,
    SwapTenorThreshold? FixedLegTenorThreshold,
    DayCounter? FixedLegDayCounter,
    BusinessDayConvention FixedLegConvention,
    BusinessDayConvention FixedLegTerminationDateConvention,
    DateGenerationRule FixedLegRule,
    bool FixedLegEndOfMonth,
    DateGenerationRule FloatingLegRule,
    bool FloatingLegEndOfMonth);
