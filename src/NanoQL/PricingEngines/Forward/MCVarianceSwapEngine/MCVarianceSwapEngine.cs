// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/mcvarianceswapengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo variance-swap engine: the variance the swap pays as the mean of the paths' realized variances, as
/// described in Demeterfi, Derman, Kamal and Zou, "A Guide to Volatility and Variance Swaps" (1999).
/// </summary>
/// <remarks>
/// <para>
/// Stateless: each call runs its own simulation. It prices the variance from the evaluation date, whatever the swap's
/// start date, as QuantLib documents ("does not manage seasoned variance swaps").
/// </para>
/// <para>
/// The path grid runs on the process's clock (<see cref="FdmBlackScholesOp.Clock"/>), the earliest reference date of
/// its structures, each read there accruing nothing before its own date, and the payment discounted by date
/// (<c>Divergences/McVarianceSwapMismatchedTimesTests.cs</c>), where QuantLib's grid runs on the risk-free curve's
/// time; on structures dated alike, QuantLib's.
/// </para>
/// </remarks>
public static class MCVarianceSwapEngine
{
    /// <summary>Prices a variance swap by Monte Carlo.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="swap">The variance swap.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/>.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The swap's results: the NPV, the variance and, where the numbers give one, the error estimate.</returns>
    /// <exception cref="ArgumentNullException">The swap, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are both given, neither, or not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its range.</exception>
    /// <exception cref="NanoQLException">
    /// Neither a tolerance nor a number of samples is given, or the samples can't reach the tolerance.
    /// </exception>
    public static VarianceSwapResults Calculate<TRng, TStats>(
        VarianceSwap swap,
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
        ArgumentNullException.ThrowIfNull(swap);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        MCVanillaEngine<SingleVariate<TRng>, MonteCarloPath, TStats>.CheckSteps(timeSteps, timeStepsPerYear);
        MCVanillaEngine<SingleVariate<TRng>, MonteCarloPath, TStats>.CheckSamples(requiredSamples, maxSamples);
        Seeds.Check(seed);
        if (swap.IsExpired(context))
        {
            return swap.SetupExpired();
        }

        swap.Validate();
        var simulation = new Simulation<TRng, TStats>(
            swap, process, timeSteps, timeStepsPerYear, brownianBridge, antitheticVariate, seed);
        return swap.FetchResults(simulation.Run(requiredTolerance, requiredSamples, maxSamples ?? int.MaxValue));
    }

    // QuantLib's engine object over one call's swap, on the process's clock.
    private sealed class Simulation<TRng, TStats> : McSimulation<MonteCarloPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly VarianceSwap swap;
        private readonly GeneralizedBlackScholesProcess process;
        private readonly TermStructure clock;
        private readonly int? timeSteps, timeStepsPerYear;
        private readonly bool brownianBridge;
        private readonly long seed;

        public Simulation(
            VarianceSwap swap,
            GeneralizedBlackScholesProcess process,
            int? timeSteps,
            int? timeStepsPerYear,
            bool brownianBridge,
            bool antitheticVariate,
            long seed)
            : base(antitheticVariate, false)
        {
            this.swap = swap;
            this.process = process;
            clock = FdmBlackScholesOp.Clock(process);
            this.timeSteps = timeSteps;
            this.timeStepsPerYear = timeStepsPerYear;
            this.brownianBridge = brownianBridge;
            this.seed = seed;
        }

        // QuantLib's calculate().
        public VarianceSwapResults Run(double? requiredTolerance, int? requiredSamples, int maxSamples)
        {
            MonteCarloModel<MonteCarloPath, TStats> mcModel = Calculate(requiredTolerance, requiredSamples, maxSamples);
            double variance = mcModel.SampleAccumulator.Mean();
            double riskFreeDiscount = process.RiskFreeRate.AccruedDiscount(swap.MaturityDate);
            double multiplier = swap.Position switch
            {
                PositionType.Long => 1.0,
                PositionType.Short => -1.0,
                _ => throw new NanoQLException("Unknown position"),
            };
            multiplier *= riskFreeDiscount * swap.Notional;
            return new VarianceSwapResults
            {
                Variance = variance,
                NPV = multiplier * (variance - swap.Strike),
                ErrorEstimate = TRng.AllowsErrorEstimate ? multiplier * mcModel.SampleAccumulator.ErrorEstimate() : null,
            };
        }

        protected override NanoQL.TimeGrid TimeGrid()
        {
            double t = clock.TimeFromReference(swap.MaturityDate);
            if (timeSteps is int steps)
            {
                return new NanoQL.TimeGrid(t, steps);
            }

            int perYear = (int)(timeStepsPerYear!.Value * t);
            return new NanoQL.TimeGrid(t, Math.Max(perYear, 1));
        }

        protected override IPathGenerator<MonteCarloPath> PathGenerator()
        {
            NanoQL.TimeGrid grid = TimeGrid();
            IRsg gen = SingleVariate<TRng>.MakeSequenceGenerator(process.Factors * (grid.Size - 1), seed);
            return SingleVariate<TRng>.MakePathGenerator(process.OnClock(clock), grid, gen, brownianBridge);
        }

        protected override PathPricer<MonteCarloPath> PathPricer() => new VariancePathPricer(process.OnClock(clock));
    }
}
