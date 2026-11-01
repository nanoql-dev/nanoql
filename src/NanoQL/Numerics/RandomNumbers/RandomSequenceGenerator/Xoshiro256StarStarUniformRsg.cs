// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/randomsequencegenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib-Python's <c>Xoshiro256StarStarUniformRsg</c>: <see cref="RandomSequenceGenerator{TRng}"/> over
/// <see cref="Xoshiro256StarStarUniformRng"/>, uniform sequences in (0, 1).
/// </summary>
/// <remarks>
/// A convenience name: nanoql's own code uses the generic type. A mutable working type, as its base is.
/// </remarks>
[WorkingType]
public sealed class Xoshiro256StarStarUniformRsg : RandomSequenceGenerator<Xoshiro256StarStarUniformRng>
{
    /// <summary>Initializes a new instance of the <see cref="Xoshiro256StarStarUniformRsg"/> class.</summary>
    /// <param name="dimensionality">The dimension of each sequence, at least 1.</param>
    /// <param name="rng">The generator, which the sequence generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rng"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimensionality"/> is below 1.</exception>
    public Xoshiro256StarStarUniformRsg(int dimensionality, Xoshiro256StarStarUniformRng rng)
        : base(dimensionality, rng)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Xoshiro256StarStarUniformRsg"/> class over a generator built from a
    /// seed (QuantLib's seed constructor).
    /// </summary>
    /// <param name="dimensionality">The dimension of each sequence, non-negative; QuantLib checks none here.</param>
    /// <param name="seed">The generator's seed, any 64 bits but 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dimensionality"/> is negative, or <paramref name="seed"/> is 0.
    /// </exception>
    public Xoshiro256StarStarUniformRsg(int dimensionality, long seed)
        : base(dimensionality, new Xoshiro256StarStarUniformRng(seed), adopt: true)
    {
    }

    private Xoshiro256StarStarUniformRsg(Xoshiro256StarStarUniformRsg source)
        : base(source)
    {
    }

    /// <summary>
    /// Copies the sequence generator deeply, its generator included: the copy's next draw equals this one's, into its
    /// own buffer, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override Xoshiro256StarStarUniformRsg Clone() => new(this);
}
