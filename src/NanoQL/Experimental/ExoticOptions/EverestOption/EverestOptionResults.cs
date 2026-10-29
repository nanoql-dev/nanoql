// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/everestoption.hpp ql/experimental/exoticoptions/everestoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;

namespace NanoQL.Experimental;

/// <summary>
/// The results of an <see cref="EverestOption"/>'s engine: a multi-asset option's, with the yield the price implies,
/// <see langword="null"/> where the engine doesn't compute it.
/// </summary>
/// <remarks>
/// Where QuantLib's option throws "yield not provided" on reading a yield its engine left null, the member is
/// <see langword="null"/> and the caller checks it.
/// </remarks>
[Experimental("NQL0001")]
public sealed record EverestOptionResults : MultiAssetOptionResults
{
    /// <summary>Initializes a new instance of the <see cref="EverestOptionResults"/> class.</summary>
    public EverestOptionResults()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EverestOptionResults"/> class from a multi-asset
    /// option's results, the yield <see langword="null"/>.</summary>
    /// <param name="results">The multi-asset option's results.</param>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    public EverestOptionResults(MultiAssetOptionResults results)
        : base(results ?? throw new ArgumentNullException(nameof(results)))
    {
    }

    /// <summary>Gets the yield the price implies: the value over the discounted notional, less one.</summary>
    public double? Yield { get; init; }
}
