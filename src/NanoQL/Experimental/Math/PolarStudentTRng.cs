// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/polarstudenttrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The members of <see cref="PolarStudentTRng{TUrng}"/> that need more of their uniform generator than the
/// class does: its seed constructor.</summary>
[Experimental("NQL0001")]
public static class PolarStudentTRng
{
    /// <summary>A polar Student t generator over a uniform generator built from a seed, which it adopts.</summary>
    /// <typeparam name="TUrng">The uniform generator, which can be built from a seed.</typeparam>
    /// <param name="degFreedom">The degrees of freedom, positive.</param>
    /// <param name="seed">The uniform generator's seed, in [1, 4294967295].</param>
    /// <returns>The generator.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    /// <exception cref="ArgumentException"><paramref name="degFreedom"/> isn't positive.</exception>
    public static PolarStudentTRng<TUrng> Create<TUrng>(double degFreedom, long seed)
        where TUrng : class, ISeedableRng<TUrng> =>
        new(degFreedom, TUrng.Create(Seeds.Check(seed)), adopt: true);
}
