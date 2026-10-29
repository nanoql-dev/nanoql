// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/exoticoptions/mceverestengine.hpp ql/experimental/exoticoptions/mceverestengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The Monte Carlo engine for Everest options on correlated processes.</summary>
/// <remarks>
/// The paths run on the clock spanning every Black-Scholes process's structures, as
/// <see cref="MCEuropeanBasketEngine"/>'s do (the S-exo plan's ER5 (i); the S-xeq plan's XER5 (xxxiii)), and the
/// payoff and the yield are discounted on the first process's risk-free curve to the exercise date, nothing accrued
/// before its reference date; a market on one clock keeps QuantLib's draws and arithmetic. The draw-price-add loop
/// allocates nothing per sample after its first batch. The seed is explicit and required (the S-xeq plan's XED3).
/// </remarks>
[Experimental("NQL0001")]
public static class MCEverestEngine
{
    /// <summary>Prices an Everest option.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics.</typeparam>
    /// <param name="option">The Everest option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' correlated processes, the first a Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/>.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether to build the paths by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether to use antithetic paths.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The largest number of samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The value, where the traits allow one its error estimate, and the yield.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are neither or both given, or not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The seed or a sample count is out of range.</exception>
    /// <exception cref="NanoQLException">
    /// The notional is 0, the first process isn't a Black-Scholes process, neither samples nor a tolerance is given, or
    /// the simulation fails.
    /// </exception>
    public static EverestOptionResults Calculate<TRng, TStats>(
        EverestOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        int? timeSteps,
        int? timeStepsPerYear,
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
            option,
            processes,
            timeSteps,
            timeStepsPerYear,
            brownianBridge,
            antitheticVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(engine.Price());
    }

    // QuantLib's engine object over one call's option, its paths on the processes' clock.
    private sealed class Simulation<TRng, TStats> : McSimulation<MultiPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly EverestOption option;
        private readonly StochasticProcessArray processes;
        private readonly StochasticProcessArray processesOnClock;
        private readonly int? timeSteps;
        private readonly int? timeStepsPerYear;
        private readonly bool brownianBridge;
        private readonly int? requiredSamples;
        private readonly double? requiredTolerance;
        private readonly int? maxSamples;
        private readonly long seed;

        public Simulation(
            EverestOption option,
            StochasticProcessArray processes,
            int? timeSteps,
            int? timeStepsPerYear,
            bool brownianBridge,
            bool antitheticVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed)
            : base(antitheticVariate, false)
        {
            MCVanillaEngine<MultiVariate<TRng>, MultiPath, TStats>.CheckSteps(timeSteps, timeStepsPerYear);
            MCVanillaEngine<MultiVariate<TRng>, MultiPath, TStats>.CheckSamples(requiredSamples, maxSamples);
            Seeds.Check(seed);
            this.option = option;
            this.processes = processes;
            TermStructure? clock = MCEuropeanBasketEngine.Clock(processes);
            processesOnClock = clock is null ? processes : processes.OnClock(clock);
            this.timeSteps = timeSteps;
            this.timeStepsPerYear = timeStepsPerYear;
            this.brownianBridge = brownianBridge;
            this.requiredSamples = requiredSamples;
            this.requiredTolerance = requiredTolerance;
            this.maxSamples = maxSamples;
            this.seed = seed;
        }

        // QuantLib's calculate(): the mean, its error estimate and the yield on the discounted notional.
        public EverestOptionResults Price()
        {
            MonteCarloModel<MultiPath, TStats> model = Calculate(requiredTolerance, requiredSamples, maxSamples ?? int.MaxValue);
            double value = model.SampleAccumulator.Mean();
            return new EverestOptionResults
            {
                NPV = value,
                ErrorEstimate = TRng.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
                Yield = (value / (option.Notional * EndDiscount())) - 1.0,
            };
        }

        protected override TimeGrid TimeGrid()
        {
            double residualTime = processesOnClock.Time(option.Exercise.LastDate());
            if (timeSteps is int steps)
            {
                return new TimeGrid(residualTime, steps);
            }
            else if (timeStepsPerYear is int perYear)
            {
                int stepsFromYears = (int)(perYear * residualTime);
                return new TimeGrid(residualTime, Math.Max(stepsFromYears, 1));
            }
            else
            {
                throw new NanoQLException("time steps not specified");
            }
        }

        protected override IPathGenerator<MultiPath> PathGenerator()
        {
            int numAssets = processesOnClock.Size;
            TimeGrid grid = TimeGrid();
            IRsg gen = MultiVariate<TRng>.MakeSequenceGenerator(numAssets * (grid.Size - 1), seed);
            return MultiVariate<TRng>.MakePathGenerator(processesOnClock, grid, gen, brownianBridge);
        }

        protected override PathPricer<MultiPath> PathPricer() =>
            new EverestMultiPathPricer(option.Notional, option.Guarantee, EndDiscount());

        // by date on the first process's risk-free curve, nothing accrued before its reference date
        private double EndDiscount()
        {
            if (processes.Process(0) is not GeneralizedBlackScholesProcess process)
            {
                throw new NanoQLException("Black-Scholes process required");
            }

            return process.RiskFreeRate.AccruedDiscount(option.Exercise.LastDate());
        }
    }
}
