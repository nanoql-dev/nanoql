// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwiseaccountingengine.hpp ql/models/marketmodels/pathwiseaccountingengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The parts QuantLib's three pathwise engines repeat: their checks, their discounters, the cash-flow times each step
/// reads, and the adjoint step that carries a product's derivatives from a step's end rates to its start rates.
/// </summary>
internal static class PathwiseEngineFunctions
{
    // QuantLib: "need to check that we are in money market measure", unchecked (MMPR5 (a)).
    public static void CheckMoneyMarket(EvolutionDescription evolution, ReadOnlySpan<int> numeraires)
    {
        if (!EvolutionDescriptionFunctions.IsInMoneyMarketMeasure(evolution, numeraires))
        {
            throw new ArgumentException(
                "the pathwise engines need an evolver in the money-market measure (the discretely compounding money-market"
                + " account, each step's numeraire its first alive rate)",
                nameof(numeraires));
        }
    }

    // QuantLib sizes its buffers by the model and reads them by the product.
    public static void CheckEvolution(EvolutionDescription productEvolution, EvolutionDescription modelEvolution)
    {
        if (!productEvolution.RateTimes.AsSpan().SequenceEqual(modelEvolution.RateTimes.AsSpan())
            || !productEvolution.EvolutionTimes.AsSpan().SequenceEqual(modelEvolution.EvolutionTimes.AsSpan()))
        {
            throw new ArgumentException("the product's rate and evolution times must be the model's", nameof(productEvolution));
        }
    }

    // QuantLib's evolver steps past its last step for a product not finished there.
    public static void CheckNotFinished(bool done, int thisStep, int numberSteps)
    {
        if (!done && thisStep + 1 >= numberSteps)
        {
            throw new NanoQLException("the product isn't finished at the evolver's last step");
        }
    }

    // The caller's means and errors, QuantLib's caller vectors.
    public static void CheckResults(Span<double> means, Span<double> errors, int size)
    {
        if (means.Length < size)
        {
            throw new ArgumentException($"the means hold {means.Length} entries, fewer than the {size} results", nameof(means));
        }

        if (errors.Length < size)
        {
            throw new ArgumentException($"the errors hold {errors.Length} entries, fewer than the {size} results", nameof(errors));
        }
    }

    // QuantLib's means and standard errors from the paths' sums and sums of squares.
    public static void MeansAndErrors(
        ReadOnlySpan<double> sums, ReadOnlySpan<double> sumsqs, int numberOfPaths, Span<double> means, Span<double> errors)
    {
        for (int j = 0; j < sums.Length; ++j)
        {
            means[j] = sums[j] / numberOfPaths;
            double meanSq = sumsqs[j] / numberOfPaths;
            double variance = meanSq - (means[j] * means[j]);
            errors[j] = Math.Sqrt(variance / numberOfPaths);
        }
    }

    public static MarketModelPathwiseDiscounter[] Discounters(ReadOnlySpan<double> cashFlowTimes, ReadOnlySpan<double> rateTimes)
    {
        var discounters = new MarketModelPathwiseDiscounter[cashFlowTimes.Length];
        for (int i = 0; i < cashFlowTimes.Length; ++i)
        {
            discounters[i] = new MarketModelPathwiseDiscounter(cashFlowTimes[i], rateTimes);
        }

        return discounters;
    }

    // For each step, the cash-flow times whose flows it reads: the last evolution time at or before each.
    public static int[][] CashFlowIndicesThisStep(
        ReadOnlySpan<double> cashFlowTimes, ReadOnlySpan<double> evolutionTimes, int numberSteps)
    {
        var indices = new List<int>[numberSteps];
        for (int s = 0; s < numberSteps; ++s)
        {
            indices[s] = [];
        }

        for (int i = 0; i < cashFlowTimes.Length; ++i)
        {
            int it = 0;
            while (it < evolutionTimes.Length && !(cashFlowTimes[i] < evolutionTimes[it]))
            {
                ++it;
            }

            if (it != 0)
            {
                --it;
            }

            indices[it].Add(i);
        }

        return [.. indices.Select(l => l.ToArray())];
    }

    /// <summary>
    /// Carries a product's derivatives with respect to a step's end rates, V's row <paramref name="stepToUse"/>, to its
    /// start rates, row <paramref name="nextStepIndex"/>: the log-Euler step's Jacobian, its drift's derivative at the
    /// step's start (MMPR4 (i)), the rates displaced (MMPR4 (ii)).
    /// </summary>
    public static void UpdateV(
        Matrix v,
        Matrix pseudoRoot,
        Matrix liborRates,
        Matrix liborRatios,
        Matrix stepsDiscountsSquared,
        Matrix partials,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> displacements,
        int stepToUse,
        int nextStepIndex)
    {
        int numberRates = taus.Length;
        int factors = partials.Rows;
        ReadOnlySpan<double> rates = liborRates.Row(stepToUse);
        ReadOnlySpan<double> vEnd = v.Row(stepToUse);

        // compute partials
        for (int f = 0; f < factors; ++f)
        {
            double libor = rates[numberRates - 1];
            double vLast = vEnd[numberRates - 1];
            double pseudo = pseudoRoot[numberRates - 1, f];
            double thisPartialTerm = libor * vLast * pseudo;
            partials[f, numberRates - 1] = thisPartialTerm;

            for (int r = numberRates - 2; r >= 0; --r)
            {
                double thisPartialTermr = rates[r] * vEnd[r] * pseudoRoot[r, f];

                partials[f, r] = partials[f, r + 1] + thisPartialTermr;
            }
        }

        ReadOnlySpan<double> ratios = liborRatios.Row(stepToUse);
        ReadOnlySpan<double> squared = stepsDiscountsSquared.Row(stepToUse);
        Span<double> vStart = v.Row(nextStepIndex);
        for (int j = 0; j < numberRates; ++j)
        {
            double nextV = vEnd[j] * ratios[j];
            vStart[j] = nextV;

            double summandTerm = 0.0;
            for (int f = 0; f < factors; ++f)
            {
                summandTerm += pseudoRoot[j, f] * partials[f, j];
            }

            summandTerm *= taus[j] * squared[j] * (1.0 - (taus[j] * displacements[j]));

            vStart[j] += summandTerm;
        }
    }
}
