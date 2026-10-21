// SPDX-FileCopyrightText: Copyright (C) 2026 Chirag Desai
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/fxforward.hpp ql/instruments/fxforward.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results of an <see cref="FxForward"/>'s engine: the instrument's results, with the NPV in the source currency,
/// and the fair forward rate and the NPV in each currency.
/// </summary>
/// <remarks>Where QuantLib throws "fair forward rate not available" and "NPV in … currency not available" on reading a
/// null result, the caller checks for the null.</remarks>
public sealed record FxForwardResults : InstrumentResults
{
    /// <summary>Gets the fair forward rate, units of the target currency per unit of the source, the market-implied
    /// rate the engine computes.</summary>
    public double? FairForwardRate { get; init; }

    /// <summary>Gets the NPV in the source currency.</summary>
    public double? NpvSourceCurrency { get; init; }

    /// <summary>Gets the NPV in the target currency.</summary>
    public double? NpvTargetCurrency { get; init; }
}
