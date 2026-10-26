// SPDX-FileCopyrightText: Copyright (C) 2010 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/margrabeoption.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results of a <see cref="MargrabeOption"/>'s engine: a multi-asset option's, with each asset's delta and gamma,
/// each <see langword="null"/> where the engine doesn't compute it.
/// </summary>
/// <remarks>
/// Where QuantLib's option throws "delta1 not provided" and its siblings on reading a Greek its engine left null, the
/// member is <see langword="null"/> and the caller checks it.
/// </remarks>
public sealed record MargrabeOptionResults : MultiAssetOptionResults
{
    /// <summary>Initializes a new instance of the <see cref="MargrabeOptionResults"/> class.</summary>
    public MargrabeOptionResults()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MargrabeOptionResults"/> class from a multi-asset
    /// option's results, the assets' deltas and gammas <see langword="null"/>.</summary>
    /// <param name="results">The multi-asset option's results.</param>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    public MargrabeOptionResults(MultiAssetOptionResults results)
        : base(results ?? throw new ArgumentNullException(nameof(results)))
    {
    }

    /// <summary>Gets the sensitivity to the first asset's spot.</summary>
    public double? Delta1 { get; init; }

    /// <summary>Gets the sensitivity to the second asset's spot.</summary>
    public double? Delta2 { get; init; }

    /// <summary>Gets the second-order sensitivity to the first asset's spot.</summary>
    public double? Gamma1 { get; init; }

    /// <summary>Gets the second-order sensitivity to the second asset's spot.</summary>
    public double? Gamma2 { get; init; }
}
