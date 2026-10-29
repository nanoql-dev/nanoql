// SPDX-FileCopyrightText: Copyright (C) 2010 Kakhkhor Abdijalilov
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/zigguratrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>Random-number traits for the Ziggurat generator: QuantLib's <c>Ziggurat</c>.</summary>
[Experimental("NQL0001")]
public readonly struct Ziggurat : IRngTraits
{
    /// <summary>Gets a value indicating whether the generators' samples allow an error estimate: they do.</summary>
    public static bool AllowsErrorEstimate => true;

    /// <summary>
    /// Builds a sequence generator over a <see cref="ZigguratRng"/> built from the seed (QuantLib's
    /// <c>make_sequence_generator</c>, returning its <c>rsg_type</c>).
    /// </summary>
    /// <param name="dimension">The dimension of each sequence, non-negative.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The sequence generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of its range.</exception>
    public static RandomSequenceGenerator<ZigguratRng> MakeSequenceGenerator(int dimension, long seed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dimension);
        return RandomSequenceGenerator.Create<ZigguratRng>(dimension, seed);
    }

    /// <inheritdoc/>
    static IRsg IRngTraits.MakeSequenceGenerator(int dimension, long seed) => MakeSequenceGenerator(dimension, seed);
}
