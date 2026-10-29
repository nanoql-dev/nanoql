// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/mchimalayaengine.hpp ql/experimental/exoticoptions/mchimalayaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The Monte Carlo engine for Himalaya options on correlated processes.</summary>
/// <remarks>
/// The paths step from the evaluation date to each fixing date on the clock spanning every Black-Scholes process's
/// structures, as <see cref="MCEuropeanBasketEngine"/>'s do (the S-exo plan's ER5 (i); the S-xeq plan's XER5
/// (xxxiii)), and the payoff is discounted on the first process's risk-free curve to the last fixing date, nothing
/// accrued before its reference date; a market on one clock keeps QuantLib's draws and arithmetic. The seed is explicit
/// and required (the S-xeq plan's XED3).
/// </remarks>
[Experimental("NQL0001")]
public static class MCHimalayaEngine
{
    /// <summary>Prices a Himalaya option.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics.</typeparam>
    /// <param name="option">The Himalaya option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' correlated processes, the first a Black-Scholes process.</param>
    /// <param name="brownianBridge">Whether to build the paths by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether to use antithetic paths.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The largest number of samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The value and, where the traits allow one, its error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The seed or a sample count is out of range.</exception>
    /// <exception cref="NanoQLException">
    /// A fixing date precedes the evaluation date ("seasoned options are not handled") or the dates aren't sorted, the
    /// first process isn't a Black-Scholes process, neither samples nor a tolerance is given, or the simulation fails.
    /// </exception>
    public static MultiAssetOptionResults Calculate<TRng, TStats>(
        HimalayaOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(processes);
        var engine = new Simulation<TRng, TStats>(
            option, processes, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(engine.Price());
    }

    // QuantLib's grid on the fixing times: each on the clock, none before 0, strictly increasing.
    internal static TimeGrid FixingGrid(StochasticProcessArray processesOnClock, ImmutableArray<Date> fixingDates)
    {
        var fixingTimes = new List<double>(fixingDates.Length);
        for (int i = 0; i < fixingDates.Length; i++)
        {
            double t = processesOnClock.Time(fixingDates[i]);
            if (!(t >= 0.0))
            {
                throw new NanoQLException("seasoned options are not handled");
            }

            if (i > 0 && !(t > fixingTimes[^1]))
            {
                throw new NanoQLException("fixing dates not sorted");
            }

            fixingTimes.Add(t);
        }

        return new TimeGrid([.. fixingTimes]);
    }

    // QuantLib's engine object over one call's option, its paths on the processes' clock.
    private sealed class Simulation<TRng, TStats> : McSimulation<MultiPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly HimalayaOption option;
        private readonly StochasticProcessArray processes;
        private readonly StochasticProcessArray processesOnClock;
        private readonly bool brownianBridge;
        private readonly int? requiredSamples;
        private readonly double? requiredTolerance;
        private readonly int? maxSamples;
        private readonly long seed;

        public Simulation(
            HimalayaOption option,
            StochasticProcessArray processes,
            bool brownianBridge,
            bool antitheticVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed)
            : base(antitheticVariate, false)
        {
            MCVanillaEngine<MultiVariate<TRng>, MultiPath, TStats>.CheckSamples(requiredSamples, maxSamples);
            Seeds.Check(seed);
            this.option = option;
            this.processes = processes;
            TermStructure? clock = MCEuropeanBasketEngine.Clock(processes);
            processesOnClock = clock is null ? processes : processes.OnClock(clock);
            this.brownianBridge = brownianBridge;
            this.requiredSamples = requiredSamples;
            this.requiredTolerance = requiredTolerance;
            this.maxSamples = maxSamples;
            this.seed = seed;
        }

        // QuantLib's calculate().
        public MultiAssetOptionResults Price()
        {
            MonteCarloModel<MultiPath, TStats> model = Calculate(requiredTolerance, requiredSamples, maxSamples ?? int.MaxValue);
            return new MultiAssetOptionResults
            {
                NPV = model.SampleAccumulator.Mean(),
                ErrorEstimate = TRng.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
            };
        }

        protected override TimeGrid TimeGrid() => FixingGrid(processesOnClock, option.FixingDates);

        protected override IPathGenerator<MultiPath> PathGenerator()
        {
            int numAssets = processesOnClock.Size;
            TimeGrid grid = TimeGrid();
            IRsg gen = MultiVariate<TRng>.MakeSequenceGenerator(numAssets * (grid.Size - 1), seed);
            return MultiVariate<TRng>.MakePathGenerator(processesOnClock, grid, gen, brownianBridge);
        }

        protected override PathPricer<MultiPath> PathPricer()
        {
            if (processes.Process(0) is not GeneralizedBlackScholesProcess process)
            {
                throw new NanoQLException("Black-Scholes process required");
            }

            // by date on the first process's risk-free curve, nothing accrued before its reference date
            return new HimalayaMultiPathPricer(
                option.Payoff!, process.RiskFreeRate.AccruedDiscount(option.Exercise.LastDate()));
        }
    }
}
