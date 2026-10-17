// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/inversecumulativersg.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A random sequence generator generic code can build from a dimension and a seed, as QuantLib's
/// <c>GenericLowDiscrepancy</c> constructs its <c>ursg_type(dimension, seed)</c>.
/// </summary>
/// <remarks>
/// Used only as a constraint: an interface with a static abstract member can't be a type argument, so the instance
/// concept is <see cref="IRsg"/>.
/// </remarks>
/// <typeparam name="TSelf">The generator's own type.</typeparam>
public interface ISeedableRsg<TSelf> : IRsg
    where TSelf : class, ISeedableRsg<TSelf>
{
    /// <summary>Builds the generator from a dimension and a seed.</summary>
    /// <param name="dimension">The dimension of each sequence.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The generator.</returns>
    static abstract TSelf Create(int dimension, long seed);
}
