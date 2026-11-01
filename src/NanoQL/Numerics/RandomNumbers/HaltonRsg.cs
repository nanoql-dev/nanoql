// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002 Peter Jäckel "Monte Carlo Methods in Finance".
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND LicenseRef-Jaeckel
// Upstream: ql/math/randomnumbers/haltonrsg.hpp ql/math/randomnumbers/haltonrsg.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// ===========================================================================
// NOTE: The following copyright notice applies to the original code,
//
// Copyright (C) 2002 Peter Jäckel "Monte Carlo Methods in Finance".
// All rights reserved.
//
// Permission to use, copy, modify, and distribute this software is freely
// granted, provided that this notice is preserved.
// ===========================================================================

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The Halton low-discrepancy sequence generator. For more details see chapter 8, paragraph 2 of "Monte Carlo Methods
/// in Finance", by Peter Jäckel.
/// </summary>
/// <remarks>
/// <para>
/// A mutable working type: each draw changes the generator, and it is not thread-safe. A draw returns a view of the
/// generator's buffer, valid until its next draw.
/// </para>
/// <para>
/// The seed, in [1, 4294967295], is only used when <c>randomStart</c> or <c>randomShift</c> is true; QuantLib's seed
/// 0, which there draws a seed from the clock, is refused. The counter and the random start add as QuantLib's
/// <c>unsigned long</c>s do under MSVC, in 32 bits, so their sum wraps at 2^32.
/// </para>
/// </remarks>
[WorkingType]
public sealed class HaltonRsg : ISeedableRsg<HaltonRsg>
{
    private readonly uint[] primes;
    private readonly uint[] randomStart;
    private readonly double[] randomShift;
    private readonly double[] sequence;
    private uint sequenceCounter;

    /// <summary>Initializes a new instance of the <see cref="HaltonRsg"/> class.</summary>
    /// <param name="dimensionality">The dimension of each sequence, at least 1.</param>
    /// <param name="seed">The seed, in [1, 4294967295], of the random start and shift.</param>
    /// <param name="randomStart">Whether each dimension starts at a random point of its sequence.</param>
    /// <param name="randomShift">Whether each dimension is shifted by a random amount, modulo 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dimensionality"/> is below 1, or <paramref name="seed"/> outside [1, 4294967295].
    /// </exception>
    public HaltonRsg(int dimensionality, long seed, bool randomStart = true, bool randomShift = false)
    {
        CheckDimensionality(dimensionality, nameof(dimensionality));
        Seeds.Check(seed);
        sequence = new double[dimensionality];
        this.randomStart = new uint[dimensionality];
        this.randomShift = new double[dimensionality];

        // QuantLib asks PrimeNumbers::get(i) for each dimension at each draw, from its shared list; the primes are
        // computed once here instead.
        primes = PrimeNumbers.First(dimensionality);

        if (randomStart || randomShift)
        {
            RandomSequenceGenerator<MersenneTwisterUniformRng> uniformRsg =
                RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(dimensionality, seed);
            if (randomStart)
            {
                long[] start = uniformRsg.NextInt32Sequence();
                for (int i = 0; i < dimensionality; i++)
                {
                    this.randomStart[i] = (uint)start[i];
                }
            }

            if (randomShift)
            {
                uniformRsg.NextSequence().Value.Span.CopyTo(this.randomShift);
            }
        }
    }

    private HaltonRsg(HaltonRsg source)
    {
        // The primes, the random start and the random shift never change after construction, so a copy shares them.
        primes = source.primes;
        randomStart = source.randomStart;
        randomShift = source.randomShift;
        sequence = (double[])source.sequence.Clone();
        sequenceCounter = source.sequenceCounter;
    }

    /// <summary>Gets the dimension of each sequence.</summary>
    public int Dimension => sequence.Length;

    /// <summary>
    /// Builds a generator from a dimension and a seed, with QuantLib's defaults (a random start, no random shift), for
    /// generic code (<see cref="ISeedableRsg{TSelf}"/>).
    /// </summary>
    /// <param name="dimension">The dimension of each sequence, at least 1.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of its range.</exception>
    public static HaltonRsg Create(int dimension, long seed)
    {
        CheckDimensionality(dimension, nameof(dimension));
        return new(dimension, seed);
    }

    /// <summary>Draws the next sequence, with weight 1.0.</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight.</returns>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        ++sequenceCounter;
        for (int i = 0; i < sequence.Length; ++i)
        {
            double h = 0.0;
            uint b = primes[i];
            double f = 1.0;
            uint k = unchecked(sequenceCounter + randomStart[i]);
            while (k != 0U)
            {
                f /= b;
                h += (k % b) * f;
                k /= b;
            }

            sequence[i] = h + randomShift[i];

            // QuantLib's long(), 32 bits under MSVC
            sequence[i] -= (int)sequence[i];
        }

        return new(sequence, 1.0);
    }

    /// <summary>Gets the sequence the last draw gave.</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight.</returns>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(sequence, 1.0);

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this generator's, into its own buffer, and the two
    /// change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public HaltonRsg Clone() => new(this);

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();

    // QuantLib's check of the dimensionality, under the caller's parameter name.
    private static void CheckDimensionality(int dimensionality, string paramName)
    {
        if (dimensionality <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, dimensionality, "dimensionality must be greater than 0");
        }
    }
}
