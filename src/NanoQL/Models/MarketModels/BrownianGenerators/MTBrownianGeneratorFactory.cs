// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/browniangenerators/mtbrowniangenerator.hpp ql/models/marketmodels/browniangenerators/mtbrowniangenerator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>The factory of <see cref="MTBrownianGenerator"/>s, each on the factory's seed.</summary>
/// <remarks>Immutable. Its seed is explicit, in [1, 4294967295], where QuantLib's default 0 draws one from the clock.</remarks>
public sealed class MTBrownianGeneratorFactory : BrownianGeneratorFactory
{
    private readonly long seed;

    /// <summary>Initializes a new instance of the <see cref="MTBrownianGeneratorFactory"/> class.</summary>
    /// <param name="seed">The generators' seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public MTBrownianGeneratorFactory(long seed)
    {
        Seeds.Check(seed);
        this.seed = seed;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="factors"/> or <paramref name="steps"/> is below 1.</exception>
    public override BrownianGenerator Create(int factors, int steps) => new MTBrownianGenerator(factors, steps, seed);
}
