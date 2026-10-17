// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Walter Penschke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/rngtraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The traits for Poisson-distributed pseudo-random number generation: QuantLib's
/// <c>GenericPseudoRandom&lt;MersenneTwisterUniformRng, InverseCumulativePoisson&gt;</c>, Poisson sequences from
/// MT19937.
/// </summary>
public readonly struct PoissonPseudoRandom : IRngTraits
{
    /// <summary>Gets a value indicating whether the generators' samples allow an error estimate: they do.</summary>
    public static bool AllowsErrorEstimate =>
        GenericPseudoRandom<MersenneTwisterUniformRng, InverseCumulativePoisson>.AllowsErrorEstimate;

    /// <summary>
    /// Builds a sequence generator with λ = 1, the inverse cumulative's default (QuantLib's
    /// <c>make_sequence_generator</c>, returning its <c>rsg_type</c>).
    /// </summary>
    /// <param name="dimension">The dimension of each sequence, non-negative.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The sequence generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of its range.</exception>
    public static InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativePoisson>
        MakeSequenceGenerator(int dimension, long seed) =>
        GenericPseudoRandom<MersenneTwisterUniformRng, InverseCumulativePoisson>.MakeSequenceGenerator(dimension, seed);

    /// <summary>
    /// Builds a sequence generator with a given inverse cumulative, as QuantLib's does when its <c>icInstance</c> is
    /// set.
    /// </summary>
    /// <param name="dimension">The dimension of each sequence, non-negative.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="inverseCumulative">The inverse cumulative distribution.</param>
    /// <returns>The sequence generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of its range.</exception>
    public static InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>, InverseCumulativePoisson>
        MakeSequenceGenerator(int dimension, long seed, InverseCumulativePoisson inverseCumulative) =>
        GenericPseudoRandom<MersenneTwisterUniformRng, InverseCumulativePoisson>.MakeSequenceGenerator(
            dimension, seed, inverseCumulative);

    /// <inheritdoc/>
    static IRsg IRngTraits.MakeSequenceGenerator(int dimension, long seed) => MakeSequenceGenerator(dimension, seed);
}
