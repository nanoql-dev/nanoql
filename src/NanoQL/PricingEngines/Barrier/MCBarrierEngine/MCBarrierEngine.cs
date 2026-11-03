// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/mcbarrierengine.hpp ql/pricingengines/barrier/mcbarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo pricing engine for barrier options, with the Brownian bridge correction for the barrier's crossings
/// (<see cref="BarrierPathPricer"/>) or without it (<see cref="BiasedBarrierPathPricer"/>).
/// </summary>
/// <remarks>
/// <para>
/// A static class over a private simulation deriving <see cref="McSimulation{TPath, TStats}"/> directly, as QuantLib's
/// engine derives <c>McSimulation</c>: <c>Calculate</c> builds the simulation with QuantLib's checks and returns the
/// NPV and, where the random-number traits allow one, the error estimate. The seed is explicit. QuantLib's references:
/// Beaglehole, Dybvig and Zhou (1997), El Babsiri and Noel (1998).
/// </para>
/// <para>
/// Documented divergences. The paths and the discounts run on the engine's clock,
/// <see cref="FdmBlackScholesOp.Clock"/>, from the earliest reference date of the structures, where QuantLib's run on
/// the risk-free curve's (<c>Divergences/McBarrierMismatchedTimesTests.cs</c>). And the bridge's uniform numbers come
/// from QuantLib's constant seed 5, but 6 where the pricing seed is 5 on pseudo-random traits, whose paths then draw
/// from the same Mersenne twister stream as the bridge, so that each step's increment and its extreme share their
/// uniform numbers, +31.9 standard errors at ten steps (<c>Divergences/McBarrierSeed5Tests.cs</c>); low-discrepancy
/// paths share nothing with it and keep QuantLib's 5. And a non-European exercise is refused, as the family's European
/// engines refuse it, where QuantLib prices it to its last date: an American down-and-out put at 80 prices 1.6705, its
/// European value, where the binomial engine prices 6.3644 (<c>Divergences/McBarrierExerciseTests.cs</c>).
/// </para>
/// </remarks>
public static class MCBarrierEngine
{
    // QuantLib's constant seed of the bridge's uniform numbers (mcbarrierengine.hpp:253-254), and the one taken at the
    // pricing seed 5 on pseudo-random traits
    private const long BridgeSeed = 5;
    private const long BridgeSeedAt5 = 6;

    /// <summary>Prices a barrier option by Monte Carlo.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/> for a number of
    /// steps.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="isBiased">Whether the barrier is checked at the path's points only.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are given neither or both ways, or aren't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its
    /// range.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the spot isn't positive or lies beyond the barrier ("barrier touched"), the payoff
    /// isn't plain vanilla, neither a tolerance nor a number of samples is given, or the samples can't reach the
    /// tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        BarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        bool isBiased,
        long seed)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        var engine = new Simulation<TRng, TStats>(
            option,
            process,
            FdmBlackScholesOp.Clock(process),
            timeSteps,
            timeStepsPerYear,
            brownianBridge,
            antitheticVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            isBiased,
            seed);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(engine.Price());
    }

    // Whether the traits draw their paths' uniform numbers from a Mersenne twister seeded with the seed, as the
    // bridge's constant stream is
    private static bool DrawsFromMersenneTwister<TRng>()
        where TRng : IRngTraits
    {
        Type t = typeof(TRng);
        return t == typeof(PseudoRandom)
            || t == typeof(PoissonPseudoRandom)
            || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(GenericPseudoRandom<,>)
                && t.GetGenericArguments()[0] == typeof(MersenneTwisterUniformRng));
    }

    // QuantLib's engine object over one call's option, on the process's clock.
    private sealed class Simulation<TRng, TStats> : McSimulation<MonteCarloPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly BarrierOption option;
        private readonly GeneralizedBlackScholesProcess process;
        private readonly StochasticProcess1D clockedProcess;
        private readonly TermStructure clock;
        private readonly int? timeSteps;
        private readonly int? timeStepsPerYear;
        private readonly bool brownianBridge;
        private readonly int? requiredSamples;
        private readonly double? requiredTolerance;
        private readonly int? maxSamples;
        private readonly bool isBiased;
        private readonly long seed;

        public Simulation(
            BarrierOption option,
            GeneralizedBlackScholesProcess process,
            TermStructure clock,
            int? timeSteps,
            int? timeStepsPerYear,
            bool brownianBridge,
            bool antitheticVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            bool isBiased,
            long seed)
            : base(antitheticVariate, false)
        {
            MCVanillaEngine<SingleVariate<TRng>, MonteCarloPath, TStats>.CheckSteps(timeSteps, timeStepsPerYear);
            MCVanillaEngine<SingleVariate<TRng>, MonteCarloPath, TStats>.CheckSamples(requiredSamples, maxSamples);
            Seeds.Check(seed);
            this.option = option;
            this.process = process;
            this.clock = clock;
            clockedProcess = process.OnClock(clock);
            this.timeSteps = timeSteps;
            this.timeStepsPerYear = timeStepsPerYear;
            this.brownianBridge = brownianBridge;
            this.requiredSamples = requiredSamples;
            this.requiredTolerance = requiredTolerance;
            this.maxSamples = maxSamples;
            this.isBiased = isBiased;
            this.seed = seed;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Price()
        {
            // QuantLib never reads the exercise's type and prices any exercise to its last date: the paths price a
            // European exercise only, early exercise wanting a regression
            if (option.Exercise.Type != ExerciseType.European)
            {
                throw new NanoQLException("only european style option are supported");
            }

            double spot = process.X0;
            if (!(spot > 0.0))
            {
                throw new NanoQLException("negative or null underlying given");
            }

            if (option.Triggered(spot))
            {
                throw new NanoQLException("barrier touched");
            }

            MonteCarloModel<MonteCarloPath, TStats> model = Calculate(requiredTolerance, requiredSamples, maxSamples ?? int.MaxValue);
            return new OneAssetOptionResults
            {
                NPV = model.SampleAccumulator.Mean(),
                ErrorEstimate = TRng.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
            };
        }

        protected override NanoQL.TimeGrid TimeGrid()
        {
            double residualTime = clockedProcess.Time(option.Exercise.LastDate());
            if (timeSteps is int steps)
            {
                return new NanoQL.TimeGrid(residualTime, steps);
            }
            else if (timeStepsPerYear is int perYear)
            {
                int n = (int)(perYear * residualTime);
                return new NanoQL.TimeGrid(residualTime, Math.Max(n, 1));
            }
            else
            {
                throw new NanoQLException("time steps not specified");
            }
        }

        protected override IPathGenerator<MonteCarloPath> PathGenerator()
        {
            NanoQL.TimeGrid grid = TimeGrid();
            IRsg gen = SingleVariate<TRng>.MakeSequenceGenerator(grid.Size - 1, seed);
            return SingleVariate<TRng>.MakePathGenerator(clockedProcess, grid, gen, brownianBridge);
        }

        protected override PathPricer<MonteCarloPath> PathPricer()
        {
            if (option.Payoff is not PlainVanillaPayoff payoff)
            {
                throw new NanoQLException("non-plain payoff given");
            }

            // the discounts at the grid's times, on the clock
            NanoQL.TimeGrid grid = TimeGrid();
            double[] discounts = new double[grid.Size];
            for (int i = 0; i < grid.Size; i++)
            {
                discounts[i] = process.RiskFreeRate.DiscountAt(clock, grid[i]);
            }

            // do this with template parameters?
            if (isBiased)
            {
                return new BiasedBarrierPathPricer(
                    option.BarrierType, option.Barrier, option.Rebate, payoff.OptionType, payoff.Strike, discounts);
            }

            long bridgeSeed = seed == BridgeSeed && DrawsFromMersenneTwister<TRng>() ? BridgeSeedAt5 : BridgeSeed;
            var sequenceGen = new MersenneTwisterUniformRsg(grid.Size - 1, bridgeSeed);
            return new BarrierPathPricer(
                option.BarrierType,
                option.Barrier,
                option.Rebate,
                payoff.OptionType,
                payoff.Strike,
                discounts,
                clockedProcess,
                sequenceGen);
        }
    }
}
