// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>The factory of <see cref="SobolBrownianGenerator"/>s, each on the factory's ordering, seed and direction
/// integers.</summary>
/// <remarks>Immutable. Its seed is explicit, in [1, 4294967295], where QuantLib's default 0 draws one from the clock.</remarks>
public sealed class SobolBrownianGeneratorFactory : BrownianGeneratorFactory
{
    private readonly SobolBrownianGeneratorBaseOrdering ordering;
    private readonly long seed;
    private readonly SobolRsgDirectionIntegers integers;

    /// <summary>Initializes a new instance of the <see cref="SobolBrownianGeneratorFactory"/> class.</summary>
    /// <param name="ordering">How the generators assign the variates to the factors and steps.</param>
    /// <param name="seed">The generators' seed, in [1, 4294967295].</param>
    /// <param name="directionIntegers">The generators' direction integers.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="ordering"/> or <paramref name="directionIntegers"/> isn't a member of its type, or
    /// <paramref name="seed"/> is outside [1, 4294967295].
    /// </exception>
    public SobolBrownianGeneratorFactory(
        SobolBrownianGeneratorBaseOrdering ordering,
        long seed,
        SobolRsgDirectionIntegers directionIntegers = SobolRsgDirectionIntegers.Jaeckel)
    {
        if (ordering is < SobolBrownianGeneratorBaseOrdering.Factors or > SobolBrownianGeneratorBaseOrdering.Diagonal)
        {
            throw new ArgumentOutOfRangeException(nameof(ordering), ordering, "unknown ordering");
        }

        if (directionIntegers is < SobolRsgDirectionIntegers.Unit or > SobolRsgDirectionIntegers.Kuo3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(directionIntegers), directionIntegers, $"unknown direction integers ({(int)directionIntegers})");
        }

        Seeds.Check(seed);
        this.ordering = ordering;
        this.seed = seed;
        integers = directionIntegers;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="factors"/> or <paramref name="steps"/> is below 1, or their product is outside the Sobol
    /// sequence's dimensions.
    /// </exception>
    public override BrownianGenerator Create(int factors, int steps) =>
        new SobolBrownianGenerator(factors, steps, ordering, seed, integers);
}
