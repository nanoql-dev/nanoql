// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/piecewiseconstantvariance.hpp ql/models/marketmodels/models/piecewiseconstantvariance.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A rate's variance, piecewise constant over the steps between its rate times: each step's variance and volatility,
/// and their totals from time zero.
/// </summary>
/// <remarks>Immutable. An extension point, as in QuantLib.</remarks>
public abstract class PiecewiseConstantVariance
{
    /// <summary>Initializes a new instance of the <see cref="PiecewiseConstantVariance"/> class.</summary>
    protected PiecewiseConstantVariance()
    {
    }

    /// <summary>Gets each step's variance.</summary>
    public abstract ImmutableArray<double> Variances { get; }

    /// <summary>Gets each step's volatility.</summary>
    public abstract ImmutableArray<double> Volatilities { get; }

    /// <summary>Gets the rate times ending the steps.</summary>
    public abstract ImmutableArray<double> RateTimes { get; }

    /// <summary>The <paramref name="i"/>-th step's variance.</summary>
    /// <param name="i">The step, below the number of variances.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public double Variance(int i)
    {
        ImmutableArray<double> variances = Variances;
        CheckStep(i, variances.Length);
        return variances[i];
    }

    /// <summary>The <paramref name="i"/>-th step's volatility.</summary>
    /// <param name="i">The step, below the number of volatilities.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public double Volatility(int i)
    {
        ImmutableArray<double> volatilities = Volatilities;
        CheckStep(i, volatilities.Length);
        return volatilities[i];
    }

    /// <summary>The variance from time zero to the end of the <paramref name="i"/>-th step.</summary>
    /// <param name="i">The last step summed, below the number of variances.</param>
    /// <returns>The total variance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public double TotalVariance(int i)
    {
        ImmutableArray<double> variances = Variances;
        CheckStep(i, variances.Length);
        double sum = 0.0;
        for (int k = 0; k <= i; ++k)
        {
            sum += variances[k];
        }

        return sum;
    }

    /// <summary>The volatility from time zero to the end of the <paramref name="i"/>-th step.</summary>
    /// <param name="i">The last step, below the number of variances.</param>
    /// <returns>The total variance over the <paramref name="i"/>-th rate time, its square root.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step or a rate time.</exception>
    public double TotalVolatility(int i)
    {
        double total = TotalVariance(i);

        // QuantLib reads the rate time unchecked, past an extension's rate times.
        ImmutableArray<double> rateTimes = RateTimes;
        CheckStep(i, rateTimes.Length);
        return Math.Sqrt(total / rateTimes[i]);
    }

    private static void CheckStep(int i, int size)
    {
        if (!(i >= 0 && i < size))
        {
            throw new ArgumentOutOfRangeException(nameof(i), i, "invalid step index");
        }
    }
}
