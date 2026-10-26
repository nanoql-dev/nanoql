// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/basketoption.hpp ql/instruments/basketoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Instruments;

/// <summary>A basket payoff on the weighted sum of the assets' prices.</summary>
/// <remarks>
/// A price count other than the weights' is refused, with the text of QuantLib's Choi, Deng-Li-Zhou and single-factor
/// engines, where QuantLib's <c>inner_product</c> reads past a shorter array or sums a partial basket, so that its
/// Monte Carlo and finite-difference engines price a run-dependent value or a partial basket there
/// (<c>Divergences/AverageBasketPayoffWeightCountTests.cs</c>).
/// </remarks>
public sealed class AverageBasketPayoff : BasketPayoff
{
    private readonly ImmutableArray<double> weights;

    /// <summary>Initializes a new instance of the <see cref="AverageBasketPayoff"/> class with given weights.</summary>
    /// <param name="basePayoff">The one-dimensional payoff applied to the weighted sum.</param>
    /// <param name="weights">The weights, one per asset, copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="basePayoff"/> is <see langword="null"/>.</exception>
    public AverageBasketPayoff(Payoff basePayoff, ReadOnlySpan<double> weights)
        : base(basePayoff)
    {
        this.weights = [.. weights];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AverageBasketPayoff"/> class with equal weights, each 1/n.
    /// </summary>
    /// <param name="basePayoff">The one-dimensional payoff applied to the average.</param>
    /// <param name="n">The number of assets.</param>
    /// <exception cref="ArgumentNullException"><paramref name="basePayoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    public AverageBasketPayoff(Payoff basePayoff, int n)
        : base(basePayoff)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        double w = 1.0 / n;
        var builder = ImmutableArray.CreateBuilder<double>(n);
        for (int i = 0; i < n; ++i)
        {
            builder.Add(w);
        }

        weights = builder.MoveToImmutable();
    }

    /// <summary>Gets the weights.</summary>
    public ImmutableArray<double> Weights => weights;

    /// <summary>The weighted sum of the prices, summed in order from 0, as <c>std::inner_product</c>.</summary>
    /// <param name="a">The assets' prices, one per weight.</param>
    /// <returns>The weighted sum.</returns>
    /// <exception cref="ArgumentException"><paramref name="a"/>'s length isn't the number of weights.</exception>
    public override double Accumulate(ReadOnlySpan<double> a)
    {
        // QuantLib's inner_product runs over the weights and reads a unchecked
        if (a.Length != weights.Length)
        {
            throw new ArgumentException("wrong number of weights arguments in payoff", nameof(a));
        }

        double sum = 0.0;
        for (int i = 0; i < weights.Length; ++i)
        {
            sum = sum + (weights[i] * a[i]);
        }

        return sum;
    }
}
