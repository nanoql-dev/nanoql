// SPDX-FileCopyrightText: Copyright (C) 2008 Andrea Odetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/mcbasket/mcpathbasketengine.hpp ql/experimental/mcbasket/mcpathbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>The Monte Carlo engine for path options on correlated processes, their early exercise ignored.</summary>
/// <remarks>
/// <para>
/// The paths run on the clock spanning every Black-Scholes process's structures, as
/// <see cref="MCEuropeanBasketEngine"/>'s do: the grid holds the fixing times on it, each payment discounted on the
/// first process's risk-free curve at the instant its time denotes, nothing accrued before the curve's own date, and
/// each fixing's curve the risk-free curve implied from its date; a market on one clock keeps QuantLib's draws and
/// arithmetic. The seed is explicit and required.
/// </para>
/// <para>
/// The fixing dates are refused unless strictly increasing ("Invalid dates/times", as QuantLib's American path engine
/// refuses them), where QuantLib's basket engine pairs the grid's sorted, distinct fixing times with the dates in their
/// given order, so a repeated or unsorted date reads the wrong date's curve.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class MCPathBasketEngine
{
    /// <summary>Prices a path option, its early exercise ignored.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics.</typeparam>
    /// <param name="option">The path option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' correlated processes, the first a Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/>.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether to build the paths by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether to use antithetic paths.</param>
    /// <param name="controlVariate">Whether to use a control variate, which this engine doesn't provide.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The largest number of samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The value and, where the traits allow one, its error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are neither or both given, or not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The seed or a sample count is out of range.</exception>
    /// <exception cref="NanoQLException">
    /// The option has no payoff or no fixing date, the fixing dates aren't strictly increasing, the first process isn't
    /// a Black-Scholes process, neither samples nor a tolerance is given, or the simulation fails.
    /// </exception>
    public static InstrumentResults Calculate<TRng, TStats>(
        PathMultiAssetOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        bool controlVariate,
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
            controlVariate,
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

    // QuantLib's grid: the fixing times on the clock, refused unless strictly increasing, and the given steps
    internal static TimeGrid FixingGrid(
        StochasticProcess processOnClock, ImmutableArray<Date> fixings, int? timeSteps, int? timeStepsPerYear)
    {
        var fixingTimes = new double[fixings.Length];
        for (int i = 0; i < fixings.Length; ++i)
        {
            fixingTimes[i] = processOnClock.Time(fixings[i]);
            if (i > 0 && !(fixingTimes[i] > fixingTimes[i - 1]))
            {
                throw new NanoQLException("Invalid dates/times");
            }
        }

        int numberOfTimeSteps = timeSteps ?? (int)(timeStepsPerYear!.Value * fixingTimes[^1]);
        return new TimeGrid(fixingTimes, numberOfTimeSteps);
    }

    // Each fixing's grid index, its discount on the risk-free curve at its instant on the clock, and the risk-free curve
    // implied from its date
    internal static (int[] Positions, Vector Discounts, ImmutableArray<YieldTermStructure> Curves) FixingData(
        TimeGrid grid, ImmutableArray<Date> fixings, YieldTermStructure riskFreeRate, TermStructure clock)
    {
        ImmutableArray<double> times = grid.MandatoryTimes;
        int numberOfTimes = times.Length;
        if (!(fixings.Length == numberOfTimes))
        {
            throw new NanoQLException("Invalid dates/times");
        }

        var timePositions = new int[numberOfTimes];
        var discountFactors = new Vector(numberOfTimes);
        var forwardTermStructures = new YieldTermStructure[numberOfTimes];
        for (int i = 0; i < numberOfTimes; ++i)
        {
            timePositions[i] = grid.Index(times[i]);
            discountFactors[i] = riskFreeRate.DiscountAt(clock, times[i], true);
            forwardTermStructures[i] = new ImpliedTermStructure(riskFreeRate, fixings[i]);
        }

        return (timePositions, discountFactors, [.. forwardTermStructures]);
    }

    // QuantLib's engine object over one call's option, its paths on the processes' clock.
    private sealed class Simulation<TRng, TStats> : McSimulation<MultiPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly PathMultiAssetOption option;
        private readonly StochasticProcessArray processes;
        private readonly StochasticProcessArray processesOnClock;
        private readonly TermStructure? clock;
        private readonly int? timeSteps;
        private readonly int? timeStepsPerYear;
        private readonly bool brownianBridge;
        private readonly int? requiredSamples;
        private readonly double? requiredTolerance;
        private readonly int? maxSamples;
        private readonly long seed;

        public Simulation(
            PathMultiAssetOption option,
            StochasticProcessArray processes,
            int? timeSteps,
            int? timeStepsPerYear,
            bool brownianBridge,
            bool antitheticVariate,
            bool controlVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed)
            : base(antitheticVariate, controlVariate)
        {
            MCVanillaEngine<MultiVariate<TRng>, MultiPath, TStats>.CheckSteps(timeSteps, timeStepsPerYear);
            MCVanillaEngine<MultiVariate<TRng>, MultiPath, TStats>.CheckSamples(requiredSamples, maxSamples);
            Seeds.Check(seed);
            this.option = option;
            this.processes = processes;
            clock = MCEuropeanBasketEngine.Clock(processes);
            processesOnClock = clock is null ? processes : processes.OnClock(clock);
            this.timeSteps = timeSteps;
            this.timeStepsPerYear = timeStepsPerYear;
            this.brownianBridge = brownianBridge;
            this.requiredSamples = requiredSamples;
            this.requiredTolerance = requiredTolerance;
            this.maxSamples = maxSamples;
            this.seed = seed;
        }

        // QuantLib's calculate().
        public InstrumentResults Price()
        {
            MonteCarloModel<MultiPath, TStats> model = Calculate(requiredTolerance, requiredSamples, maxSamples ?? int.MaxValue);
            return new InstrumentResults
            {
                NPV = model.SampleAccumulator.Mean(),
                ErrorEstimate = TRng.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
            };
        }

        protected override TimeGrid TimeGrid() =>
            FixingGrid(processesOnClock, option.FixingDates, timeSteps, timeStepsPerYear);

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

            (int[] positions, Vector discounts, ImmutableArray<YieldTermStructure> curves) =
                FixingData(TimeGrid(), option.FixingDates, process.RiskFreeRate, clock!);
            return new EuropeanPathMultiPathPricer(option.PathPayoff, positions, curves, discounts);
        }
    }
}
