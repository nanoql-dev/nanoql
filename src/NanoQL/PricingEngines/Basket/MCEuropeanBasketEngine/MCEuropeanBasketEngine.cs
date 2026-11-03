// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/mceuropeanbasketengine.hpp ql/pricingengines/basket/mceuropeanbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The Monte Carlo engine for European basket options on correlated processes.</summary>
/// <remarks>
/// <para>
/// The paths run on the clock spanning every Black-Scholes process's structures, from their earliest reference date on
/// the first process's risk-free day counter, each process read at the instants those times denote and accruing
/// nothing before its own date, where QuantLib evolves every process on the first's times
/// (<c>Divergences/McBasketMismatchedTimesTests.cs</c>); a market on one clock keeps QuantLib's draws and arithmetic.
/// The payoff is discounted on the first process's risk-free curve to the exercise date.
/// </para>
/// <para>
/// The draw-price-add loop allocates nothing per sample after its first batch: the paths step through
/// <see cref="StochasticProcessArray"/>'s in-place pair and the path pricer passes its buffer
/// (<c>Conventions/McBasketAllocationTests.cs</c>). The seed is explicit and required (the playbook's
/// <c>SeedGenerator</c> row). The correctness of the returned value is tested by reproducing results available in
/// literature.
/// </para>
/// </remarks>
public static class MCEuropeanBasketEngine
{
    /// <summary>Prices a European basket option.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics.</typeparam>
    /// <param name="option">The basket option.</param>
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
    /// <returns>The value and, where the traits allow one, its error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are neither or both given, or not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The seed or a sample count is out of range.</exception>
    /// <exception cref="NanoQLException">
    /// The first process isn't a Black-Scholes process, neither samples nor a tolerance is given, or the simulation
    /// fails.
    /// </exception>
    public static MultiAssetOptionResults Calculate<TRng, TStats>(
        BasketOption option,
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

    // The clock spanning the array's Black-Scholes processes, where the first is one.
    internal static TermStructure? Clock(StochasticProcessArray processes)
    {
        if (processes.Process(0) is not GeneralizedBlackScholesProcess)
        {
            return null;
        }

        var bs = new List<GeneralizedBlackScholesProcess>(processes.Size);
        for (int i = 0; i < processes.Size; ++i)
        {
            if (processes.Process(i) is GeneralizedBlackScholesProcess p)
            {
                bs.Add(p);
            }
        }

        return Methods.FdmBlackScholesOp.ClockAcross(bs);
    }

    // QuantLib's engine object over one call's option, its paths on the processes' clock.
    private sealed class Simulation<TRng, TStats> : McSimulation<MultiPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly BasketOption option;
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
            BasketOption option,
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
            TermStructure? clock = Clock(processes);
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
        public MultiAssetOptionResults Price()
        {
            MonteCarloModel<MultiPath, TStats> model = Calculate(requiredTolerance, requiredSamples, maxSamples ?? int.MaxValue);
            return new MultiAssetOptionResults
            {
                NPV = model.SampleAccumulator.Mean(),
                ErrorEstimate = TRng.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
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

        protected override PathPricer<MultiPath> PathPricer()
        {
            if (option.Payoff is not BasketPayoff payoff)
            {
                throw new NanoQLException("non-basket payoff given");
            }

            if (processes.Process(0) is not GeneralizedBlackScholesProcess process)
            {
                throw new NanoQLException("Black-Scholes process required");
            }

            // by date on the first process's risk-free curve, nothing accrued before its reference date
            Date lastDate = option.Exercise.LastDate();
            return new EuropeanMultiPathPricer(payoff, process.RiskFreeRate.AccruedDiscount(lastDate));
        }
    }
}
