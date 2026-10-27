// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mc_discr_arith_av_price_heston.hpp ql/pricingengines/asian/mc_discr_arith_av_price_heston.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Monte Carlo engine for discrete arithmetic average-price Asian options on a Heston process, with the geometric
/// average as an optional control variate priced by <see cref="AnalyticDiscreteGeometricAveragePriceAsianHestonEngine"/>.
/// </summary>
/// <remarks>
/// <para>
/// The value and, where the generator allows one, its error estimate, with the grid as the additional result
/// <c>"TimeGrid"</c>, as QuantLib's (the S-bar plan's BR1 (iii)): a static class over a private simulation deriving
/// <see cref="MCDiscreteAveragingAsianEngineBase{TMc, TPath, TStats}"/> on multi-variate paths, the grid the fixing
/// times with the steps or steps per year given, the seed explicit (S2R4). The process is <see cref="HestonProcess"/>
/// or a process deriving it, QuantLib's template parameter <c>P</c>. The control is the unseasoned geometric average on
/// the fixings to come, its analytic value the engine's on the same contract, as QuantLib's.
/// </para>
/// <para>
/// Documented divergences (BR5). The paths and the discount run on the engine's clock, <see cref="FdmHestonOp.Clock"/>,
/// from the earliest reference date of the structures, where QuantLib's run on the risk-free curve's (BR1 (v),
/// <c>Divergences/AsianMismatchedTimesTests.cs</c>); a fixing date given twice counts twice in both averages ((xxviii),
/// <c>Divergences/AsianDuplicateFixingsTests.cs</c>); the control's analytic value is the corrected engine's ((xxxiv),
/// (xlix), (l)).
/// </para>
/// </remarks>
public static class MCDiscreteArithmeticAPHestonEngine
{
    /// <summary>Prices a discrete arithmetic average-price Asian option by Monte Carlo on a Heston process.</summary>
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
    /// <param name="controlVariate">Whether the geometric average is a control variate.</param>
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
        int? timeStepsPerYear = null,
        bool controlVariate = false)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        MCDiscreteGeometricAPHestonEngine.CheckSteps(timeSteps, timeStepsPerYear);
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
            timeStepsPerYear,
            controlVariate);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(engine.Price());
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
            int? timeStepsPerYear,
            bool controlVariate)
            : base(option, context, process.OnClock(clock), false, antitheticVariate, controlVariate,
                requiredSamples, requiredTolerance, maxSamples, seed, timeSteps, timeStepsPerYear)
        {
            this.process = process;
            this.clock = clock;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Price() => Run().Results;

        protected override PathPricer<MultiPath> PathPricer()
        {
            // Keep track of the fixing indices, the path pricer will need to sum only these, one per fixing
            (PlainVanillaPayoff payoff, double discount, var fixingIndexes) = Terms();
            return new ArithmeticAPOHestonPathPricer(
                payoff.OptionType, payoff.Strike, discount, fixingIndexes, RunningAccumulator, PastFixings);
        }

        protected override PathPricer<MultiPath>? ControlPathPricer()
        {
            // Keep track of the fixing indices, the path pricer will need to prod only these, one per fixing
            (PlainVanillaPayoff payoff, double discount, var fixingIndexes) = Terms();

            // TODO: Currently the analytic pricer does not support seasoned asian
            // options (coming soon). Once that is available, we will be able to
            // pass seasoning details to the path pricer (NB. NEED to pass them to
            // the analytic pricer as well in that case).
            return new GeometricAPOHestonPathPricer(payoff.OptionType, payoff.Strike, discount, fixingIndexes);
        }

        protected override Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults>? ControlPricingEngine()
        {
            HestonProcess p = process;
            return (option, context) => AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(option, context, p);
        }

        // QuantLib's checks, in its order, the discount to the exercise on the clock and the fixings' indices
        private (PlainVanillaPayoff Payoff, double Discount, ImmutableArray<int> FixingIndexes) Terms()
        {
            NanoQL.TimeGrid timeGrid = TimeGrid();
            ImmutableArray<int> fixingIndexes = FixingIndices(timeGrid);

            if (Option.Payoff is not PlainVanillaPayoff payoff)
            {
                throw new NanoQLException("non-plain payoff given");
            }

            if (Option.Exercise is not EuropeanExercise exercise)
            {
                throw new NanoQLException("wrong exercise given");
            }

            return (payoff, process.RiskFreeRate.DiscountAt(clock, Process.Time(exercise.LastDate())), fixingIndexes);
        }
    }
}
