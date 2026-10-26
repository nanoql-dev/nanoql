// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/mcforwardeuropeanhestonengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>The forward-start European option pricing engine by Monte Carlo simulation on a Heston process.</summary>
/// <remarks>
/// A static class over <see cref="MCForwardVanillaEngine{TMc, TPath, TStats}"/> (the S-exo plan's ER2 (v)), pinned to
/// <see cref="HestonProcess"/>, QuantLib's default template argument. The paths run on the process's clock, as S-hes's
/// Monte Carlo Heston engine runs them, the reset read at its index on the grid and the payoff discounted at expiry
/// there (the plan's ER5 (xxiv)); on structures dated alike, QuantLib's bits. The control variate is the vanilla struck
/// at the moneyness times the spot, priced by <see cref="AnalyticHestonEngine"/> and simulated with its reset at the
/// start, QuantLib's. The payoff must be plain vanilla; the seed is explicit (S2R4).
/// </remarks>
public static class MCForwardEuropeanHestonEngine
{
    /// <summary>Prices a forward-start European option by Monte Carlo under a Heston process.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="controlVariate">Whether the analytic vanilla is used as a control variate.</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are given neither or both ways, or aren't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its range.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, its payoff isn't plain vanilla or its exercise European, neither a tolerance nor a
    /// number of samples is given, the samples can't reach the tolerance, or a step refuses.
    /// </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        ForwardVanillaOption option,
        EvaluationContext context,
        HestonProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        bool controlVariate = false)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        TermStructure clock = process.ModelClock();
        var engine = new Simulation<TRng, TStats>(
            option,
            context,
            process,
            clock,
            timeSteps,
            timeStepsPerYear,
            antitheticVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed,
            controlVariate);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate(context);
        return option.FetchResults(engine.Price());
    }

    // QuantLib's engine object over one call's option, on the process's clocked view.
    private sealed class Simulation<TRng, TStats> : MCForwardVanillaEngine<MultiVariate<TRng>, MultiPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly HestonProcess process;
        private readonly TermStructure clock;

        public Simulation(
            ForwardVanillaOption option,
            EvaluationContext context,
            HestonProcess process,
            TermStructure clock,
            int? timeSteps,
            int? timeStepsPerYear,
            bool antitheticVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed,
            bool controlVariate)
            : base(option, context, process.OnClock(clock), timeSteps, timeStepsPerYear, false, antitheticVariate,
                requiredSamples, requiredTolerance, maxSamples, seed, controlVariate)
        {
            this.process = process;
            this.clock = clock;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Price() => Run();

        protected override PathPricer<MultiPath> PathPricer()
        {
            NanoQL.TimeGrid timeGrid = TimeGrid();
            double resetTime = Process.Time(Option.ResetDate);
            int resetIndex = timeGrid.ClosestIndex(resetTime);
            return PathPricerAt(resetIndex, timeGrid);
        }

        protected override PathPricer<MultiPath>? ControlPathPricer() => PathPricerAt(0, TimeGrid());

        protected override Func<VanillaOption, EvaluationContext, OneAssetOptionResults>? ControlPricingEngine()
        {
            var hestonModel = new HestonModel(process);
            return (option, context) => AnalyticHestonEngine.Calculate(option, context, hestonModel);
        }

        // QuantLib's pathPricer() and controlPathPricer(), which differ in the reset's index alone.
        private ForwardEuropeanHestonPathPricer PathPricerAt(int resetIndex, NanoQL.TimeGrid timeGrid)
        {
            if (Option.Payoff is not PlainVanillaPayoff payoff)
            {
                throw new NanoQLException("non-plain payoff given");
            }

            if (Option.Exercise is not EuropeanExercise)
            {
                throw new NanoQLException("wrong exercise given");
            }

            return new ForwardEuropeanHestonPathPricer(
                payoff.OptionType,
                Option.Moneyness,
                resetIndex,
                process.RiskFreeRate.DiscountAt(clock, timeGrid.Back()));
        }
    }
}
