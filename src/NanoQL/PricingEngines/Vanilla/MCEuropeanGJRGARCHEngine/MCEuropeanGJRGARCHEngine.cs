// SPDX-FileCopyrightText: Copyright (C) 2008 Yee Man Chan
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mceuropeangjrgarchengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>The Monte Carlo GJR-GARCH engine for European options.</summary>
/// <remarks>
/// <para>
/// A static engine over a private simulation deriving <see cref="MCVanillaEngine{TMc, TPath, TStats}"/>, QuantLib's
/// <c>MCVanillaEngine&lt;MultiVariate, RNG, S&gt;</c>; the seed is explicit (<see
/// cref="MakeMCEuropeanGJRGARCHEngine{TRng, TStats}"/>). The path generator steps the process in place.
/// </para>
/// <para>
/// The path grid and the discount run on the process's model clock, from the earliest curve date, each curve read at
/// the instant a time denotes: QuantLib's grid and reads where the dividend curve isn't dated before the risk-free
/// curve.
/// </para>
/// </remarks>
public static class MCEuropeanGJRGARCHEngine
{
    /// <summary>Prices a European plain-vanilla option.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The GJR-GARCH process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> where they're given per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/> where a tolerance is given.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The most samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>The option's results: its value and error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The payoff isn't plain vanilla, or QuantLib's Monte Carlo checks fail.</exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        VanillaOption option,
        EvaluationContext context,
        GJRGARCHProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
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
        TermStructure clock = process.ModelClock();
        var engine = new Simulation<TRng, TStats>(
            option,
            context,
            process.OnClock(clock),
            process.RiskFreeRate,
            clock,
            timeSteps,
            timeStepsPerYear,
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

    // QuantLib's engine object over one call's option, on the process's clocked view.
    private sealed class Simulation<TRng, TStats> : MCVanillaEngine<MultiVariate<TRng>, MultiPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly YieldTermStructure riskFreeRate;
        private readonly TermStructure clock;

        public Simulation(
            VanillaOption option,
            EvaluationContext context,
            GJRGARCHProcess processOnClock,
            YieldTermStructure riskFreeRate,
            TermStructure clock,
            int? timeSteps,
            int? timeStepsPerYear,
            bool antitheticVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed)
            : base(option, context, processOnClock, timeSteps, timeStepsPerYear, false, antitheticVariate, false,
                requiredSamples, requiredTolerance, maxSamples, seed)
        {
            this.riskFreeRate = riskFreeRate;
            this.clock = clock;
        }

        // QuantLib's calculate().
        public OneAssetOptionResults Price() => Run().Results;

        protected override PathPricer<MultiPath> PathPricer()
        {
            if (Option.Payoff is not PlainVanillaPayoff payoff)
            {
                throw new NanoQLException("non-plain payoff given");
            }

            return new EuropeanGJRGARCHPathPricer(
                payoff.OptionType, payoff.Strike, riskFreeRate.DiscountAt(clock, TimeGrid().Back()));
        }
    }
}
