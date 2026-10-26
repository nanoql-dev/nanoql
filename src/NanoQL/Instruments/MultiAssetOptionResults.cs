// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/multiassetoption.hpp ql/instruments/multiassetoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results of a <see cref="MultiAssetOption"/>'s engine: the instrument's results and QuantLib's Greeks, each
/// <see langword="null"/> where the engine doesn't compute it.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib's results class: the results of options derived from
/// <see cref="MultiAssetOption"/> derive from it (Margrabe's option adds its two deltas and gammas). QuantLib's
/// <c>Greeks</c> are its members.
/// </para>
/// <para>
/// Where QuantLib's option throws "&lt;greek&gt; not provided" on reading a Greek its engine left null, the member
/// is <see langword="null"/> and the caller checks it, as <see cref="InstrumentResults"/> documents for every result.
/// </para>
/// </remarks>
public record MultiAssetOptionResults : InstrumentResults
{
    /// <summary>Gets the sensitivity to the spot.</summary>
    public double? Delta { get; init; }

    /// <summary>Gets the second-order sensitivity to the spot.</summary>
    public double? Gamma { get; init; }

    /// <summary>Gets the sensitivity to time.</summary>
    public double? Theta { get; init; }

    /// <summary>Gets the sensitivity to the volatility.</summary>
    public double? Vega { get; init; }

    /// <summary>Gets the sensitivity to the risk-free rate.</summary>
    public double? Rho { get; init; }

    /// <summary>Gets the sensitivity to the dividend yield.</summary>
    public double? DividendRho { get; init; }
}
