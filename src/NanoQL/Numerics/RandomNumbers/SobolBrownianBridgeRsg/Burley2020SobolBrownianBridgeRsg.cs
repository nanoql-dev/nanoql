// SPDX-FileCopyrightText: Copyright (C) 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/sobolbrownianbridgersg.hpp ql/math/randomnumbers/sobolbrownianbridgersg.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Models;

namespace NanoQL.Numerics;

/// <summary>
/// The Brownian-bridge sequence generator on Burley's scrambled Sobol sequence: each sequence a path of
/// <see cref="Burley2020SobolBrownianGenerator"/>'s, its steps' Gaussian variates one after another, the factors of
/// each step together.
/// </summary>
/// <remarks>
/// A working type, as every <see cref="IRsg"/>: each draw changes it. Both seeds are explicit, in [1, 4294967295];
/// QuantLib's defaults are 42 and 43. Every sequence's weight is 1. <see cref="Clone"/> builds the generator again from
/// its arguments and draws the paths this one has drawn, the scrambled sequence being deterministic, so the copy's next
/// draw is this one's.
/// </remarks>
[WorkingType]
public sealed class Burley2020SobolBrownianBridgeRsg : IRsg
{
    private readonly int factors;
    private readonly int steps;
    private readonly SobolBrownianGeneratorBaseOrdering ordering;
    private readonly long seed;
    private readonly SobolRsgDirectionIntegers directionIntegers;
    private readonly long scrambleSeed;
    private readonly Burley2020SobolBrownianGenerator gen;
    private readonly double[] seq;
    private readonly double[] output;
    private long paths;

    /// <summary>Initializes a new instance of the <see cref="Burley2020SobolBrownianBridgeRsg"/> class.</summary>
    /// <param name="factors">The number of factors each step draws, at least 1.</param>
    /// <param name="steps">The number of steps each sequence draws, at least 1.</param>
    /// <param name="ordering">How the variates are assigned to the factors and steps.</param>
    /// <param name="seed">The Sobol sequence's seed, in [1, 4294967295].</param>
    /// <param name="directionIntegers">The Sobol sequence's direction integers.</param>
    /// <param name="scrambleSeed">The scrambling's seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="factors"/> or <paramref name="steps"/> is below 1, their product is outside the sequence's
    /// dimensions, or the ordering, a seed or the direction integers is out of range.
    /// </exception>
    public Burley2020SobolBrownianBridgeRsg(
        int factors,
        int steps,
        SobolBrownianGeneratorBaseOrdering ordering,
        long seed,
        SobolRsgDirectionIntegers directionIntegers,
        long scrambleSeed)
    {
        gen = new Burley2020SobolBrownianGenerator(factors, steps, ordering, seed, directionIntegers, scrambleSeed);
        this.factors = factors;
        this.steps = steps;
        this.ordering = ordering;
        this.seed = seed;
        this.directionIntegers = directionIntegers;
        this.scrambleSeed = scrambleSeed;
        seq = new double[factors * steps];
        output = new double[factors];
    }

    /// <inheritdoc/>
    public int Dimension => gen.NumberOfFactors * gen.NumberOfSteps;

    /// <inheritdoc/>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        gen.NextPath();
        ++paths;
        for (int i = 0; i < gen.NumberOfSteps; ++i)
        {
            gen.NextStep(output);
            output.CopyTo(seq.AsSpan(i * gen.NumberOfFactors));
        }

        return LastSequence();
    }

    /// <inheritdoc/>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(seq, 1.0);

    /// <inheritdoc/>
    public IRsg Clone()
    {
        var copy = new Burley2020SobolBrownianBridgeRsg(factors, steps, ordering, seed, directionIntegers, scrambleSeed);
        for (long i = 0; i < paths; ++i)
        {
            copy.NextSequence();
        }

        seq.AsSpan().CopyTo(copy.seq);
        return copy;
    }
}
