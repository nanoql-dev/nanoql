// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mceuropeanengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>The European option pricing engine by Monte Carlo simulation.</summary>
/// <remarks>
/// <para>
/// A static class over <see cref="MCVanillaEngine{TMc, TPath, TStats}"/> (the S-van plan's VR1): <c>Calculate</c>
/// builds the simulation for the call, with QuantLib's checks, and returns the samples' mean as the NPV and, where the
/// random-number traits allow one, their error estimate. The type parameters, the traits and the accumulator, are
/// QuantLib's template parameters, whose defaults are <see cref="PseudoRandom"/> and <see cref="GeneralStatistics"/>
/// (QuantLib's <c>Statistics</c>). The payoff must be plain vanilla.
/// </para>
/// <para>
/// The paths run on the engine's clock, from the earliest reference date of the process's structures on the risk-free
/// curve's day counter, each structure read at the instants the grid's times denote and the payoff discounted there
/// (the S-van plan's VR1 (v), <c>Divergences/McMismatchedTimesTests.cs</c>); QuantLib's grid runs from the risk-free
/// curve's date, reading the other structures at its times. Where every structure shares the risk-free curve's clock,
/// the bits are QuantLib's. The seed is explicit (S2R4): QuantLib's default, 0, a seed from the clock, is refused.
/// </para>
/// </remarks>
public static class MCEuropeanEngine
{
    /// <summary>Prices a European vanilla option by Monte Carlo.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/> for a number of steps.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <returns>The option's results, the NPV and the error estimate; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The time steps are given neither or both ways, or aren't positive; or the strike is negative.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its range.</exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't plain vanilla, neither a tolerance nor a number of samples is given, or the samples can't reach
    /// the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        VanillaOption option,
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
            context,
            process,
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
    private sealed class Simulation<TRng, TStats> : MCVanillaEngine<SingleVariate<TRng>, MonteCarloPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly GeneralizedBlackScholesProcess process;
        private readonly TermStructure clock;

        public Simulation(
            VanillaOption option,
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
            : this(option, context, process, FdmBlackScholesOp.Clock(process), timeSteps, timeStepsPerYear, brownianBridge,
                antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed)
        {
        }

        private Simulation(
            VanillaOption option,
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
                false, requiredSamples, requiredTolerance, maxSamples, seed)
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

            return new EuropeanPathPricer(
                payoff.OptionType, payoff.Strike, process.RiskFreeRate.DiscountAt(clock, TimeGrid().Back()));
        }
    }
}
