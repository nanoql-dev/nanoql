// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market the market-model product facts run on, the S-mmp probes' <c>common.hpp</c>: rate times t₀, t₀ + τ, …;
/// forwards f₀ + slope · i, paid at each period's end on its accrual; discounts from 0.95; a FlatVol model on an
/// exponential correlation (0.5, 0.2); and QuantLib's <c>simulate</c> through the accounting engine.
/// </summary>
internal sealed class MarketModelProductMarket
{
    public MarketModelProductMarket(int n, double t0 = 0.5, double tau = 0.5, double f0 = 0.03, double slope = 0.001)
    {
        RateTimes = new double[n + 1];
        Accruals = new double[n];
        PaymentTimes = new double[n];
        Forwards = new double[n];
        Discounts = new double[n + 1];
        for (int i = 0; i <= n; ++i)
        {
            RateTimes[i] = t0 + (tau * i);
        }

        for (int i = 0; i < n; ++i)
        {
            Accruals[i] = RateTimes[i + 1] - RateTimes[i];
            PaymentTimes[i] = RateTimes[i + 1];
            Forwards[i] = f0 + (slope * i);
        }

        Discounts[0] = 0.95;
        for (int i = 0; i < n; ++i)
        {
            Discounts[i + 1] = Discounts[i] / (1.0 + (Forwards[i] * Accruals[i]));
        }
    }

    public double[] RateTimes { get; }

    public double[] Accruals { get; }

    public double[] PaymentTimes { get; }

    public double[] Forwards { get; }

    public double[] Discounts { get; }

    public int N => Forwards.Length;

    // The statistics' means and error estimates, QuantLib's simulate over the paths.
    public static (ImmutableArray<double> Means, ImmutableArray<double> Errors) Simulate(
        MarketModelEvolver evolver, MarketModelMultiProduct product, double initialNumeraireValue, int paths)
    {
        var engine = new AccountingEngine(evolver, product, initialNumeraireValue);
        var stats = new SequenceStatisticsInc(product.NumberOfProducts);
        engine.MultiplePathValues(stats, paths);
        return (stats.Mean(), stats.ErrorEstimate());
    }

    public MarketModel FlatVol(EvolutionDescription evolution, int factors, double vol = 0.20, double displacement = 0.0)
    {
        Matrix corr = ExpCorrelationsFunctions.ExponentialCorrelations(evolution.RateTimes.AsSpan(), 0.5, 0.2);
        var c = new TimeHomogeneousForwardCorrelation(corr, evolution.RateTimes.AsSpan());
        return new FlatVol(
            [.. Enumerable.Repeat(vol, N)], c, evolution, factors, Forwards, [.. Enumerable.Repeat(displacement, N)]);
    }

    // The probes' route: QuantLib's LogNormalFwdRatePc on the Mersenne twister, in the terminal measure, valued from
    // the terminal bond's discount.
    public (ImmutableArray<double> Means, ImmutableArray<double> Errors) TerminalPc(
        MarketModelMultiProduct product, int factors, int paths, long seed = 42, double vol = 0.20)
    {
        EvolutionDescription evolution = product.Evolution;
        ImmutableArray<int> numeraires = EvolutionDescriptionFunctions.TerminalMeasure(evolution);
        var evolver = new LogNormalFwdRatePc(
            FlatVol(evolution, factors, vol), new MTBrownianGeneratorFactory(seed), numeraires.AsSpan());
        return Simulate(evolver, product, Discounts[numeraires[0]], paths);
    }
}
