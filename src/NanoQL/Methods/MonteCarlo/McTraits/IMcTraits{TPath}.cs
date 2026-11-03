// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/mctraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// Monte Carlo traits: a path type and its generator over a random-number traits type, QuantLib's
/// <c>SingleVariate&lt;RNG&gt;</c> and <c>MultiVariate&lt;RNG&gt;</c> policies (D12: static abstract members for
/// stateless traits).
/// </summary>
/// <typeparam name="TPath">The path type, QuantLib's <c>path_type</c>.</typeparam>
/// <remarks>
/// QuantLib's traits name types (<c>rng_traits</c>, <c>rsg_type</c>, <c>path_generator_type</c>); a C# interface
/// can't, so these members build them: an engine on <c>TMc : IMcTraits&lt;TPath&gt;</c> makes its sequence generator
/// and path generator through them, as QuantLib's engines make <c>RNG::rsg_type</c> and <c>path_generator_type</c>.
/// </remarks>
public interface IMcTraits<TPath>
{
    /// <summary>Gets a value indicating whether the random numbers give an error estimate (pseudo-random ones do).</summary>
    static abstract bool AllowsErrorEstimate { get; }

    /// <summary>The random-number traits' sequence generator, QuantLib's <c>RNG::make_sequence_generator</c>.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="seed">The seed (explicit, 0 refused).</param>
    /// <returns>A new generator.</returns>
    static abstract IRsg MakeSequenceGenerator(int dimension, long seed);

    /// <summary>A new path generator, QuantLib's <c>path_generator_type</c>, over a time grid.</summary>
    /// <param name="process">The process.</param>
    /// <param name="timeGrid">The time grid.</param>
    /// <param name="generator">The sequence generator, which the path generator copies.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <returns>A new path generator.</returns>
    static abstract IPathGenerator<TPath> MakePathGenerator(
        StochasticProcess process, TimeGrid timeGrid, IRsg generator, bool brownianBridge);
}
