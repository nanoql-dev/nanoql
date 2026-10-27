// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_arith_av_price.hpp ql/pricingengines/asian/mc_discr_arith_av_price.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.PricingEngines;

/// <summary>
/// The path pricer of a discrete arithmetic average-price Asian option: the discounted payoff at the average of the
/// path's fixings and the past ones.
/// </summary>
/// <remarks>
/// QuantLib's: every node of the path a fixing but its start, which is one where the grid's first time is 0. The
/// engine's own route weighs each node by the number of fixings on its time, so a fixing date given twice counts twice
/// (the S-bar plan's BR5 (xxviii)), QuantLib's sum to the bit where each time holds one fixing.
/// </remarks>
public sealed class ArithmeticAPOPathPricer : PathPricer<MonteCarloPath>
{
    private readonly PlainVanillaPayoff payoff;
    private readonly double discount;
    private readonly double runningSum;
    private readonly int pastFixings;
    private readonly ImmutableArray<int> multiplicities;

    /// <summary>Initializes a new instance of the <see cref="ArithmeticAPOPathPricer"/> class.</summary>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike, non-negative.</param>
    /// <param name="discount">The discount factor to the exercise.</param>
    /// <param name="runningSum">The sum of the past fixings.</param>
    /// <param name="pastFixings">The number of past fixings.</param>
    /// <exception cref="ArgumentException">The strike is negative ("strike less than zero not allowed").</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pastFixings"/> is negative.</exception>
    public ArithmeticAPOPathPricer(OptionType type, double strike, double discount, double runningSum = 0.0, int pastFixings = 0)
        : this(type, strike, discount, runningSum, pastFixings, default)
    {
    }

    // the engine's route: the number of fixings at each node of the grid (BR5 (xxviii))
    internal ArithmeticAPOPathPricer(
        OptionType type, double strike, double discount, double runningSum, int pastFixings, ImmutableArray<int> multiplicities)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pastFixings);
        payoff = new PlainVanillaPayoff(type, strike);
        this.discount = discount;
        this.runningSum = runningSum;
        this.pastFixings = pastFixings;
        this.multiplicities = multiplicities;
        if (!(strike >= 0.0))
        {
            throw new ArgumentException("strike less than zero not allowed", nameof(strike));
        }
    }

    /// <summary>Prices a path.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The discounted payoff.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The path has no node after its start.</exception>
    public override double Evaluate(MonteCarloPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        int n = path.Length;
        if (!(n > 1))
        {
            throw new NanoQLException("the path cannot be empty");
        }

        double sum = runningSum;
        int fixings;
        int first = path.TimeGrid.MandatoryTimes[0] == 0.0 ? 0 : 1;
        if (multiplicities.IsDefault)
        {
            // the start is a fixing where the first time is 0
            for (int i = first; i < n; ++i)
            {
                sum += path[i];
            }

            fixings = pastFixings + n - first;
        }
        else
        {
            fixings = pastFixings;
            for (int i = first; i < n; ++i)
            {
                int m = multiplicities[i];
                if (m != 0)
                {
                    sum += m == 1 ? path[i] : m * path[i];
                    fixings += m;
                }
            }
        }

        double averagePrice = sum / fixings;
        return discount * payoff.Evaluate(averagePrice);
    }
}
