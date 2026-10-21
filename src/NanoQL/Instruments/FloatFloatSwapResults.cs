// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/floatfloatswap.hpp ql/instruments/floatfloatswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>The results an engine returns for a float-float swap: the swap's, with each leg's fair spread.</summary>
/// <remarks>
/// A result QuantLib leaves null is <see langword="null"/>, where QuantLib throws "fair spread 1 not available" or
/// "fair spread 2 not available" on reading it: a leg's fair spread when its BPS is zero or unknown, and both for an
/// expired swap. Equality compares the per-leg arrays element by element, and the fair spreads by value.
/// </remarks>
public sealed record FloatFloatSwapResults : SwapResults
{
    /// <summary>Initializes a new instance of the <see cref="FloatFloatSwapResults"/> class.</summary>
    public FloatFloatSwapResults()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FloatFloatSwapResults"/> class with a swap's results, and no fair
    /// spreads.
    /// </summary>
    /// <param name="results">The swap's results.</param>
    internal FloatFloatSwapResults(SwapResults results)
        : base(results)
    {
    }

    /// <summary>Gets the first leg's spread that gives the swap a zero NPV.</summary>
    public double? FairSpread1 { get; init; }

    /// <summary>Gets the second leg's spread that gives the swap a zero NPV.</summary>
    public double? FairSpread2 { get; init; }

    /// <inheritdoc/>
    public bool Equals(FloatFloatSwapResults? other) =>
        other is not null
        && base.Equals(other)
        && EqualityComparer<double?>.Default.Equals(FairSpread1, other.FairSpread1)
        && EqualityComparer<double?>.Default.Equals(FairSpread2, other.FairSpread2);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), FairSpread1, FairSpread2);
}
