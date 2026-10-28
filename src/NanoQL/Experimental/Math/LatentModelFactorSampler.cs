// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/latentmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The members of <see cref="LatentModelFactorSampler{TCopula, TInitTraits, TUsng}"/> that need more of their
/// generator than the class does: its seed constructor.</summary>
[Experimental("NQL0001")]
public static class LatentModelFactorSampler
{
    /// <summary>A factor sampler over a uniform sequence generator built from a seed, which it adopts.</summary>
    /// <typeparam name="TCopula">The copula policy.</typeparam>
    /// <typeparam name="TInitTraits">The policy's initialization parameters.</typeparam>
    /// <typeparam name="TUsng">The uniform sequence generator, which can be built from a seed.</typeparam>
    /// <param name="copula">The copula policy.</param>
    /// <param name="seed">The generator's seed, in [1, 4294967295].</param>
    /// <returns>The sampler.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public static LatentModelFactorSampler<TCopula, TInitTraits, TUsng> Create<TCopula, TInitTraits, TUsng>(TCopula copula, long seed)
        where TCopula : struct, ICopulaPolicy<TCopula, TInitTraits>
        where TInitTraits : struct
        where TUsng : class, ISeedableRsg<TUsng> =>
        new(copula, TUsng.Create(copula.NumFactors, seed), adopt: true);
}
