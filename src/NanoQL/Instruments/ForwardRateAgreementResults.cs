// SPDX-FileCopyrightText: Copyright (C) 2006 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/forwardrateagreement.hpp ql/instruments/forwardrateagreement.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results of a <see cref="ForwardRateAgreement"/>'s calculation: the instrument's results, with the NPV the
/// payoff discounted from the value date, and the payoff and the forward rate.
/// </summary>
/// <remarks>QuantLib's <c>amount()</c> and <c>forwardRate()</c>, which compute on reading, are results here. A result
/// that can't be computed is <see langword="null"/>: an expired FRA's under the par approximation, whose curve can't
/// reach its dates.</remarks>
public sealed record ForwardRateAgreementResults : InstrumentResults
{
    /// <summary>Gets the payoff on the value date.</summary>
    public double? Amount { get; init; }

    /// <summary>Gets the forward rate over the FRA's term: the index fixing, or the par approximation's simple rate
    /// from the value date to the maturity date.</summary>
    public InterestRate? ForwardRate { get; init; }
}
