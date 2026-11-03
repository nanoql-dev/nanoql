// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_arith_av_price.hpp ql/pricingengines/asian/mc_discr_arith_av_price.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo engine for discrete arithmetic average-price Asian options on a Black-Scholes process, with the
/// geometric average as an optional control variate priced by
/// <see cref="AnalyticDiscreteGeometricAveragePriceAsianEngine"/>.
/// </summary>
/// <remarks>
/// <para>
/// The value and, where the generator allows one, its error estimate, with the grid as the additional result
/// <c>"TimeGrid"</c>, as QuantLib's: a static class over a private simulation deriving
/// <see cref="MCDiscreteAveragingAsianEngineBase{TMc, TPath, TStats}"/>, the seed explicit.
/// </para>
/// <para>
/// Documented divergences. The paths and the discounts run on the engine's clock,
/// <see cref="FdmBlackScholesOp.Clock"/>, from the earliest reference date of the structures, where QuantLib's run on
/// the risk-free curve's (<c>Divergences/AsianMismatchedTimesTests.cs</c>). The control path is discounted to the
/// exercise, as its analytic value is, where QuantLib's discounts it to the last fixing and biases the controlled price
/// where the exercise follows (<c>Divergences/McAsianControlVariateTests.cs</c>). A fixing date given twice counts
/// twice in both averages, as in the analytic engines (<c>Divergences/AsianDuplicateFixingsTests.cs</c>). The control's
/// geometric product is kept in double's normal range (<see cref="GeometricAPOPathPricer"/>).
/// </para>
/// </remarks>
public static class MCDiscreteArithmeticAPEngine
{
    /// <summary>Prices a discrete arithmetic average-price Asian option by Monte Carlo.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="controlVariate">Whether the geometric average is a control variate.</param>
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
        ArgumentNullException.ThrowIfNull(process);
        var engine = new Simulation<TRng, TStats>(
            option,
            context,
            process,
            FdmBlackScholesOp.Clock(process),
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
            bool controlVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed)
            : base(option, context, process.OnClock(clock), brownianBridge, antitheticVariate, controlVariate,
                requiredSamples, requiredTolerance, maxSamples, seed)
        {
            this.process = process;
            this.clock = clock;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Price() => Run().Results;

        protected override PathPricer<MonteCarloPath> PathPricer()
        {
            (PlainVanillaPayoff payoff, double discount) = Terms();
            return new ArithmeticAPOPathPricer(
                payoff.OptionType, payoff.Strike, discount, RunningAccumulator, PastFixings, FixingMultiplicities(TimeGrid()));
        }

        // the control discounted to the exercise, as its analytic value is, where QuantLib discounts it to the grid's
        // end, the last fixing
        protected override PathPricer<MonteCarloPath>? ControlPathPricer()
        {
            (PlainVanillaPayoff payoff, double discount) = Terms();

            // for seasoned option the geometric strike might be rescaled
            // to obtain an equivalent arithmetic strike.
            // Any change applied here MUST be applied to the analytic engine too
            return new GeometricAPOPathPricer(
                payoff.OptionType, payoff.Strike, discount, 1.0, double.NaN, 0, FixingMultiplicities(TimeGrid()));
        }

        protected override Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults>? ControlPricingEngine()
        {
            GeneralizedBlackScholesProcess p = process;
            return (option, context) => AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(option, context, p);
        }

        // QuantLib's checks, in its order, and the discount to the exercise on the clock
        private (PlainVanillaPayoff Payoff, double Discount) Terms()
        {
            if (Option.Payoff is not PlainVanillaPayoff payoff)
            {
                throw new NanoQLException("non-plain payoff given");
            }

            if (Option.Exercise is not EuropeanExercise exercise)
            {
                throw new NanoQLException("wrong exercise given");
            }

            return (payoff, process.RiskFreeRate.DiscountAt(clock, Process.Time(exercise.LastDate())));
        }
    }
}
