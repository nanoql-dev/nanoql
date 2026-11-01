// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/ranluxuniformrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's <c>Ranlux3UniformRng</c>, <c>Ranlux64UniformRng&lt;223, 24&gt;</c>: the "luxury" uniform random-number
/// generator keeping 24 of every 223 draws of its engine, where any theoretically possible correlations have a very
/// small chance of being observed.
/// </summary>
/// <remarks>
/// A mutable working type, as its base is; its seed is explicit, in [1, 4294967295] (<see cref="Ranlux64UniformRng"/>).
/// </remarks>
[WorkingType]
public sealed class Ranlux3UniformRng : Ranlux64UniformRng, ISeedableRng<Ranlux3UniformRng>
{
    /// <summary>Initializes a new instance of the <see cref="Ranlux3UniformRng"/> class from a seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public Ranlux3UniformRng(long seed)
        : base(seed, 223, 24)
    {
    }

    private Ranlux3UniformRng(Ranlux3UniformRng source)
        : base(source)
    {
    }

    /// <summary>Builds the generator from a seed, for generic code (<see cref="ISeedableRng{TSelf}"/>).</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public static Ranlux3UniformRng Create(long seed) => new(seed);

    /// <summary>
    /// Copies the generator deeply: the copy's next draw equals this generator's, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public override Ranlux3UniformRng Clone() => new(this);
}
