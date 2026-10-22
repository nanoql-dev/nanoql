// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/mctraits.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The default Monte Carlo traits for multi-variate models: <see cref="MultiPath"/> paths from a
/// <see cref="MultiPathGenerator{TGsg}"/> over the random-number traits' sequence generator.
/// </summary>
/// <typeparam name="TRng">The random-number traits, <see cref="PseudoRandom"/> or <see cref="LowDiscrepancy"/>.</typeparam>
public readonly struct MultiVariate<TRng> : IMcTraits<MultiPath>
    where TRng : IRngTraits
{
    /// <inheritdoc/>
    public static bool AllowsErrorEstimate => TRng.AllowsErrorEstimate;

    /// <inheritdoc/>
    public static IRsg MakeSequenceGenerator(int dimension, long seed) => TRng.MakeSequenceGenerator(dimension, seed);

    /// <inheritdoc/>
    public static IPathGenerator<MultiPath> MakePathGenerator(
        StochasticProcess process, TimeGrid timeGrid, IRsg generator, bool brownianBridge) =>
        new MultiPathGenerator<IRsg>(process, timeGrid, generator, brownianBridge);
}
