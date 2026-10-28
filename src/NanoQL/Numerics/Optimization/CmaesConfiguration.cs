// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/cmaes.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>The CMA-ES optimizer's configuration, QuantLib's <c>Cmaes::Configuration</c>.</summary>
/// <remarks>
/// Immutable, with QuantLib's defaults: each <c>With…</c> method returns a modified copy, where QuantLib's setters
/// change the configuration and return it (the S-core plan's CD12). QuantLib's <c>seed</c> isn't here: the seed is
/// the optimizer's own argument, required (<see cref="Cmaes"/>). Its vectors compare by reference, so two
/// configurations are equal only when they share them.
/// </remarks>
public sealed record CmaesConfiguration
{
    /// <summary>Gets the population size λ; 0 by default, for 4 + ⌊3 ln n⌋.</summary>
    public int PopulationSize { get; private init; }

    /// <summary>Gets the initial step size, in x-space units; 0.3 by default.</summary>
    public double Sigma { get; private init; } = 0.3;

    /// <summary>Gets the lower bound; empty by default, for the constraint's.</summary>
    public ImmutableArray<double> LowerBound { get; private init; } = [];

    /// <summary>Gets the upper bound; empty by default, for the constraint's.</summary>
    public ImmutableArray<double> UpperBound { get; private init; } = [];

    /// <summary>Gets the initial mean; empty by default, for the problem's current value.</summary>
    public ImmutableArray<double> InitialMean { get; private init; } = [];

    /// <summary>Returns a copy with the population size.</summary>
    /// <param name="n">The size, not negative; 0 for the default.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    public CmaesConfiguration WithPopulationSize(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        return this with { PopulationSize = n };
    }

    /// <summary>Returns a copy with the initial step size.</summary>
    /// <param name="s">The step size, positive.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException"><paramref name="s"/> isn't positive.</exception>
    public CmaesConfiguration WithSigma(double s)
    {
        if (!(s > 0.0))
        {
            throw new ArgumentException("sigma must be positive", nameof(s));
        }

        return this with { Sigma = s };
    }

    /// <summary>Returns a copy with box bounds, which override the constraint's.</summary>
    /// <param name="lo">The lower bound, which it copies.</param>
    /// <param name="up">The upper bound, which it copies.</param>
    /// <returns>The copy.</returns>
    public CmaesConfiguration WithBounds(ReadOnlySpan<double> lo, ReadOnlySpan<double> up) =>
        this with { LowerBound = [.. lo], UpperBound = [.. up] };

    /// <summary>Returns a copy with the initial mean.</summary>
    /// <param name="m">The mean, which it copies.</param>
    /// <returns>The copy.</returns>
    public CmaesConfiguration WithInitialMean(ReadOnlySpan<double> m) => this with { InitialMean = [.. m] };
}
