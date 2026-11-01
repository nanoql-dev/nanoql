// SPDX-FileCopyrightText: Copyright (C) 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/sobolbrownianbridgersg.hpp ql/math/randomnumbers/sobolbrownianbridgersg.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Models;

namespace NanoQL.Numerics;

/// <summary>
/// The Sobol Brownian-bridge sequence generator: each sequence a path of <see cref="SobolBrownianGenerator"/>'s, its
/// steps' Gaussian variates one after another, the factors of each step together.
/// </summary>
/// <remarks>
/// A working type, as every <see cref="IRsg"/>: each draw changes it. Its seed is explicit, in [1, 4294967295], as
/// <see cref="SobolBrownianGenerator"/> takes it: QuantLib's default 0 draws one from the clock. Every sequence's
/// weight is 1. <see cref="Clone"/> builds the generator again from its arguments and draws the paths this one has
/// drawn, the Sobol sequence being deterministic, so the copy's next draw is this one's.
/// </remarks>
[WorkingType]
public sealed class SobolBrownianBridgeRsg : IRsg
{
    private readonly int factors;
    private readonly int steps;
    private readonly SobolBrownianGeneratorBaseOrdering ordering;
    private readonly long seed;
    private readonly SobolRsgDirectionIntegers directionIntegers;
    private readonly SobolBrownianGenerator gen;
    private readonly double[] seq;
    private readonly double[] output;
    private long paths;

    /// <summary>Initializes a new instance of the <see cref="SobolBrownianBridgeRsg"/> class.</summary>
    /// <param name="factors">The number of factors each step draws, at least 1.</param>
    /// <param name="steps">The number of steps each sequence draws, at least 1.</param>
    /// <param name="ordering">How the variates are assigned to the factors and steps.</param>
    /// <param name="seed">The Sobol sequence's seed, in [1, 4294967295].</param>
    /// <param name="directionIntegers">The Sobol sequence's direction integers.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="factors"/> or <paramref name="steps"/> is below 1, their product is outside the Sobol
    /// sequence's dimensions, or <paramref name="ordering"/>, <paramref name="seed"/> or
    /// <paramref name="directionIntegers"/> is out of range.
    /// </exception>
    public SobolBrownianBridgeRsg(
        int factors,
        int steps,
        SobolBrownianGeneratorBaseOrdering ordering,
        long seed,
        SobolRsgDirectionIntegers directionIntegers = SobolRsgDirectionIntegers.JoeKuoD7)
    {
        gen = new SobolBrownianGenerator(factors, steps, ordering, seed, directionIntegers);
        this.factors = factors;
        this.steps = steps;
        this.ordering = ordering;
        this.seed = seed;
        this.directionIntegers = directionIntegers;
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
        var copy = new SobolBrownianBridgeRsg(factors, steps, ordering, seed, directionIntegers);
        for (long i = 0; i < paths; ++i)
        {
            copy.NextSequence();
        }

        seq.AsSpan().CopyTo(copy.seq);
        return copy;
    }
}
