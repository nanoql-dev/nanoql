// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_geom_av_price.hpp ql/pricingengines/asian/mc_discr_geom_av_price.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo engine for discrete geometric average-price Asian options on a Black-Scholes process.
/// </summary>
/// <remarks>
/// <para>
/// The value and, where the generator allows one, its error estimate, with the grid as the additional result
/// <c>"TimeGrid"</c>, as QuantLib's: a static class over a private simulation deriving
/// <see cref="MCDiscreteAveragingAsianEngineBase{TMc, TPath, TStats}"/>, the seed explicit.
/// </para>
/// <para>
/// Documented divergences. The paths and the discount run on the engine's clock, <see cref="FdmBlackScholesOp.Clock"/>,
/// from the earliest reference date of the structures, where QuantLib's run on the risk-free curve's
/// (<c>Divergences/AsianMismatchedTimesTests.cs</c>). The geometric product is kept in double's normal range and the
/// past fixings are read by their log where their product leaves it (<see cref="GeometricAPOPathPricer"/>); a fixing
/// date given twice counts twice.
/// </para>
/// </remarks>
public static class MCDiscreteGeometricAPEngine
{
    /// <summary>Prices a discrete geometric average-price Asian option by Monte Carlo.</summary>
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
                requiredSamples, requiredTolerance, maxSamples, seed)
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

            // the past fixings by their log where their product leaves double's normal range
            double product = RunningAccumulator;
            bool inRange = double.IsNormal(product) && product > 0.0;
            return new GeometricAPOPathPricer(
                payoff.OptionType,
                payoff.Strike,
                process.RiskFreeRate.DiscountAt(clock, Process.Time(exercise.LastDate())),
                inRange ? product : 1.0,
                inRange ? double.NaN : RunningLog,
                PastFixings,
                FixingMultiplicities(TimeGrid()));
        }
    }
}
