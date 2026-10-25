// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/browniangenerators/mtbrowniangenerator.hpp ql/models/marketmodels/browniangenerators/mtbrowniangenerator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The Mersenne-twister Brownian generator for market-model simulations: each path a uniform sequence of the factors
/// times the steps, drawn eagerly, each step's slice turned into Gaussian variates by the inverse cumulative normal.
/// </summary>
/// <remarks>
/// A working type (the S-hes plan's HR3 (iv)). Its seed is explicit, in [1, 4294967295]: QuantLib's seed 0, which draws
/// a seed from the clock, is refused, as <see cref="MersenneTwisterUniformRng"/> refuses it. A step's output length
/// and a path's exhaustion are checked always, QuantLib's extra-safety checks.
/// </remarks>
public sealed class MTBrownianGenerator : BrownianGenerator
{
    private readonly int factors;
    private readonly int steps;
    private readonly RandomSequenceGenerator<MersenneTwisterUniformRng> generator;
    private readonly InverseCumulativeNormal inverseCumulative;
    private int lastStep;

    /// <summary>Initializes a new instance of the <see cref="MTBrownianGenerator"/> class.</summary>
    /// <param name="factors">The number of factors each step draws, at least 1.</param>
    /// <param name="steps">The number of steps each path draws, at least 1.</param>
    /// <param name="seed">The Mersenne twister's seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="factors"/> or <paramref name="steps"/> is below 1, or <paramref name="seed"/> is outside
    /// [1, 4294967295].
    /// </exception>
    public MTBrownianGenerator(int factors, int steps, long seed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(factors);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(steps);
        this.factors = factors;
        this.steps = steps;
        generator = new RandomSequenceGenerator<MersenneTwisterUniformRng>(
            checked(factors * steps), new MersenneTwisterUniformRng(seed));
        inverseCumulative = new InverseCumulativeNormal();
    }

    /// <inheritdoc/>
    public override int NumberOfFactors => factors;

    /// <inheritdoc/>
    public override int NumberOfSteps => steps;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="output"/>'s length isn't the number of factors.</exception>
    /// <exception cref="InvalidOperationException">The path's steps are drawn.</exception>
    public override double NextStep(Span<double> output)
    {
        if (output.Length != factors)
        {
            throw new ArgumentException("size mismatch", nameof(output));
        }

        if (!(lastStep < steps))
        {
            throw new InvalidOperationException("uniform sequence exhausted");
        }

        ReadOnlySpan<double> currentSequence = generator.LastSequence().Value.Span;
        int start = lastStep * factors;
        for (int i = 0; i < factors; ++i)
        {
            output[i] = inverseCumulative.Evaluate(currentSequence[start + i]);
        }

        ++lastStep;
        return 1.0;
    }

    /// <inheritdoc/>
    public override double NextPath()
    {
        Sample<ReadOnlyMemory<double>> sample = generator.NextSequence();
        lastStep = 0;
        return sample.Weight;
    }
}
