// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/oneassetoption.hpp ql/instruments/oneassetoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results of a <see cref="OneAssetOption"/>'s engine: the instrument's results and QuantLib's Greeks and more
/// Greeks, each <see langword="null"/> where the engine doesn't compute it.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib's results class: the results of options derived from
/// <see cref="OneAssetOption"/> derive from it. QuantLib's <c>Greeks</c> and <c>MoreGreeks</c> are replaced by its
/// members.
/// </para>
/// <para>
/// Where QuantLib's option throws "&lt;greek&gt; not provided" on reading a Greek its engine left null, the member
/// is <see langword="null"/> and the caller checks it, as <see cref="InstrumentResults"/> documents for every result.
/// </para>
/// </remarks>
public record OneAssetOptionResults : InstrumentResults
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

    /// <summary>Gets the sensitivity to the forward.</summary>
    public double? DeltaForward { get; init; }

    /// <summary>Gets the sensitivity in percent to a percent change of the spot.</summary>
    public double? Elasticity { get; init; }

    /// <summary>Gets the sensitivity to time, per day.</summary>
    public double? ThetaPerDay { get; init; }

    /// <summary>Gets the sensitivity to the strike.</summary>
    public double? StrikeSensitivity { get; init; }

    /// <summary>Gets the probability of expiring in the money in the bond martingale measure.</summary>
    public double? ItmCashProbability { get; init; }
}
