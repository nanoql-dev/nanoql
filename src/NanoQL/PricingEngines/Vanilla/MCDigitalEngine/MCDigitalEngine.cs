// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mcdigitalengine.hpp ql/pricingengines/vanilla/mcdigitalengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The pricing engine for American cash-or-nothing digital options by Monte Carlo simulation, with the Brownian bridge
/// correction for the strike's crossings (<see cref="DigitalPathPricer"/>).
/// </summary>
/// <remarks>
/// <para>
/// A static class over <see cref="MCVanillaEngine{TMc, TPath, TStats}"/>: <c>Calculate</c> builds the simulation for
/// the call, with QuantLib's checks, and returns the NPV and, where the random-number traits allow one, the error
/// estimate. The payoff must be cash-or-nothing and the exercise American. The bridge's uniform numbers come from
/// QuantLib's constant seed 76.
/// </para>
/// <para>
/// Documented divergences beside the pricer's: the paths and the discounts run on the engine's clock (as
/// <see cref="MCEuropeanEngine"/>); an exercise window opening after the start joins the grid as a mandatory time,
/// crossings counted from it; and at seed 76 the bridge's seed is 77, since QuantLib's bridge then draws the uniform
/// numbers of the pseudo-random path's own increments, step by step (<c>Divergences/McDigitalSeed76Tests.cs</c>). The
/// seed is explicit.
/// </para>
/// </remarks>
public static class MCDigitalEngine
{
    // QuantLib's constant seed of the bridge's uniform numbers (mcdigitalengine.hpp:178-179), and the one taken at the
    // pricing seed 76.
    private const long BridgeSeed = 76;
    private const long BridgeSeedAt76 = 77;

    /// <summary>Prices an American cash-or-nothing option by Monte Carlo.</summary>
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
    /// <exception cref="ArgumentException">The time steps are given neither or both ways, or aren't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its range.</exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't cash-or-nothing, the exercise isn't American, neither a tolerance nor a number of samples is
    /// given, or the samples can't reach the tolerance.
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
            FdmBlackScholesOp.Clock(process),
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
            if (Option.Payoff is not CashOrNothingPayoff payoff)
            {
                throw new NanoQLException("wrong payoff given");
            }

            if (Option.Exercise is not AmericanExercise exercise)
            {
                throw new NanoQLException("wrong exercise given");
            }

            NanoQL.TimeGrid grid = TimeGrid();
            var sequenceGen = new MersenneTwisterUniformRsg(grid.Size - 1, Seed == BridgeSeed ? BridgeSeedAt76 : BridgeSeed);

            return new DigitalPathPricer(payoff, exercise, process.RiskFreeRate, clock, (StochasticProcess1D)Process, sequenceGen);
        }

        // The window's opening joins the grid as a mandatory time where it is after the start; QuantLib's grid
        // otherwise.
        protected override NanoQL.TimeGrid TimeGrid()
        {
            double windowStart = Process.Time(Option.Exercise.Dates[0]);
            if (!(windowStart > 0.0))
            {
                return base.TimeGrid();
            }

            double t = Process.Time(Option.Exercise.LastDate());
            int steps = TimeSteps is int timeSteps ? timeSteps : Math.Max((int)(TimeStepsPerYear!.Value * t), 1);
            return new NanoQL.TimeGrid([windowStart, t], steps);
        }
    }
}
