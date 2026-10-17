// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/randomsequencegenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A random-number generator generic code can build from a seed, as QuantLib's templates construct their
/// <c>RNG</c> (<c>RandomSequenceGenerator&lt;RNG&gt;(dimensionality, seed)</c>).
/// </summary>
/// <remarks>
/// Used only as a constraint: an interface with a static abstract member can't be a type argument, so the instance
/// concept is <see cref="IRng"/>.
/// </remarks>
/// <typeparam name="TSelf">The generator's own type.</typeparam>
public interface ISeedableRng<TSelf> : IRng
    where TSelf : class, ISeedableRng<TSelf>
{
    /// <summary>Builds the generator from a seed.</summary>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The generator.</returns>
    static abstract TSelf Create(long seed);
}
