// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/latentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The members of <see cref="PolarStudentTFactorSampler{TUrng}"/> that need more of their generator than the
/// class does: its seed constructor.</summary>
[Experimental("NQL0001")]
public static class PolarStudentTFactorSampler
{
    /// <summary>A polar Student t factor sampler over a uniform generator built from a seed.</summary>
    /// <typeparam name="TUrng">The uniform generator, which can be built from a seed.</typeparam>
    /// <param name="copula">The Student t copula policy.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295].</param>
    /// <returns>The sampler.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public static PolarStudentTFactorSampler<TUrng> Create<TUrng>(TCopulaPolicy copula, long seed)
        where TUrng : class, ISeedableRng<TUrng> =>
        new(copula, TUrng.Create(Seeds.Check(seed)));
}
