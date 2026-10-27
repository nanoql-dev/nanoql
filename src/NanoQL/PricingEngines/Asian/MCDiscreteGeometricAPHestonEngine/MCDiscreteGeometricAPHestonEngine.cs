// SPDX-FileCopyrightText: Copyright (C) 2020 Jack Gillett
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_geom_av_price_heston.hpp ql/pricingengines/asian/mc_discr_geom_av_price_heston.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo engine for discrete geometric average-price Asian options on a Heston process.
/// </summary>
/// <remarks>
/// <para>
/// The value and, where the generator allows one, its error estimate, with the grid as the additional result
/// <c>"TimeGrid"</c>, as QuantLib's (the S-bar plan's BR1 (iii)): a static class over a private simulation deriving
/// <see cref="MCDiscreteAveragingAsianEngineBase{TMc, TPath, TStats}"/> on multi-variate paths, the grid the fixing
/// times with the steps or steps per year given, the seed explicit (S2R4). The process is <see cref="HestonProcess"/>
/// or a process deriving it, QuantLib's template parameter <c>P</c>.
/// </para>
/// <para>
/// Documented divergences (BR5). The paths and the discount run on the engine's clock, <see cref="FdmHestonOp.Clock"/>,
/// from the earliest reference date of the structures, where QuantLib's run on the risk-free curve's (BR1 (v),
/// <c>Divergences/AsianMismatchedTimesTests.cs</c>). The geometric product is kept in double's normal range and the past
/// fixings are read by their log where their product leaves it ((xli), <see cref="GeometricAPOHestonPathPricer"/>); a
/// fixing date given twice counts twice ((xxviii)).
/// </para>
/// </remarks>
public static class MCDiscreteGeometricAPHestonEngine
{
    /// <summary>Prices a discrete geometric average-price Asian option by Monte Carlo on a Heston process.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/>.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <returns>The option's results, the NPV, the error estimate and the grid; the expired results when it has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Both the time steps and the time steps per year are given.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples or steps is negative, or the seed is outside
    /// its range.</exception>
    /// <exception cref="NanoQLException">
    /// The option's past fixings fail its checks, the payoff isn't plain vanilla, the exercise isn't European, every
    /// fixing is in the past, or the samples can't reach the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        DiscreteAveragingAsianOption option,
        EvaluationContext context,
        HestonProcess process,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        int? timeSteps = null,
        int? timeStepsPerYear = null)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        CheckSteps(timeSteps, timeStepsPerYear);
        var engine = new Simulation<TRng, TStats>(
            option,
            context,
            process,
            FdmHestonOp.Clock(process),
            antitheticVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed,
            timeSteps,
            timeStepsPerYear);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(engine.Price());
    }

    // QuantLib's constructor check, and the counts QuantLib holds unsigned
    internal static void CheckSteps(int? timeSteps, int? timeStepsPerYear)
    {
        if (timeSteps is int steps)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(steps, nameof(timeSteps));
        }

        if (timeStepsPerYear is int perYear)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(perYear, nameof(timeStepsPerYear));
        }

        if (!(timeSteps is null || timeStepsPerYear is null))
        {
            throw new ArgumentException("both time steps and time steps per year were provided", nameof(timeStepsPerYear));
        }
    }

    // QuantLib's engine object over one call's option, on the process's clock.
    private sealed class Simulation<TRng, TStats> : MCDiscreteAveragingAsianEngineBase<MultiVariate<TRng>, MultiPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly HestonProcess process;
        private readonly TermStructure clock;

        public Simulation(
            DiscreteAveragingAsianOption option,
            EvaluationContext context,
            HestonProcess process,
            TermStructure clock,
            bool antitheticVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed,
            int? timeSteps,
            int? timeStepsPerYear)
            : base(option, context, process.OnClock(clock), false, antitheticVariate, false,
                requiredSamples, requiredTolerance, maxSamples, seed, timeSteps, timeStepsPerYear)
        {
            this.process = process;
            this.clock = clock;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Price() => Run().Results;

        protected override PathPricer<MultiPath> PathPricer()
        {
            // Keep track of the fixing indices, the path pricer will need to prod only these, one per fixing
            NanoQL.TimeGrid timeGrid = TimeGrid();
            var fixingIndexes = FixingIndices(timeGrid);

            if (Option.Payoff is not PlainVanillaPayoff payoff)
            {
                throw new NanoQLException("non-plain payoff given");
            }

            if (Option.Exercise is not EuropeanExercise exercise)
            {
                throw new NanoQLException("wrong exercise given");
            }

            // the past fixings by their log where their product leaves double's normal range (BR5 (xli))
            double product = RunningAccumulator;
            bool inRange = double.IsNormal(product) && product > 0.0;
            return new GeometricAPOHestonPathPricer(
                payoff.OptionType,
                payoff.Strike,
                process.RiskFreeRate.DiscountAt(clock, Process.Time(exercise.LastDate())),
                fixingIndexes,
                inRange ? product : 1.0,
                inRange ? double.NaN : RunningLog,
                PastFixings);
        }
    }
}
