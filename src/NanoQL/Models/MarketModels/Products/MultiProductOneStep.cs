// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multiproductonestep.hpp ql/models/marketmodels/products/multiproductonestep.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A market-model product evaluated in one step (Rebonato's very long jump): its evolution steps once, at the last
/// rate's reset, every rate relevant.
/// </summary>
/// <remarks>An extension point (U13): a working type, as every product (the S-mmp plan's MMPR1 (i)).</remarks>
public abstract class MultiProductOneStep : MarketModelMultiProduct
{
    private readonly EvolutionDescription evolution;

    /// <summary>Initializes a new instance of the <see cref="MultiProductOneStep"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <exception cref="ArgumentException">There are fewer than two rate times, or they aren't increasing.</exception>
    protected MultiProductOneStep(ReadOnlySpan<double> rateTimes)
    {
        if (rateTimes.Length <= 1)
        {
            throw new ArgumentException("Rate times must contain at least two values", nameof(rateTimes));
        }

        RateTimes = [.. rateTimes];
        ReadOnlySpan<double> evolutionTimes = [rateTimes[^2]];
        ReadOnlySpan<(int First, int Second)> relevanceRates = [(0, rateTimes.Length - 1)];
        evolution = new EvolutionDescription(rateTimes, evolutionTimes, relevanceRates);
    }

    /// <summary>Initializes a new instance of the <see cref="MultiProductOneStep"/> class as a copy of
    /// another.</summary>
    /// <param name="other">The product copied.</param>
    protected MultiProductOneStep(MultiProductOneStep other)
    {
        ArgumentNullException.ThrowIfNull(other);
        RateTimes = other.RateTimes;
        evolution = other.evolution;
    }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <summary>Gets the rate times.</summary>
    protected ImmutableArray<double> RateTimes { get; }

    /// <summary>The terminal measure's numeraire: the last bond.</summary>
    /// <returns>The numeraire's index.</returns>
    public override ImmutableArray<int> SuggestedNumeraires() => [RateTimes.Length - 1];
}
