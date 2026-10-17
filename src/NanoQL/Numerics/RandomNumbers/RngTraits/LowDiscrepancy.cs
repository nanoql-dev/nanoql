// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Walter Penschke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/rngtraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The default traits for low-discrepancy sequence generation: QuantLib's
/// <c>GenericLowDiscrepancy&lt;SobolRsg, InverseCumulativeNormal&gt;</c>, Gaussian Sobol sequences with Jäckel's
/// direction integers and the Gray code.
/// </summary>
public readonly struct LowDiscrepancy : IRngTraits
{
    /// <summary>Gets a value indicating whether the generators' samples allow an error estimate: they don't.</summary>
    public static bool AllowsErrorEstimate => GenericLowDiscrepancy<SobolRsg, InverseCumulativeNormal>.AllowsErrorEstimate;

    /// <summary>Builds a sequence generator (QuantLib's <c>make_sequence_generator</c>, returning its <c>rsg_type</c>).</summary>
    /// <param name="dimension">The dimension of each sequence, from 1 to 21,200.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The sequence generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of its range.</exception>
    public static InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal> MakeSequenceGenerator(int dimension, long seed) =>
        GenericLowDiscrepancy<SobolRsg, InverseCumulativeNormal>.MakeSequenceGenerator(dimension, seed);

    /// <summary>
    /// Builds a sequence generator with a given inverse cumulative, as QuantLib's does when its <c>icInstance</c> is
    /// set.
    /// </summary>
    /// <param name="dimension">The dimension of each sequence, from 1 to 21,200.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="inverseCumulative">The inverse cumulative distribution.</param>
    /// <returns>The sequence generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of its range.</exception>
    public static InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal> MakeSequenceGenerator(
        int dimension, long seed, InverseCumulativeNormal inverseCumulative) =>
        GenericLowDiscrepancy<SobolRsg, InverseCumulativeNormal>.MakeSequenceGenerator(dimension, seed, inverseCumulative);

    /// <inheritdoc/>
    static IRsg IRngTraits.MakeSequenceGenerator(int dimension, long seed) => MakeSequenceGenerator(dimension, seed);
}
