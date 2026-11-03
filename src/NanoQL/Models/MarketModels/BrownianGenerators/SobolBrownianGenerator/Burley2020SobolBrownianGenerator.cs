// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The Brownian generator on Burley's scrambled Sobol sequence for market-model simulations: each path a scrambled
/// Sobol sequence of the factors times the steps, turned into Gaussian variates by the inverse cumulative normal and
/// Brownian-bridged by the base's ordering.
/// </summary>
/// <remarks>
/// A working type. Both seeds are explicit, in [1, 4294967295], as <see cref="Burley2020SobolRsg"/> takes them;
/// QuantLib's defaults are 42 and 43.
/// </remarks>
[WorkingType]
public sealed class Burley2020SobolBrownianGenerator : SobolBrownianGeneratorBase
{
    private readonly InverseCumulativeRsg<Burley2020SobolRsg, InverseCumulativeNormal> generator;

    /// <summary>Initializes a new instance of the <see cref="Burley2020SobolBrownianGenerator"/> class.</summary>
    /// <param name="factors">The number of factors each step draws, at least 1.</param>
    /// <param name="steps">The number of steps each path draws, at least 1.</param>
    /// <param name="ordering">How the variates are assigned to the factors and steps.</param>
    /// <param name="seed">The Sobol sequence's seed, in [1, 4294967295].</param>
    /// <param name="directionIntegers">The Sobol sequence's direction integers.</param>
    /// <param name="scrambleSeed">The scrambling's seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="factors"/> or <paramref name="steps"/> is below 1, their product is outside the sequence's
    /// dimensions, or the ordering, a seed or the direction integers is out of range.
    /// </exception>
    public Burley2020SobolBrownianGenerator(
        int factors,
        int steps,
        SobolBrownianGeneratorBaseOrdering ordering,
        long seed,
        SobolRsgDirectionIntegers directionIntegers,
        long scrambleSeed)
        : base(factors, steps, ordering)
    {
        generator = new InverseCumulativeRsg<Burley2020SobolRsg, InverseCumulativeNormal>(
            new Burley2020SobolRsg(checked(factors * steps), seed, directionIntegers, scrambleSeed),
            new InverseCumulativeNormal(),
            adopt: true);
    }

    /// <inheritdoc/>
    protected override Sample<ReadOnlyMemory<double>> NextSequence() => generator.NextSequence();
}
