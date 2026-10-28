// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/randomsequencegenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib-Python's <c>KnuthUniformRsg</c>: <see cref="RandomSequenceGenerator{TRng}"/> over
/// <see cref="KnuthUniformRng"/>, uniform sequences in (0, 1).
/// </summary>
/// <remarks>
/// A convenience name: nanoql's own code uses the generic type. A mutable working type, as its base is.
/// </remarks>
public sealed class KnuthUniformRsg : RandomSequenceGenerator<KnuthUniformRng>
{
    /// <summary>Initializes a new instance of the <see cref="KnuthUniformRsg"/> class.</summary>
    /// <param name="dimensionality">The dimension of each sequence, at least 1.</param>
    /// <param name="rng">The generator, which the sequence generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rng"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimensionality"/> is below 1.</exception>
    public KnuthUniformRsg(int dimensionality, KnuthUniformRng rng)
        : base(dimensionality, rng)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KnuthUniformRsg"/> class over a generator built from a
    /// seed (QuantLib's seed constructor).
    /// </summary>
    /// <param name="dimensionality">The dimension of each sequence, non-negative; QuantLib checks none here.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dimensionality"/> is negative, or <paramref name="seed"/> outside [1, 4294967295].
    /// </exception>
    public KnuthUniformRsg(int dimensionality, long seed)
        : base(dimensionality, new KnuthUniformRng(seed), adopt: true)
    {
    }

    private KnuthUniformRsg(KnuthUniformRsg source)
        : base(source)
    {
    }

    /// <summary>
    /// Copies the sequence generator deeply, its generator included: the copy's next draw equals this one's, into its
    /// own buffer, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override KnuthUniformRsg Clone() => new(this);
}
