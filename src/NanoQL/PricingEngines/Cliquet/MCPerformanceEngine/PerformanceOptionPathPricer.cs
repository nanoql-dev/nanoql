// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/cliquet/mcperformanceengine.hpp ql/pricingengines/cliquet/mcperformanceengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.PricingEngines;

/// <summary>
/// The performance option's path pricer: each period's return under a plain vanilla payoff, discounted from its end.
/// </summary>
/// <remarks>
/// Immutable. QuantLib reads the periods as the path's consecutive points after its start, refusing a path of another
/// length ("discounts/options mismatch"), so a reset at the grid's start, which the grid merges into it, is refused (the
/// S-exo plan's ER5 (xxxix), <c>Divergences/McPerformanceResetTodayTests.cs</c>); given the resets' indices on the
/// grid, it reads each period between its own points, as the Monte Carlo forward engines read their reset, and on a
/// grid of distinct reset times the same points as QuantLib's.
/// </remarks>
public sealed class PerformanceOptionPathPricer : PathPricer<MonteCarloPath>
{
    private readonly double strike;
    private readonly OptionType type;
    private readonly ImmutableArray<double> discounts;
    private readonly ImmutableArray<int> indices;

    /// <summary>Initializes a new instance of the <see cref="PerformanceOptionPathPricer"/> class, QuantLib's.</summary>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike, a moneyness.</param>
    /// <param name="discounts">The discount factors to each reset and to expiry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="discounts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    public PerformanceOptionPathPricer(OptionType type, double strike, IEnumerable<double> discounts)
    {
        ArgumentNullException.ThrowIfNull(discounts);
        EnumArguments.Check(type, nameof(type));
        this.strike = strike;
        this.type = type;
        this.discounts = [.. discounts];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceOptionPathPricer"/> class reading each reset at its
    /// index on the path's grid.
    /// </summary>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike, a moneyness.</param>
    /// <param name="discounts">The discount factors to each reset and to expiry.</param>
    /// <param name="indices">The grid's index of each reset and of expiry, one per discount.</param>
    /// <exception cref="ArgumentNullException">A list is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The lists' lengths differ.</exception>
    public PerformanceOptionPathPricer(
        OptionType type, double strike, IEnumerable<double> discounts, IEnumerable<int> indices)
        : this(type, strike, discounts)
    {
        ArgumentNullException.ThrowIfNull(indices);
        this.indices = [.. indices];
        if (this.indices.Length != this.discounts.Length)
        {
            throw new ArgumentException("discounts/options mismatch", nameof(indices));
        }
    }

    /// <summary>The periods' discounted payoffs on a path.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// Without indices, the path's length isn't the discounts' and one more; with them, an index is past the path.
    /// </exception>
    public override double Evaluate(MonteCarloPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        double sum = 0.0;
        if (indices.IsDefault)
        {
            int n = path.Length;
            if (!(n == discounts.Length + 1))
            {
                throw new NanoQLException("discounts/options mismatch");
            }

            for (int i = 2; i < n; i++)
            {
                sum += discounts[i - 1] * PlainVanillaPayoff.Value(type, strike, path[i] / path[i - 1]);
            }

            return sum;
        }

        if (!(indices[^1] < path.Length))
        {
            throw new NanoQLException("discounts/options mismatch");
        }

        for (int k = 1; k < indices.Length; k++)
        {
            sum += discounts[k] * PlainVanillaPayoff.Value(type, strike, path[indices[k]] / path[indices[k - 1]]);
        }

        return sum;
    }
}
