// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/mcdoublebarrierengine.hpp ql/experimental/barrieroption/mcdoublebarrierengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;

namespace NanoQL.Experimental;

/// <summary>
/// The Monte Carlo pricing engine for double barrier options, the barriers checked at the path's points
/// (<see cref="DoubleBarrierPathPricer"/>).
/// </summary>
/// <remarks>
/// <para>
/// A static class over a private simulation deriving <see cref="McSimulation{TPath, TStats}"/> directly, as QuantLib's
/// engine derives <c>McSimulation</c> (the S-xeq plan's §2.0, S-bar's BR1 form): <c>Calculate</c> builds the
/// simulation with QuantLib's checks and returns the NPV and, where the random-number traits allow one, the error
/// estimate. The seed is explicit (S2R4).
/// </para>
/// <para>
/// Documented divergences. The paths and the discounts run on the engine's clock, <see cref="FdmBlackScholesOp.Clock"/>,
/// from the earliest reference date of the structures, where QuantLib's run on the risk-free curve's (XER5 (xxxiii),
/// S-bar's BR5 (iii); <c>Divergences/McDoubleBarrierTests.cs</c>). And a non-European exercise is refused, as S-bar's
/// <see cref="MCBarrierEngine"/> refuses it, where QuantLib prices any exercise to its last date.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class MCDoubleBarrierEngine
{
    /// <summary>Prices a double barrier option by Monte Carlo.</summary>
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
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are given neither or both ways, or aren't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its
    /// range.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the spot isn't positive or lies on or beyond a barrier ("barrier touched"), the
    /// payoff isn't plain vanilla, the type is a KIKO or a KOKI, neither a tolerance nor a number of samples is given, or
    /// the samples can't reach the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        DoubleBarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
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
            seed);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(engine.Price());
    }

    // QuantLib's engine object over one call's option, on the process's clock.
    private sealed class Simulation<TRng, TStats> : McSimulation<MonteCarloPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly DoubleBarrierOption option;
        private readonly GeneralizedBlackScholesProcess process;
        private readonly StochasticProcess1D clockedProcess;
        private readonly TermStructure clock;
        private readonly int? timeSteps;
        private readonly int? timeStepsPerYear;
        private readonly bool brownianBridge;
        private readonly int? requiredSamples;
        private readonly double? requiredTolerance;
        private readonly int? maxSamples;
        private readonly long seed;

        public Simulation(
            DoubleBarrierOption option,
            GeneralizedBlackScholesProcess process,
            TermStructure clock,
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
            this.seed = seed;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Price()
        {
            // QuantLib never reads the exercise's type and prices any exercise to its last date: the paths price a
            // European exercise only, as S-bar's barrier engine refuses the others
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

            return new DoubleBarrierPathPricer(
                option.BarrierType,
                option.BarrierLo,
                option.BarrierHi,
                option.Rebate,
                payoff.OptionType,
                payoff.Strike,
                discounts);
        }
    }
}
