// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The Sobol Brownian generator for market-model simulations: each path a Sobol sequence of the factors times the
/// steps, turned into Gaussian variates by the inverse cumulative normal and Brownian-bridged by the base's ordering.
/// </summary>
/// <remarks>
/// A working type (the S-hes plan's HR3 (iv), (v)). Its seed is explicit, in [1, 4294967295], as
/// <see cref="SobolRsg"/> takes it: QuantLib's default 0 draws one from the clock.
/// </remarks>
public sealed class SobolBrownianGenerator : SobolBrownianGeneratorBase
{
    private readonly InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal> generator;

    /// <summary>Initializes a new instance of the <see cref="SobolBrownianGenerator"/> class.</summary>
    /// <param name="factors">The number of factors each step draws, at least 1.</param>
    /// <param name="steps">The number of steps each path draws, at least 1.</param>
    /// <param name="ordering">How the variates are assigned to the factors and steps.</param>
    /// <param name="seed">The Sobol sequence's seed, in [1, 4294967295].</param>
    /// <param name="directionIntegers">The Sobol sequence's direction integers.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="factors"/> or <paramref name="steps"/> is below 1, their product is outside the Sobol
    /// sequence's dimensions, or <paramref name="ordering"/>, <paramref name="seed"/> or
    /// <paramref name="directionIntegers"/> is out of range.
    /// </exception>
    public SobolBrownianGenerator(
        int factors,
        int steps,
        SobolBrownianGeneratorBaseOrdering ordering,
        long seed,
        SobolRsgDirectionIntegers directionIntegers = SobolRsgDirectionIntegers.Jaeckel)
        : base(factors, steps, ordering)
    {
        generator = new InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>(
            new SobolRsg(checked(factors * steps), seed, directionIntegers), new InverseCumulativeNormal(), adopt: true);
    }

    /// <inheritdoc/>
    protected override Sample<ReadOnlyMemory<double>> NextSequence() => generator.NextSequence();
}
