// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/fireflyalgorithm.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The normal distribution as a random walk's radius, <c>std::normal_distribution&lt;Real&gt;(mean, sigma)</c> drawn as
/// MSVC 14.44 draws it (the S-xrt plan's XRD4): the distribution of QuantLib's <c>GaussianWalk</c>.
/// </summary>
/// <remarks>
/// Knuth's polar Algorithm P, its pairs' second value kept for the next draw, so the distribution is a working type:
/// each draw changes it, and it is not thread-safe.
/// </remarks>
[Experimental("NQL0001")]
public sealed class NormalRandomWalkDistribution : IRandomWalkDistribution
{
    private readonly MsvcNormalDistribution distribution;

    /// <summary>Initializes a new instance of the <see cref="NormalRandomWalkDistribution"/> class.</summary>
    /// <param name="mean">The mean.</param>
    /// <param name="sigma">The standard deviation.</param>
    public NormalRandomWalkDistribution(double mean, double sigma)
    {
        Mean = mean;
        Sigma = sigma;
        distribution = new MsvcNormalDistribution(mean, sigma);
    }

    private NormalRandomWalkDistribution(NormalRandomWalkDistribution other)
    {
        Mean = other.Mean;
        Sigma = other.Sigma;
        distribution = other.distribution.Clone();
    }

    /// <summary>Gets the mean.</summary>
    public double Mean { get; }

    /// <summary>Gets the standard deviation.</summary>
    public double Sigma { get; }

    /// <inheritdoc/>
    public double Next(MersenneTwisterUniformRng engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return distribution.Next(engine);
    }

    /// <inheritdoc/>
    public IRandomWalkDistribution Clone() => new NormalRandomWalkDistribution(this);
}
