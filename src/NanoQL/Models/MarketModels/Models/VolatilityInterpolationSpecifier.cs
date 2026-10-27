// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/volatilityinterpolationspecifier.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// How the volatility structure of synthetic short rates interleaved between long rates is decided: the short rates'
/// variances interpolated from the long rates', each long rate's scaled, the last short rate's matching a caplet
/// volatility.
/// </summary>
/// <remarks>
/// Immutable (the S-mm plan's MR2 (c)): QuantLib's <c>setScalingFactors</c> and <c>setLastCapletVol</c> are
/// <see cref="WithScalingFactors"/> and <see cref="WithLastCapletVol"/>, each returning a new specifier and leaving
/// this one unchanged. An extension point, as in QuantLib.
/// </remarks>
public abstract class VolatilityInterpolationSpecifier
{
    /// <summary>Initializes a new instance of the <see cref="VolatilityInterpolationSpecifier"/> class.</summary>
    protected VolatilityInterpolationSpecifier()
    {
    }

    /// <summary>Gets the short rates' interpolated variances, one per short rate.</summary>
    public abstract ImmutableArray<PiecewiseConstantVariance> InterpolatedVariances { get; }

    /// <summary>Gets the long rates' variances, one per long rate.</summary>
    public abstract ImmutableArray<PiecewiseConstantVariance> OriginalVariances { get; }

    /// <summary>Gets the short rates each long rate spans.</summary>
    public abstract int Period { get; }

    /// <summary>Gets the short rates before the first long rate.</summary>
    public abstract int Offset { get; }

    /// <summary>Gets the number of long rates.</summary>
    public abstract int NoBigRates { get; }

    /// <summary>Gets the number of short rates.</summary>
    public abstract int NoSmallRates { get; }

    /// <summary>A specifier whose long rates' variances are scaled by the factors given.</summary>
    /// <param name="scales">The scaling factors, one per long rate.</param>
    /// <returns>The new specifier.</returns>
    public abstract VolatilityInterpolationSpecifier WithScalingFactors(ReadOnlySpan<double> scales);

    /// <summary>A specifier whose last short rate matches the caplet volatility given.</summary>
    /// <param name="vol">The last caplet's volatility.</param>
    /// <returns>The new specifier.</returns>
    public abstract VolatilityInterpolationSpecifier WithLastCapletVol(double vol);
}
