// SPDX-FileCopyrightText: Copyright (C) 2020 Jack Gillett
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_geom_av_price_heston.hpp ql/pricingengines/asian/mc_discr_geom_av_price_heston.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.PricingEngines;

/// <summary>
/// The path pricer of a discrete geometric average-price Asian option on a Heston path: the discounted payoff at the
/// geometric average of the asset's values at the fixing indices and the past fixings.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's: the product flushed into the average, as its root, where the next factor would overflow it.
/// </para>
/// <para>
/// Documented divergences. The product is flushed as well where the next factor would take it below double's normal
/// range, where QuantLib's underflows (<c>Divergences/GeometricAsianProductRangeTests.cs</c>); the engine's route takes
/// the past fixings' log where their product isn't finite and normal, and gives an index once per fixing on its time
/// (<c>Divergences/AsianDuplicateFixingsTests.cs</c>). QuantLib's arithmetic and bits wherever the product stays in
/// range.
/// </para>
/// </remarks>
public sealed class GeometricAPOHestonPathPricer : PathPricer<MultiPath>
{
    private readonly PlainVanillaPayoff payoff;
    private readonly double discount;
    private readonly ImmutableArray<int> fixingIndices;
    private readonly double runningProduct;
    private readonly double runningLog;
    private readonly int pastFixings;

    /// <summary>Initializes a new instance of the <see cref="GeometricAPOHestonPathPricer"/> class.</summary>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike, non-negative.</param>
    /// <param name="discount">The discount factor to the exercise.</param>
    /// <param name="fixingIndices">The path's indices at the fixings, one per fixing.</param>
    /// <param name="runningProduct">The product of the past fixings.</param>
    /// <param name="pastFixings">The number of past fixings.</param>
    /// <exception cref="ArgumentException">The strike is negative ("strike less than zero not allowed"), or the indices
    /// are the default array.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pastFixings"/> or an index is negative.</exception>
    public GeometricAPOHestonPathPricer(
        OptionType type,
        double strike,
        double discount,
        ImmutableArray<int> fixingIndices,
        double runningProduct = 1.0,
        int pastFixings = 0)
        : this(type, strike, discount, fixingIndices, runningProduct, double.NaN, pastFixings)
    {
    }

    // the engine's route: the past fixings' log where their product isn't finite and normal, NaN for the product
    //
    internal GeometricAPOHestonPathPricer(
        OptionType type,
        double strike,
        double discount,
        ImmutableArray<int> fixingIndices,
        double runningProduct,
        double runningLog,
        int pastFixings)
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
        this.runningProduct = runningProduct;
        this.runningLog = runningLog;
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

        int fixings = pastFixings + fixingIndices.Length;
        var product = new GeometricProduct(double.IsNaN(runningLog) ? runningProduct : 1.0, fixings);
        if (!double.IsNaN(runningLog))
        {
            product.AverageLog(runningLog);
        }

        foreach (int fixingIndex in fixingIndices)
        {
            product.Multiply(asset[fixingIndex], 1);
        }

        return discount * payoff.Evaluate(product.Average());
    }
}
