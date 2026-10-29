// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/mcpagodaengine.hpp ql/experimental/exoticoptions/mcpagodaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>The Pagoda option's path pricer: a fraction of the portfolio's average performance over the periods,
/// capped at the roof and floored at 0, discounted.</summary>
/// <remarks>Immutable; a path prices without allocating.</remarks>
[Experimental("NQL0001")]
public sealed class PagodaMultiPathPricer : PathPricer<MultiPath>
{
    private readonly double discount;
    private readonly double roof;
    private readonly double fraction;

    /// <summary>Initializes a new instance of the <see cref="PagodaMultiPathPricer"/> class.</summary>
    /// <param name="roof">The cap on the performance.</param>
    /// <param name="fraction">The fraction of the capped performance paid.</param>
    /// <param name="discount">The discount factor to the exercise date.</param>
    public PagodaMultiPathPricer(double roof, double fraction, double discount)
    {
        this.discount = discount;
        this.roof = roof;
        this.fraction = fraction;
    }

    /// <summary>The discounted payoff on the paths' performance.</summary>
    /// <param name="multiPath">The assets' paths.</param>
    /// <returns>The path's value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multiPath"/> is <see langword="null"/>.</exception>
    public override double Evaluate(MultiPath multiPath)
    {
        ArgumentNullException.ThrowIfNull(multiPath);
        int numAssets = multiPath.AssetNumber;
        int numSteps = multiPath.PathSize();
        double averagePerformance = 0.0;
        for (int i = 1; i < numSteps; i++)
        {
            for (int j = 0; j < numAssets; j++)
            {
                averagePerformance += multiPath[j].Front() * ((multiPath[j][i] / multiPath[j][i - 1]) - 1.0);
            }
        }

        averagePerformance /= numAssets;
        return discount * fraction * Math.Max(0.0, Math.Min(roof, averagePerformance));
    }
}
