// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/mceuropeanbasketengine.hpp ql/pricingengines/basket/mceuropeanbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.PricingEngines;

/// <summary>The European basket option's path pricer: the basket payoff at the paths' final prices,
/// discounted.</summary>
/// <remarks>
/// A working type: it holds the buffer it passes the final prices through, which QuantLib builds per path, so a path
/// prices without allocating after the first.
/// </remarks>
public sealed class EuropeanMultiPathPricer : PathPricer<MultiPath>
{
    private readonly BasketPayoff payoff;
    private readonly double discount;
    private double[] finalPrice = [];

    /// <summary>Initializes a new instance of the <see cref="EuropeanMultiPathPricer"/> class.</summary>
    /// <param name="payoff">The basket payoff.</param>
    /// <param name="discount">The discount factor to the exercise date.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    public EuropeanMultiPathPricer(BasketPayoff payoff, double discount)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        this.payoff = payoff;
        this.discount = discount;
    }

    /// <summary>The discounted payoff at the paths' final prices.</summary>
    /// <param name="multiPath">The assets' paths.</param>
    /// <returns>The path's value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multiPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The paths are empty, or there is no asset.</exception>
    /// <exception cref="ArgumentException">The payoff refuses the prices.</exception>
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

        if (finalPrice.Length != numAssets)
        {
            finalPrice = new double[numAssets];
        }

        for (int j = 0; j < numAssets; j++)
        {
            finalPrice[j] = multiPath[j].Back();
        }

        return payoff.Evaluate(finalPrice) * discount;
    }
}
