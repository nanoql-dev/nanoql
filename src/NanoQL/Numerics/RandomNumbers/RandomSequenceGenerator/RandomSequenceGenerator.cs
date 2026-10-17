// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/randomsequencegenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The members of QuantLib's <c>RandomSequenceGenerator&lt;RNG&gt;</c> that need more of their generator than
/// <see cref="RandomSequenceGenerator{TRng}"/> does: C++ instantiates only the members a caller uses, so each is a
/// generic method here, with its own constraint.
/// </summary>
public static class RandomSequenceGenerator
{
    /// <summary>
    /// Builds a sequence generator over a generator built from a seed (QuantLib's
    /// <c>RandomSequenceGenerator(dimensionality, seed)</c>), adopting it.
    /// </summary>
    /// <typeparam name="TRng">The generator of the sequence's elements, which can be built from a seed.</typeparam>
    /// <param name="dimensionality">The dimension of each sequence, non-negative; QuantLib checks none here.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295].</param>
    /// <returns>The sequence generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dimensionality"/> is negative, or <paramref name="seed"/> outside [1, 4294967295].
    /// </exception>
    public static RandomSequenceGenerator<TRng> Create<TRng>(int dimensionality, long seed)
        where TRng : class, ISeedableRng<TRng>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dimensionality);
        return new RandomSequenceGenerator<TRng>(dimensionality, TRng.Create(seed), adopt: true);
    }

    /// <summary>
    /// Draws the next integer sequence: the generator's next <see cref="RandomSequenceGenerator{TRng}.Dimension"/>
    /// integers (QuantLib's <c>nextInt32Sequence</c>).
    /// </summary>
    /// <typeparam name="TRng">The generator of the sequence's elements, which draws integers.</typeparam>
    /// <param name="generator">The sequence generator, which the draw changes.</param>
    /// <returns>The integers, each in [0, 4294967295], in a new array, as QuantLib returns a copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="generator"/> is null.</exception>
    public static long[] NextInt32Sequence<TRng>(this RandomSequenceGenerator<TRng> generator)
        where TRng : class, IInt32Rng
    {
        ArgumentNullException.ThrowIfNull(generator);
        var int32Sequence = new long[generator.Dimension];
        TRng rng = generator.Generator;
        for (int i = 0; i < int32Sequence.Length; i++)
        {
            int32Sequence[i] = rng.NextInt32();
        }

        return int32Sequence;
    }
}
