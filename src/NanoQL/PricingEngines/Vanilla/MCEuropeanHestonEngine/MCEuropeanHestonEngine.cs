// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mceuropeanhestonengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>The Monte Carlo engine for European options under a Heston-like process.</summary>
/// <remarks>
/// <para>
/// A static class over <see cref="MCVanillaEngine{TMc, TPath, TStats}"/> on multi-variate paths (the S-hes plan's
/// HR2 (iv), the S-van plan's VD6 form): <c>Calculate</c> builds the simulation for the call and returns the samples'
/// mean as the NPV and, where the random-number traits allow one, their error estimate. The process is a
/// <see cref="HestonProcess"/>, a <see cref="BatesProcess"/> among them, whose jumps the paths then carry, QuantLib's
/// template parameter <c>P</c>; the paths step in place, allocating nothing per sample after the first (HR2 (i),
/// (iii)). The payoff must be plain vanilla.
/// </para>
/// <para>
/// QuantLib's grid runs from the risk-free curve's reference date (<c>process-&gt;time</c>) and discounts at the
/// grid's last time on it (<c>mceuropeanhestonengine.hpp:121-129</c>). Here the paths run on the model's clock, from
/// the earliest reference date of the process's curves on the risk-free curve's day counter, each curve read at the
/// instants the grid's times denote and the payoff discounted there (HR7 (vi), the S-van plan's VR1 (v),
/// <c>Divergences/McHestonMismatchedTimesTests.cs</c>); where the dividend curve isn't dated before the risk-free curve,
/// the bits are QuantLib's. The seed is explicit (S2R4): QuantLib's default, 0, a seed from the clock, is refused.
/// </para>
/// </remarks>
public static class MCEuropeanHestonEngine
{
    /// <summary>Prices a European vanilla option by Monte Carlo under a Heston-like process.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Heston process, or a Bates process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/> for a number of steps.</param>
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
    /// The payoff isn't plain vanilla, neither a tolerance nor a number of samples is given, the samples can't reach the
    /// tolerance, or a step refuses.
    /// </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        VanillaOption option,
        EvaluationContext context,
        HestonProcess process,
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
        return Price<TRng, TStats>(
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
    }

    // The engine on one process type's clocked view and risk-free curve: QuantLib's template over P.
    private static OneAssetOptionResults Price<TRng, TStats>(
        VanillaOption option,
        EvaluationContext context,
        StochasticProcess processOnClock,
        YieldTermStructure riskFreeRate,
        TermStructure clock,
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
        var engine = new Simulation<TRng, TStats>(
            option,
            context,
            processOnClock,
            riskFreeRate,
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
            StochasticProcess processOnClock,
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

            return new EuropeanHestonPathPricer(
                payoff.OptionType, payoff.Strike, riskFreeRate.DiscountAt(clock, TimeGrid().Back()));
        }
    }
}
