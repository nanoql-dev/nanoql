// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Walter Penschke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/rngtraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Random-number traits for low-discrepancy generation: sequences of a uniform sequence generator
/// <typeparamref name="TUrsg"/>, mapped through the inverse cumulative <typeparamref name="TIc"/>.
/// </summary>
/// <remarks>
/// QuantLib's static <c>icInstance</c>, which every sequence generator the traits make reads, isn't ported: the
/// overload of <see cref="MakeSequenceGenerator(int, long, TIc)"/> takes the inverse cumulative instead, and the one
/// without it uses its zero value, QuantLib's default construction.
/// </remarks>
/// <typeparam name="TUrsg">The uniform sequence generator, which can be built from a dimension and a seed.</typeparam>
/// <typeparam name="TIc">The inverse cumulative distribution.</typeparam>
public readonly struct GenericLowDiscrepancy<TUrsg, TIc> : IRngTraits
    where TUrsg : class, ISeedableRsg<TUrsg>
    where TIc : struct, IInverseCumulative
{
    /// <summary>Gets a value indicating whether the generators' samples allow an error estimate: they don't.</summary>
    public static bool AllowsErrorEstimate => false;

    /// <summary>
    /// Builds a sequence generator: the inverse cumulative of a <typeparamref name="TUrsg"/> built from the dimension
    /// and the seed (QuantLib's <c>make_sequence_generator</c>, returning its <c>rsg_type</c>).
    /// </summary>
    /// <param name="dimension">The dimension of each sequence.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The sequence generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of its range.</exception>
    public static InverseCumulativeRsg<TUrsg, TIc> MakeSequenceGenerator(int dimension, long seed) =>
        new(TUrsg.Create(dimension, seed), default, adopt: true);

    /// <summary>
    /// Builds a sequence generator with a given inverse cumulative, as QuantLib's does when its <c>icInstance</c> is
    /// set.
    /// </summary>
    /// <param name="dimension">The dimension of each sequence.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="inverseCumulative">The inverse cumulative distribution.</param>
    /// <returns>The sequence generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of its range.</exception>
    public static InverseCumulativeRsg<TUrsg, TIc> MakeSequenceGenerator(int dimension, long seed, TIc inverseCumulative) =>
        new(TUrsg.Create(dimension, seed), inverseCumulative, adopt: true);

    /// <inheritdoc/>
    static IRsg IRngTraits.MakeSequenceGenerator(int dimension, long seed) => MakeSequenceGenerator(dimension, seed);
}
