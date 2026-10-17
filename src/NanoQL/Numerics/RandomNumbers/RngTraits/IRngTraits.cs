// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Walter Penschke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/rngtraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's random-number traits concept (<c>PseudoRandom</c>, <c>LowDiscrepancy</c>): whether the generators allow
/// an error estimate, and how to build the sequence generator a Monte Carlo engine draws from.
/// </summary>
/// <remarks>
/// A Monte Carlo engine keeps QuantLib's one traits parameter, <c>where TTraits : struct, IRngTraits</c>, and draws
/// from the type-erased <see cref="IRsg"/> this returns. Called on a traits struct itself,
/// <c>MakeSequenceGenerator</c> returns the closed generic, QuantLib's <c>rsg_type</c>.
/// </remarks>
public interface IRngTraits
{
    /// <summary>Gets a value indicating whether the generators' samples allow an error estimate.</summary>
    static abstract bool AllowsErrorEstimate { get; }

    /// <summary>Builds a sequence generator (QuantLib's <c>make_sequence_generator</c>).</summary>
    /// <param name="dimension">The dimension of each sequence.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The sequence generator.</returns>
    static abstract IRsg MakeSequenceGenerator(int dimension, long seed);
}
