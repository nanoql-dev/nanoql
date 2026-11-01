// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/ranluxuniformrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's <c>Ranlux4UniformRng</c>, <c>Ranlux64UniformRng&lt;389, 24&gt;</c>: the "luxury" uniform random-number
/// generator keeping 24 of every 389 draws of its engine, the highest possible luxury.
/// </summary>
/// <remarks>
/// A mutable working type, as its base is; its seed is explicit, in [1, 4294967295] (<see cref="Ranlux64UniformRng"/>).
/// </remarks>
[WorkingType]
public sealed class Ranlux4UniformRng : Ranlux64UniformRng, ISeedableRng<Ranlux4UniformRng>
{
    /// <summary>Initializes a new instance of the <see cref="Ranlux4UniformRng"/> class from a seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public Ranlux4UniformRng(long seed)
        : base(seed, 389, 24)
    {
    }

    private Ranlux4UniformRng(Ranlux4UniformRng source)
        : base(source)
    {
    }

    /// <summary>Builds the generator from a seed, for generic code (<see cref="ISeedableRng{TSelf}"/>).</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public static Ranlux4UniformRng Create(long seed) => new(seed);

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this generator's, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override Ranlux4UniformRng Clone() => new(this);
}
