// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/cliquet/mcperformanceengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The performance option pricing engine by Monte Carlo simulation.</summary>
/// <remarks>
/// <para>
/// A static class over a private simulation on S-sr's <see cref="McSimulation{TPath, TStats}"/>: <c>Calculate</c>
/// returns the samples' mean as the NPV and, where the random-number traits allow one, their error estimate. The grid's
/// times are the resets and expiry on the process's clock (<see cref="FdmBlackScholesOp.Clock"/>), the discounts read
/// there (the S-exo plan's ER5 (xxiv)); on structures dated alike, QuantLib's. The path pricer reads each reset at its
/// index on the grid, so a reset today prices (ER5 (xxxix), <c>Divergences/McPerformanceResetTodayTests.cs</c>). The
/// seed is explicit (S2R4).
/// </para>
/// </remarks>
public static class MCPerformanceEngine
{
    /// <summary>Prices a performance option by Monte Carlo.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its range.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, neither a tolerance nor a number of samples is given, or the samples can't reach
    /// the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        CliquetOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
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
        MCVanillaEngine<SingleVariate<TRng>, MonteCarloPath, TStats>.CheckSamples(requiredSamples, maxSamples);
        Seeds.Check(seed);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        var simulation = new Simulation<TRng, TStats>(option, process, brownianBridge, antitheticVariate, seed);
        return option.FetchResults(simulation.Run(requiredTolerance, requiredSamples, maxSamples ?? int.MaxValue));
    }

    // QuantLib's engine object over one call's option, on the process's clock.
    private sealed class Simulation<TRng, TStats> : McSimulation<MonteCarloPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly CliquetOption option;
        private readonly GeneralizedBlackScholesProcess process;
        private readonly TermStructure clock;
        private readonly bool brownianBridge;
        private readonly long seed;

        public Simulation(
            CliquetOption option, GeneralizedBlackScholesProcess process, bool brownianBridge, bool antitheticVariate, long seed)
            : base(antitheticVariate, false)
        {
            this.option = option;
            this.process = process;
            clock = FdmBlackScholesOp.Clock(process);
            this.brownianBridge = brownianBridge;
            this.seed = seed;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Run(double? requiredTolerance, int? requiredSamples, int maxSamples)
        {
            MonteCarloModel<MonteCarloPath, TStats> mcModel = Calculate(requiredTolerance, requiredSamples, maxSamples);
            return new OneAssetOptionResults
            {
                NPV = mcModel.SampleAccumulator.Mean(),
                ErrorEstimate = TRng.AllowsErrorEstimate ? mcModel.SampleAccumulator.ErrorEstimate() : null,
            };
        }

        protected override NanoQL.TimeGrid TimeGrid() => new(FixingTimes());

        protected override IPathGenerator<MonteCarloPath> PathGenerator()
        {
            NanoQL.TimeGrid grid = TimeGrid();
            IRsg gen = SingleVariate<TRng>.MakeSequenceGenerator(grid.Size - 1, seed);
            return SingleVariate<TRng>.MakePathGenerator(process.OnClock(clock), grid, gen, brownianBridge);
        }

        protected override PathPricer<MonteCarloPath> PathPricer()
        {
            var payoff = (PercentageStrikePayoff)option.Payoff!;
            double[] times = FixingTimes();
            double[] discounts = [.. times.Select(t => process.RiskFreeRate.DiscountAt(clock, t))];
            NanoQL.TimeGrid grid = TimeGrid();
            return new PerformanceOptionPathPricer(
                payoff.OptionType, payoff.Strike, discounts, times.Select(grid.ClosestIndex));
        }

        // QuantLib's fixing times: each reset's and expiry's, on the clock.
        private double[] FixingTimes()
        {
            Date[] dates = [.. option.ResetDates, option.Exercise.LastDate()];
            return [.. dates.Select(clock.TimeFromReference)];
        }
    }
}
