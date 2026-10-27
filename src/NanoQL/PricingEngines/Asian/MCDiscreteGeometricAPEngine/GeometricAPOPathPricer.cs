// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_geom_av_price.hpp ql/pricingengines/asian/mc_discr_geom_av_price.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.PricingEngines;

/// <summary>
/// The path pricer of a discrete geometric average-price Asian option: the discounted payoff at the geometric average of
/// the path's fixings and the past ones.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's: every node of the path a fixing but its start, which is one where the grid's first time is 0; the
/// product flushed into the average, as its root, where the next factor would overflow it.
/// </para>
/// <para>
/// Documented divergences (the S-bar plan's BR5). The product is flushed as well where the next factor would take it
/// below double's normal range, where QuantLib's underflows: 252 daily fixings at spot 0.01 price 0 in QuantLib ((xli),
/// <c>Divergences/GeometricAsianProductRangeTests.cs</c>); the engine's route takes the past fixings' log where their
/// product isn't finite and normal ((xli)), and weighs each node by the number of fixings on its time ((xxviii),
/// <c>Divergences/AsianDuplicateFixingsTests.cs</c>). QuantLib's arithmetic and bits wherever the product stays in
/// range and each time holds one fixing.
/// </para>
/// </remarks>
public sealed class GeometricAPOPathPricer : PathPricer<MonteCarloPath>
{
    private readonly PlainVanillaPayoff payoff;
    private readonly double discount;
    private readonly double runningProduct;
    private readonly double runningLog;
    private readonly int pastFixings;
    private readonly ImmutableArray<int> multiplicities;

    /// <summary>Initializes a new instance of the <see cref="GeometricAPOPathPricer"/> class.</summary>
    /// <param name="type">The option type.</param>
    /// <param name="strike">The strike, non-negative.</param>
    /// <param name="discount">The discount factor to the exercise.</param>
    /// <param name="runningProduct">The product of the past fixings.</param>
    /// <param name="pastFixings">The number of past fixings.</param>
    /// <exception cref="ArgumentException">The strike is negative ("negative strike given").</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pastFixings"/> is negative.</exception>
    public GeometricAPOPathPricer(OptionType type, double strike, double discount, double runningProduct = 1.0, int pastFixings = 0)
        : this(type, strike, discount, runningProduct, double.NaN, pastFixings, default)
    {
    }

    // the engine's route: the past fixings' log where their product isn't finite and normal, NaN for the product
    // (BR5 (xli)), and the number of fixings at each node of the grid (BR5 (xxviii))
    internal GeometricAPOPathPricer(
        OptionType type,
        double strike,
        double discount,
        double runningProduct,
        double runningLog,
        int pastFixings,
        ImmutableArray<int> multiplicities)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pastFixings);
        payoff = new PlainVanillaPayoff(type, strike);
        this.discount = discount;
        this.runningProduct = runningProduct;
        this.runningLog = runningLog;
        this.pastFixings = pastFixings;
        this.multiplicities = multiplicities;
        if (!(strike >= 0.0))
        {
            throw new ArgumentException("negative strike given", nameof(strike));
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
        int n = path.Length - 1;
        if (!(n > 0))
        {
            throw new NanoQLException("the path cannot be empty");
        }

        bool startFixing = path.TimeGrid.MandatoryTimes[0] == 0.0;
        int fixings = pastFixings;
        if (multiplicities.IsDefault)
        {
            fixings += startFixing ? n + 1 : n;
        }
        else
        {
            for (int i = startFixing ? 0 : 1; i <= n; ++i)
            {
                fixings += multiplicities[i];
            }
        }

        var product = new GeometricProduct(double.IsNaN(runningLog) ? runningProduct : 1.0, fixings);
        if (!double.IsNaN(runningLog))
        {
            product.AverageLog(runningLog);
        }

        if (startFixing)
        {
            product.Multiply(path.Front(), multiplicities.IsDefault ? 1 : multiplicities[0]);
        }

        for (int i = 1; i < n + 1; i++)
        {
            product.Multiply(path[i], multiplicities.IsDefault ? 1 : multiplicities[i]);
        }

        return discount * payoff.Evaluate(product.Average());
    }
}
