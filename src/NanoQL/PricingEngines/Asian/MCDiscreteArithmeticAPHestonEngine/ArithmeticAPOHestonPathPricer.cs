// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_arith_av_price_heston.hpp ql/pricingengines/asian/mc_discr_arith_av_price_heston.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.PricingEngines;

/// <summary>
/// The path pricer of a discrete arithmetic average-price Asian option on a Heston path: the discounted payoff at the
/// average of the asset's values at the fixing indices and the past fixings.
/// </summary>
/// <remarks>
/// QuantLib's. The engine gives an index once per fixing on its time, so a fixing date given twice counts twice, where
/// QuantLib's engine gives each time once.
/// </remarks>
public sealed class ArithmeticAPOHestonPathPricer : PathPricer<MultiPath>
{
    private readonly PlainVanillaPayoff payoff;
    private readonly double discount;
    private readonly ImmutableArray<int> fixingIndices;
    private readonly double runningSum;
    private readonly int pastFixings;

    /// <summary>Initializes a new instance of the <see cref="ArithmeticAPOHestonPathPricer"/> class.</summary>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike, non-negative.</param>
    /// <param name="discount">The discount factor to the exercise.</param>
    /// <param name="fixingIndices">The path's indices at the fixings, one per fixing.</param>
    /// <param name="runningSum">The sum of the past fixings.</param>
    /// <param name="pastFixings">The number of past fixings.</param>
    /// <exception cref="ArgumentException">The strike is negative ("strike less than zero not allowed"), or the indices
    /// are the default array.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pastFixings"/> or an index is negative.</exception>
    public ArithmeticAPOHestonPathPricer(
        OptionType type,
        double strike,
        double discount,
        ImmutableArray<int> fixingIndices,
        double runningSum = 0.0,
        int pastFixings = 0)
    {
        if (fixingIndices.IsDefault)
        {
            throw new ArgumentException("the fixing indices are the default array", nameof(fixingIndices));
        }

        foreach (int index in fixingIndices)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(fixingIndices));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(pastFixings);
        payoff = new PlainVanillaPayoff(type, strike);
        this.discount = discount;
        this.fixingIndices = fixingIndices;
        this.runningSum = runningSum;
        this.pastFixings = pastFixings;
        if (!(strike >= 0.0))
        {
            throw new ArgumentException("strike less than zero not allowed", nameof(strike));
        }
    }

    /// <summary>Prices a path.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The discounted payoff.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The path is empty.</exception>
    /// <exception cref="IndexOutOfRangeException">A fixing index is past the path's end.</exception>
    public override double Evaluate(MultiPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        MonteCarloPath asset = path[0];
        int n = path.PathSize();
        if (!(n > 0))
        {
            throw new NanoQLException("the path cannot be empty");
        }

        double sum = runningSum;
        int fixings = pastFixings + fixingIndices.Length;

        foreach (int fixingIndex in fixingIndices)
        {
            sum += asset[fixingIndex];
        }

        double averagePrice = sum / fixings;
        return discount * payoff.Evaluate(averagePrice);
    }
}
