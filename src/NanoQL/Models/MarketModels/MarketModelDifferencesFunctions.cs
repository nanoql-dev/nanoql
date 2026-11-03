// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/marketmodeldifferences.hpp ql/models/marketmodels/marketmodeldifferences.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: coterminalSwapPseudoRoots, never ported: marketmodeldifferences.hpp declares it and nothing defines it, and its two-argument definition in marketmodeldifferences.cpp, in no header, refuses every QuantLib correlation, reads past the variances otherwise, and scales the correlation, not a root of it

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The differences between two market models on the same initial rates and evolution: their rates' volatilities and
/// instantaneous volatilities. QuantLib's free functions in <c>marketmodeldifferences.hpp</c>.
/// </summary>
public static class MarketModelDifferencesFunctions
{
    /// <summary>
    /// Each rate's volatility difference over its life, √((Σ₁ − Σ₂)ᵢᵢ / min(Tᵢ, tₙ)): the two models' total covariances
    /// to their last steps, the rate's variance difference over the time it accrues, to its reset time Tᵢ or to the
    /// evolution's last time tₙ, whichever comes first.
    /// </summary>
    /// <remarks>
    /// A rate's variance is divided by the time it accrues over (a documented divergence): QuantLib divides rate i's by
    /// the i-th evolution time (<c>marketmodeldifferences.cpp:50</c>), √2 too high on quarterly steps over semi-annual
    /// rates and a read past the evolution times where the rates outnumber the steps. The total covariance accrues
    /// nothing past the evolution's last time, so on an evolution ending before a rate resets, the rate's reset time
    /// alone would read it low (<c>Divergences/MarketModelDifferencesTests.cs</c>). Where the evolution times are the
    /// rate times, QuantLib's value.
    /// </remarks>
    /// <param name="marketModel1">The first model.</param>
    /// <param name="marketModel2">The second model.</param>
    /// <returns>The differences, one a rate.</returns>
    /// <exception cref="NanoQLException">The models' initial rates or evolution times differ.</exception>
    public static ImmutableArray<double> RateVolDifferences(MarketModel marketModel1, MarketModel marketModel2)
    {
        CheckMatch(marketModel1, marketModel2);
        Matrix totalCovariance1 = marketModel1.TotalCovariance(marketModel1.NumberOfSteps - 1);
        Matrix totalCovariance2 = marketModel2.TotalCovariance(marketModel2.NumberOfSteps - 1);
        ImmutableArray<double> resetTimes = marketModel1.Evolution.RateTimes;
        double lastTime = marketModel1.Evolution.EvolutionTimes[^1];

        var result = new double[totalCovariance1.Columns];
        for (int i = 0; i < totalCovariance1.Columns; ++i)
        {
            double diff = totalCovariance1[i, i] - totalCovariance2[i, i];
            result[i] = Math.Sqrt(diff / Math.Min(resetTimes[i], lastTime));
        }

        return ImmutableArray.Create(result);
    }

    /// <summary>
    /// A rate's instantaneous volatility difference at each step, √((C₁ − C₂)ᵢᵢ / Δt): the two models' covariances of
    /// the step, the rate's variance difference over the step's length.
    /// </summary>
    /// <remarks>
    /// The rate's index is checked against the number of rates (a documented divergence): QuantLib checks it against
    /// the number of steps (<c>marketmodeldifferences.cpp:68</c>), refusing valid rates on coarse steps and reading
    /// past the covariance on fine ones.
    /// </remarks>
    /// <param name="marketModel1">The first model.</param>
    /// <param name="marketModel2">The second model.</param>
    /// <param name="index">The rate's index.</param>
    /// <returns>The differences, one a step.</returns>
    /// <exception cref="NanoQLException">
    /// The models' initial rates or evolution times differ, or the index is past the rates.
    /// </exception>
    public static ImmutableArray<double> RateInstVolDifferences(MarketModel marketModel1, MarketModel marketModel2, int index)
    {
        CheckMatch(marketModel1, marketModel2);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= marketModel1.NumberOfRates)
        {
            throw new NanoQLException("the index given is greater than the number of rates");
        }

        ImmutableArray<double> evolutionTimes = marketModel1.Evolution.EvolutionTimes;
        var result = new double[evolutionTimes.Length];

        double previousEvolutionTime = 0;
        for (int i = 0; i < evolutionTimes.Length; ++i)
        {
            double currentEvolutionTime = evolutionTimes[i];
            double dt = currentEvolutionTime - previousEvolutionTime;
            Matrix covariance1 = marketModel1.Covariance(i);
            Matrix covariance2 = marketModel2.Covariance(i);
            double diff = covariance1[index, index] - covariance2[index, index];
            result[i] = Math.Sqrt(diff / dt);
            previousEvolutionTime = currentEvolutionTime;
        }

        return ImmutableArray.Create(result);
    }

    private static void CheckMatch(MarketModel marketModel1, MarketModel marketModel2)
    {
        ArgumentNullException.ThrowIfNull(marketModel1);
        ArgumentNullException.ThrowIfNull(marketModel2);
        if (!marketModel1.InitialRates.AsSpan().SequenceEqual(marketModel2.InitialRates.AsSpan()))
        {
            throw new NanoQLException("initialRates do not match");
        }

        if (!marketModel1.Evolution.EvolutionTimes.AsSpan().SequenceEqual(marketModel2.Evolution.EvolutionTimes.AsSpan()))
        {
            throw new NanoQLException("Evolution times do not match");
        }
    }
}
