// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/randomsequencegenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib-Python's <c>ZigguratXoshiro256StarStarGaussianRsg</c>: <see cref="RandomSequenceGenerator{TRng}"/> over
/// <see cref="ZigguratGaussianRng{TRng}"/> over <see cref="Xoshiro256StarStarUniformRng"/>, standard Gaussian
/// pseudo-random sequences.
/// </summary>
/// <remarks>
/// A convenience name: nanoql's own code uses the generic type. A mutable working type, as its base is.
/// </remarks>
[WorkingType]
public sealed class ZigguratXoshiro256StarStarGaussianRsg
    : RandomSequenceGenerator<ZigguratGaussianRng<Xoshiro256StarStarUniformRng>>
{
    /// <summary>Initializes a new instance of the <see cref="ZigguratXoshiro256StarStarGaussianRsg"/> class.</summary>
    /// <param name="dimensionality">The dimension of each sequence, at least 1.</param>
    /// <param name="rng">The generator, which the sequence generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rng"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimensionality"/> is below 1.</exception>
    public ZigguratXoshiro256StarStarGaussianRsg(int dimensionality, ZigguratXoshiro256StarStarGaussianRng rng)
        : base(dimensionality, rng)
    {
    }

    private ZigguratXoshiro256StarStarGaussianRsg(ZigguratXoshiro256StarStarGaussianRsg source)
        : base(source)
    {
    }

    /// <summary>
    /// Copies the sequence generator deeply, its generator included: the copy's next draw equals this one's, into its
    /// own buffer, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override ZigguratXoshiro256StarStarGaussianRsg Clone() => new(this);
}
