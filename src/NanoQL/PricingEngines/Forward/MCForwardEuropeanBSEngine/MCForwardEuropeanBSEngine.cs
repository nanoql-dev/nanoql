// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/mcforwardeuropeanbsengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>The forward-start European option pricing engine by Monte Carlo simulation on a Black-Scholes process.</summary>
/// <remarks>
/// A static class over <see cref="MCForwardVanillaEngine{TMc, TPath, TStats}"/> (the S-exo plan's ER2 (v)): the paths
/// run on the process's clock (<see cref="FdmBlackScholesOp.Clock"/>), the reset read at its index on the grid and the
/// payoff discounted at expiry there (the plan's ER5 (xxiv)); on structures dated alike, QuantLib's bits. The payoff
/// must be plain vanilla; the seed is explicit (S2R4).
/// </remarks>
public static class MCForwardEuropeanBSEngine
{
    /// <summary>Prices a forward-start European option by Monte Carlo.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are given neither or both ways, or aren't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its range.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, its payoff isn't plain vanilla or its exercise European, neither a tolerance nor a
    /// number of samples is given, or the samples can't reach the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        ForwardVanillaOption option,
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
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        var engine = new Simulation<TRng, TStats>(
            option,
            context,
            process,
            clock,
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

        option.Validate(context);
        return option.FetchResults(engine.Price());
    }

    // QuantLib's engine object over one call's option, on the process's clock.
    private sealed class Simulation<TRng, TStats> : MCForwardVanillaEngine<SingleVariate<TRng>, MonteCarloPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly GeneralizedBlackScholesProcess process;
        private readonly TermStructure clock;

        public Simulation(
            ForwardVanillaOption option,
            EvaluationContext context,
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
            : base(option, context, process.OnClock(clock), timeSteps, timeStepsPerYear, brownianBridge, antitheticVariate,
                requiredSamples, requiredTolerance, maxSamples, seed)
        {
            this.process = process;
            this.clock = clock;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Price() => Run();

        protected override PathPricer<MonteCarloPath> PathPricer()
        {
            NanoQL.TimeGrid timeGrid = TimeGrid();
            double resetTime = Process.Time(Option.ResetDate);
            int resetIndex = timeGrid.ClosestIndex(resetTime);
            if (Option.Payoff is not PlainVanillaPayoff payoff)
            {
                throw new NanoQLException("non-plain payoff given");
            }

            if (Option.Exercise is not EuropeanExercise)
            {
                throw new NanoQLException("wrong exercise given");
            }

            return new ForwardEuropeanBSPathPricer(
                payoff.OptionType,
                Option.Moneyness,
                resetIndex,
                process.RiskFreeRate.DiscountAt(clock, timeGrid.Back()));
        }
    }
}
