// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/mchimalayaengine.hpp ql/experimental/exoticoptions/mchimalayaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>The Himalaya option's path pricer: at each fixing after the first node the best performer left joins the
/// average and leaves; the payoff at the average, discounted.</summary>
/// <remarks>
/// A working type: it holds the flags of the assets left, which QuantLib builds per path, so a path prices without
/// allocating after the first.
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class HimalayaMultiPathPricer : PathPricer<MultiPath>
{
    private readonly Payoff payoff;
    private readonly double discount;
    private bool[] remainingAssets = [];

    /// <summary>Initializes a new instance of the <see cref="HimalayaMultiPathPricer"/> class.</summary>
    /// <param name="payoff">The payoff on the average.</param>
    /// <param name="discount">The discount factor to the exercise date.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    public HimalayaMultiPathPricer(Payoff payoff, double discount)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        this.payoff = payoff;
        this.discount = discount;
    }

    /// <summary>The discounted payoff at the average of the best performers.</summary>
    /// <param name="multiPath">The assets' paths.</param>
    /// <returns>The path's value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multiPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">There is no asset.</exception>
    public override double Evaluate(MultiPath multiPath)
    {
        ArgumentNullException.ThrowIfNull(multiPath);
        int numAssets = multiPath.AssetNumber;
        int numNodes = multiPath.PathSize();
        if (!(numAssets > 0))
        {
            throw new NanoQLException("no asset given");
        }

        if (remainingAssets.Length != numAssets)
        {
            remainingAssets = new bool[numAssets];
        }

        Array.Fill(remainingAssets, true);
        double averagePrice = 0.0;
        int fixings = numNodes - 1;
        for (int i = 1; i < numNodes; i++)
        {
            double bestPrice = 0.0;
            double bestYield = double.MinValue;

            // dummy assignement to avoid compiler warning
            int removeAsset = 0;
            for (int j = 0; j < numAssets; j++)
            {
                if (remainingAssets[j])
                {
                    double price = multiPath[j][i];
                    double yield = price / multiPath[j].Front();
                    if (yield >= bestYield)
                    {
                        bestPrice = price;
                        bestYield = yield;
                        removeAsset = j;
                    }
                }
            }

            remainingAssets[removeAsset] = false;
            averagePrice += bestPrice;
        }

        averagePrice /= Math.Min(fixings, numAssets);
        double value = payoff.Evaluate(averagePrice);
        return value * discount;
    }
}
