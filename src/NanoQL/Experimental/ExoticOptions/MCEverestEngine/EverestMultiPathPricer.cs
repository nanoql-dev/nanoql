// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/mceverestengine.hpp ql/experimental/exoticoptions/mceverestengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>The Everest option's path pricer: the notional times one plus the worst asset's return plus the
/// guarantee, discounted.</summary>
/// <remarks>Immutable; a path prices without allocating.</remarks>
[Experimental("NQL0001")]
public sealed class EverestMultiPathPricer : PathPricer<MultiPath>
{
    private readonly double notional;
    private readonly double guarantee;
    private readonly double discount;

    /// <summary>Initializes a new instance of the <see cref="EverestMultiPathPricer"/> class.</summary>
    /// <param name="notional">The notional.</param>
    /// <param name="guarantee">The guaranteed return.</param>
    /// <param name="discount">The discount factor to the exercise date.</param>
    public EverestMultiPathPricer(double notional, double guarantee, double discount)
    {
        this.notional = notional;
        this.guarantee = guarantee;
        this.discount = discount;
    }

    /// <summary>The discounted payoff on the worst asset's return over its path.</summary>
    /// <param name="multiPath">The assets' paths.</param>
    /// <returns>The path's value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multiPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The paths are empty, or there is no asset.</exception>
    public override double Evaluate(MultiPath multiPath)
    {
        ArgumentNullException.ThrowIfNull(multiPath);
        int n = multiPath.PathSize();
        if (!(n > 0))
        {
            throw new NanoQLException("the path cannot be empty");
        }

        int numAssets = multiPath.AssetNumber;
        if (!(numAssets > 0))
        {
            throw new NanoQLException("there must be some paths");
        }

        // We search the yield min
        double minYield = (multiPath[0].Back() / multiPath[0].Front()) - 1.0;
        for (int j = 1; j < numAssets; ++j)
        {
            double yield = (multiPath[j].Back() / multiPath[j].Front()) - 1.0;
            minYield = Math.Min(minYield, yield);
        }

        return (1.0 + minYield + guarantee) * notional * discount;
    }
}
