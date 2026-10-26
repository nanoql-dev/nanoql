// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/quantovanillaoption.hpp ql/instruments/quantovanillaoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// The results of a quanto option: a one-asset option's, with the sensitivities to the exchange rate's volatility, the
/// foreign rate and the correlation.
/// </summary>
/// <remarks>
/// Immutable. QuantLib's template over its results collapses to one record, every quanto instrument's results being
/// <see cref="OneAssetOptionResults"/> (the S-exo plan's ER2 (i)); a sensitivity the engine doesn't compute is
/// <see langword="null"/>, where QuantLib's accessors refuse it.
/// </remarks>
public sealed record QuantoOptionResults : OneAssetOptionResults
{
    /// <summary>Initializes a new instance of the <see cref="QuantoOptionResults"/> class.</summary>
    public QuantoOptionResults()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="QuantoOptionResults"/> class from a one-asset option's
    /// results, the quanto sensitivities <see langword="null"/>.</summary>
    /// <param name="results">The one-asset option's results.</param>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    public QuantoOptionResults(OneAssetOptionResults results)
        : base(results ?? throw new ArgumentNullException(nameof(results)))
    {
    }

    /// <summary>Gets the sensitivity to the exchange rate's volatility.</summary>
    public double? Qvega { get; init; }

    /// <summary>Gets the sensitivity to the foreign risk-free rate.</summary>
    public double? Qrho { get; init; }

    /// <summary>Gets the sensitivity to the correlation of the asset and the exchange rate.</summary>
    public double? Qlambda { get; init; }
}
