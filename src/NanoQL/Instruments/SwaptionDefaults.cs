// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// The defaults <see cref="MakeSwaption"/> takes for an option it isn't given, which
/// <see cref="ConventionProvider.ResolveSwaptionDefaults"/> returns: QuantLib's. The exercise calendar and the
/// underlying swap are the swap index's, so they aren't defaults.
/// </summary>
/// <param name="SettlementType">The settlement type.</param>
/// <param name="SettlementMethod">The settlement method.</param>
/// <param name="OptionConvention">The business-day convention the option tenor is advanced by.</param>
public sealed record SwaptionDefaults(
    SettlementType SettlementType,
    SettlementMethod SettlementMethod,
    BusinessDayConvention OptionConvention);
