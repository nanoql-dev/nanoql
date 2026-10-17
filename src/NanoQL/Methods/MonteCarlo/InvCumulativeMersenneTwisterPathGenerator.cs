// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/pathgenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// A path generator over QuantLib's pseudo-random Gaussian sequences, inverse-cumulative normal deviates of
/// Mersenne-Twister uniform sequences (<c>PseudoRandom::rsg_type</c>): QuantLib-Python's
/// <c>InvCumulativeMersenneTwisterPathGenerator</c>, which it also names <c>GaussianPathGenerator</c>.
/// </summary>
/// <remarks>A convenience: nanoql's own code uses <see cref="PathGenerator{TGsg}"/>.</remarks>
public sealed class InvCumulativeMersenneTwisterPathGenerator
    : PathGenerator<InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvCumulativeMersenneTwisterPathGenerator"/> class over a regular
    /// time grid from 0 to <paramref name="length"/>.
    /// </summary>
    /// <param name="process">The process, which must be 1-D.</param>
    /// <param name="length">The length of the paths, positive.</param>
    /// <param name="timeSteps">The number of steps, the generator's dimension.</param>
    /// <param name="generator">The Gaussian sequence generator, which the path generator copies.</param>
    /// <param name="brownianBridge">Whether to build the paths as Brownian bridges.</param>
    /// <exception cref="ArgumentException">
    /// The process isn't a <see cref="StochasticProcess1D"/>, the length isn't positive, or the generator's dimension
    /// isn't <paramref name="timeSteps"/>.
    /// </exception>
    public InvCumulativeMersenneTwisterPathGenerator(
        StochasticProcess process,
        double length,
        int timeSteps,
        InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal> generator,
        bool brownianBridge)
        : base(process, length, timeSteps, generator, brownianBridge)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvCumulativeMersenneTwisterPathGenerator"/> class over a time grid.</summary>
    /// <param name="process">The process, which must be 1-D.</param>
    /// <param name="timeGrid">The time grid, whose steps number the generator's dimension.</param>
    /// <param name="generator">The Gaussian sequence generator, which the path generator copies.</param>
    /// <param name="brownianBridge">Whether to build the paths as Brownian bridges.</param>
    /// <exception cref="ArgumentException">
    /// The process isn't a <see cref="StochasticProcess1D"/>, or the generator's dimension isn't the number of the
    /// grid's steps.
    /// </exception>
    public InvCumulativeMersenneTwisterPathGenerator(
        StochasticProcess process,
        TimeGrid timeGrid,
        InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativeNormal> generator,
        bool brownianBridge)
        : base(process, timeGrid, generator, brownianBridge)
    {
    }

    private InvCumulativeMersenneTwisterPathGenerator(InvCumulativeMersenneTwisterPathGenerator source)
        : base(source)
    {
    }

    /// <summary>
    /// Copies the path generator deeply, its sequence generator included: the copy's next path equals this one's, into
    /// its own path, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override InvCumulativeMersenneTwisterPathGenerator Clone() => new(this);
}
