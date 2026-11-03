// SPDX-FileCopyrightText: Copyright (C) 2008 Andrea Odetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/mcbasket/mcpathbasketengine.hpp ql/experimental/mcbasket/mcpathbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The path basket's path pricer: the path payoff's payments at the fixing times, discounted and summed, its early
/// exercise ignored.
/// </summary>
/// <remarks>
/// A working type: it holds the path's fixings and payments buffers, which QuantLib builds per path, so a path prices
/// without allocating after the first.
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class EuropeanPathMultiPathPricer : PathPricer<MultiPath>
{
    private static readonly Vector[] NoStates = [];

    private readonly PathPayoff payoff;
    private readonly ImmutableArray<int> timePositions;
    private readonly ImmutableArray<YieldTermStructure> forwardTermStructures;
    private readonly Vector discounts;
    private readonly Vector noExercises = new(0);
    private Matrix path = new(0, 0);
    private Vector values = new(0);

    /// <summary>Initializes a new instance of the <see cref="EuropeanPathMultiPathPricer"/> class.</summary>
    /// <param name="payoff">The path payoff.</param>
    /// <param name="timePositions">The grid index of each fixing time.</param>
    /// <param name="forwardTermStructures">The yield curve at each fixing date.</param>
    /// <param name="discounts">The discount factor to each fixing time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> or <paramref name="discounts"/> is
    /// <see langword="null"/>.</exception>
    public EuropeanPathMultiPathPricer(
        PathPayoff payoff,
        IEnumerable<int> timePositions,
        ImmutableArray<YieldTermStructure> forwardTermStructures,
        Vector discounts)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(timePositions);
        ArgumentNullException.ThrowIfNull(discounts);
        this.payoff = payoff;
        this.timePositions = [.. timePositions];
        this.forwardTermStructures = forwardTermStructures;
        this.discounts = discounts.Clone();
    }

    /// <summary>The discounted sum of the path payoff's payments.</summary>
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

        int numberOfAssets = multiPath.AssetNumber;
        if (!(numberOfAssets > 0))
        {
            throw new NanoQLException("there must be some paths");
        }

        int numberOfTimes = timePositions.Length;
        if (path.Rows != numberOfAssets || path.Columns != numberOfTimes)
        {
            path = new Matrix(numberOfAssets, numberOfTimes);
            values = new Vector(numberOfTimes);
        }

        for (int i = 0; i < numberOfTimes; ++i)
        {
            int pos = timePositions[i];
            for (int j = 0; j < numberOfAssets; ++j)
            {
                path[j, i] = multiPath[j][pos];
            }
        }

        for (int i = 0; i < numberOfTimes; ++i)
        {
            values[i] = 0.0;
        }

        // ignored
        payoff.Value(path, forwardTermStructures, values, noExercises, NoStates);

        // in this engine we ignore early exercise
        double discountedPayoff = 0.0;
        for (int i = 0; i < numberOfTimes; ++i)
        {
            discountedPayoff += values[i] * discounts[i];
        }

        return discountedPayoff;
    }
}
