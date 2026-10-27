// SPDX-FileCopyrightText: Copyright (C) 2008 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_arith_av_strike.hpp ql/pricingengines/asian/mc_discr_arith_av_strike.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo engine for discrete arithmetic average-strike Asian options on a Black-Scholes process.
/// </summary>
/// <remarks>
/// <para>
/// The value and, where the generator allows one, its error estimate, with the grid as the additional result
/// <c>"TimeGrid"</c>, as QuantLib's (the S-bar plan's BR1 (iii)): a static class over a private simulation deriving
/// <see cref="MCDiscreteAveragingAsianEngineBase{TMc, TPath, TStats}"/>, the exercise a time of the grid where it follows
/// the last fixing, the seed explicit (S2R4).
/// </para>
/// <para>
/// Documented divergences (BR5). The paths and the discount run on the engine's clock,
/// <see cref="FdmBlackScholesOp.Clock"/>, from the earliest reference date of the structures, where QuantLib's run on
/// the risk-free curve's (BR1 (v), <c>Divergences/AsianMismatchedTimesTests.cs</c>); a fixing date given twice counts
/// twice in the average strike ((xxviii), <c>Divergences/AsianDuplicateFixingsTests.cs</c>).
/// </para>
/// </remarks>
public static class MCDiscreteArithmeticASEngine
{
    /// <summary>Prices a discrete arithmetic average-strike Asian option by Monte Carlo.</summary>
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
    /// <returns>The option's results, the NPV, the error estimate and the grid; the expired results when it has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its
    /// range.</exception>
    /// <exception cref="NanoQLException">
    /// The option's past fixings fail its checks, the payoff isn't plain vanilla, the exercise isn't European, every
    /// fixing is in the past, or the samples can't reach the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        DiscreteAveragingAsianOption option,
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
        var engine = new Simulation<TRng, TStats>(
            option,
            context,
            process,
            FdmBlackScholesOp.Clock(process),
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
    private sealed class Simulation<TRng, TStats> : MCDiscreteAveragingAsianEngineBase<SingleVariate<TRng>, MonteCarloPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly GeneralizedBlackScholesProcess process;
        private readonly TermStructure clock;

        public Simulation(
            DiscreteAveragingAsianOption option,
            EvaluationContext context,
            GeneralizedBlackScholesProcess process,
            TermStructure clock,
            bool brownianBridge,
            bool antitheticVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed)
            : base(option, context, process.OnClock(clock), brownianBridge, antitheticVariate, false,
                requiredSamples, requiredTolerance, maxSamples, seed, null, null, true)
        {
            this.process = process;
            this.clock = clock;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Price() => Run().Results;

        protected override PathPricer<MonteCarloPath> PathPricer()
        {
            if (Option.Payoff is not PlainVanillaPayoff payoff)
            {
                throw new NanoQLException("non-plain payoff given");
            }

            if (Option.Exercise is not EuropeanExercise exercise)
            {
                throw new NanoQLException("wrong exercise given");
            }

            // When the exercise date was added to the time grid (i.e., it
            // falls after the last fixing), tell the path pricer how many
            // grid points are fixings so it can exclude the exercise point
            // from the average.
            int? fixingCount = null;
            NanoQL.TimeGrid grid = TimeGrid();
            if (IncludeExerciseDate)
            {
                if (!(TimeSteps is null && TimeStepsPerYear is null))
                {
                    throw new NanoQLException("extra time steps are not supported when includeExerciseDate is enabled");
                }

                double lastFixing = Process.Time(FixingDates[^1]);
                double exerciseTime = Process.Time(exercise.LastDate());
                if (exerciseTime > lastFixing)
                {
                    // exercise date was added to the grid; path has one
                    // extra point at the end that is NOT a fixing
                    fixingCount = grid.Size - 1;
                }
            }

            return new ArithmeticASOPathPricer(
                payoff.OptionType,
                process.RiskFreeRate.DiscountAt(clock, Process.Time(exercise.LastDate())),
                RunningAccumulator,
                PastFixings,
                fixingCount,
                FixingMultiplicities(grid));
        }
    }
}
