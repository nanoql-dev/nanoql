// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/marketmodel.hpp ql/models/marketmodels/marketmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A market model: for each evolution step, the pseudo-square root of the rates' covariance over the step.
/// </summary>
/// <remarks>
/// <para>
/// Immutable and thread-safe: a model may serve many evolvers on many threads, each evolver copying the pseudo-roots it
/// reads. A matrix read returns a copy. An extension point, as in QuantLib.
/// </para>
/// <para>
/// QuantLib's covariance caches are kept (the rule on QuantLib's speed caches): each step's covariance, the pseudo-root
/// times its transpose, and their running totals are computed once, at the first read of either, from the model's own
/// pseudo-roots, under a lock, and every read returns a copy, so an answer is what a fresh model returns, whatever was
/// read before and on whichever thread.
/// </para>
/// </remarks>
public abstract class MarketModel
{
    private object? cacheLock;
    private Matrix[]? covariances;
    private Matrix[]? totalCovariances;

    /// <summary>Initializes a new instance of the <see cref="MarketModel"/> class.</summary>
    protected MarketModel()
    {
    }

    /// <summary>Gets the rates at time zero.</summary>
    public abstract ImmutableArray<double> InitialRates { get; }

    /// <summary>Gets the rates' displacements.</summary>
    public abstract ImmutableArray<double> Displacements { get; }

    /// <summary>Gets the evolution the model is built on.</summary>
    public abstract EvolutionDescription Evolution { get; }

    /// <summary>Gets the number of rates.</summary>
    public abstract int NumberOfRates { get; }

    /// <summary>Gets the number of factors.</summary>
    public abstract int NumberOfFactors { get; }

    /// <summary>Gets the number of evolution steps.</summary>
    public abstract int NumberOfSteps { get; }

    /// <summary>The pseudo-square root of the <paramref name="i"/>-th step's covariance, rates by factors.</summary>
    /// <param name="i">The step, below the number of steps.</param>
    /// <returns>A copy of the step's pseudo-root.</returns>
    public abstract Matrix PseudoRoot(int i);

    /// <summary>The <paramref name="i"/>-th step's covariance, its pseudo-root times its transpose.</summary>
    /// <param name="i">The step, below the number of steps.</param>
    /// <returns>A copy of the cached covariance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public virtual Matrix Covariance(int i)
    {
        Matrix[] cache = Covariances();
        if (!(i >= 0 && i < cache.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                string.Create(CultureInfo.InvariantCulture, $"i ({i}) must be less than covariance_.size() ({cache.Length})"));
        }

        return cache[i].Clone();
    }

    /// <summary>The covariance from time zero to the end of the <paramref name="endIndex"/>-th step.</summary>
    /// <param name="endIndex">The last step summed, below the number of steps.</param>
    /// <returns>A copy of the cached total.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="endIndex"/> isn't a step.</exception>
    public virtual Matrix TotalCovariance(int endIndex)
    {
        Matrix[] cache = TotalCovariances();
        if (!(endIndex >= 0 && endIndex < cache.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(endIndex),
                string.Create(
                    CultureInfo.InvariantCulture, $"endIndex ({endIndex}) must be less than covariance_.size() ({cache.Length})"));
        }

        return cache[endIndex].Clone();
    }

    /// <summary>The <paramref name="i"/>-th rate's volatility over each step.</summary>
    /// <param name="i">The rate, below the number of rates.</param>
    /// <returns>One volatility per step, the square root of the step's variance over its length.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a rate.</exception>
    public ImmutableArray<double> TimeDependentVolatility(int i)
    {
        if (!(i >= 0 && i < NumberOfRates))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                string.Create(CultureInfo.InvariantCulture, $"index ({i}) must less than number of rates ({NumberOfRates})"));
        }

        double[] result = new double[NumberOfSteps];
        ImmutableArray<double> evolutionTime = Evolution.EvolutionTimes;
        double lastTime = 0.0;
        for (int j = 0; j < NumberOfSteps; ++j)
        {
            double tau = evolutionTime[j] - lastTime;
            double thisVariance = Covariance(j)[i, i];
            double thisVol = Math.Sqrt(thisVariance / tau);
            result[j] = thisVol;
            lastTime = evolutionTime[j];
        }

        return ImmutableArray.Create(result);
    }

    // QuantLib's covariance_: each step's pseudo-root times its transpose.
    private Matrix[] Covariances() =>
        LazyInitializer.EnsureInitialized(ref covariances, ref cacheLock, () =>
        {
            var c = new Matrix[NumberOfSteps];
            for (int j = 0; j < c.Length; ++j)
            {
                Matrix a = PseudoRoot(j);
                c[j] = a * MatrixFunctions.Transpose(a);
            }

            return c;
        });

    // QuantLib's totalCovariance_: the running sums of the steps' covariances.
    private Matrix[] TotalCovariances() =>
        LazyInitializer.EnsureInitialized(ref totalCovariances, ref cacheLock, () =>
        {
            Matrix[] c = Covariances();
            var total = new Matrix[c.Length];
            total[0] = c[0];
            for (int j = 1; j < c.Length; ++j)
            {
                total[j] = total[j - 1] + c[j];
            }

            return total;
        });
}
