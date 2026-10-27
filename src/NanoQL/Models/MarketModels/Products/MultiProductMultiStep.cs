// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/multiproductmultistep.hpp ql/models/marketmodels/products/multiproductmultistep.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A market-model product evaluated in more than one step (Rebonato's long jump): its evolution steps at every rate
/// time but the last, each step's relevant rates the one resetting there.
/// </summary>
/// <remarks>An extension point (U13): a working type, as every product (the S-mmp plan's MMPR1 (i)).</remarks>
public abstract class MultiProductMultiStep : MarketModelMultiProduct
{
    private readonly EvolutionDescription evolution;

    /// <summary>Initializes a new instance of the <see cref="MultiProductMultiStep"/> class.</summary>
    /// <param name="rateTimes">The rate times, at least two.</param>
    /// <exception cref="ArgumentException">There are fewer than two rate times, or they aren't increasing.</exception>
    protected MultiProductMultiStep(ReadOnlySpan<double> rateTimes)
    {
        if (rateTimes.Length <= 1)
        {
            throw new ArgumentException("Rate times must contain at least two values", nameof(rateTimes));
        }

        RateTimes = [.. rateTimes];
        int n = rateTimes.Length - 1;
        var evolutionTimes = new double[n];
        var relevanceRates = new (int First, int Second)[n];
        for (int i = 0; i < n; ++i)
        {
            evolutionTimes[i] = rateTimes[i];
            relevanceRates[i] = (i, i + 1);
        }

        evolution = new EvolutionDescription(rateTimes, evolutionTimes, relevanceRates);
    }

    /// <summary>Initializes a new instance of the <see cref="MultiProductMultiStep"/> class as a copy of
    /// another.</summary>
    /// <param name="other">The product copied.</param>
    protected MultiProductMultiStep(MultiProductMultiStep other)
    {
        ArgumentNullException.ThrowIfNull(other);
        RateTimes = other.RateTimes;
        evolution = other.evolution;
    }

    /// <inheritdoc/>
    public override EvolutionDescription Evolution => evolution;

    /// <summary>Gets the rate times.</summary>
    protected ImmutableArray<double> RateTimes { get; }

    /// <summary>The money-market measure's numeraires, offset by one: the bond after each step's time.</summary>
    /// <returns>The numeraires' indices.</returns>
    public override ImmutableArray<int> SuggestedNumeraires()
    {
        int n = RateTimes.Length - 1;
        var numeraires = new int[n];

        // MoneyMarketPlus(1)
        for (int i = 0; i < n; ++i)
        {
            numeraires[i] = i + 1;
        }

        return ImmutableArray.Create(numeraires);
    }
}
