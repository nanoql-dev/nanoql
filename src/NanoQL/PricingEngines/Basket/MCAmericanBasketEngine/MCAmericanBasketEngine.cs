// SPDX-FileCopyrightText: Copyright (C) 2004 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/mcamericanbasketengine.hpp ql/pricingengines/basket/mcamericanbasketengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Longstaff-Schwartz Monte Carlo engine for American and Bermudan basket options on correlated processes.
/// </summary>
/// <remarks>
/// <para>
/// The method is intrinsically weak for out-of-the-money options, as QuantLib warns. The pricing and the calibration
/// draw from the same traits; the value, its error estimate where the traits allow one, and the exercise probability
/// among the additional results as "exerciseProbability", as QuantLib's. The calibration is one call's working state.
/// </para>
/// <para>
/// The paths run on the clock spanning every Black-Scholes process's structures, each process read on it, and the
/// payoffs are discounted on the first process's risk-free curve at that clock's times, where QuantLib evolves every
/// process on the first's times (<c>Divergences/TwoAssetMismatchedTimesTests.cs</c>); a market on one clock keeps
/// QuantLib's arithmetic.
/// </para>
/// </remarks>
public static class MCAmericanBasketEngine
{
    /// <summary>Prices an American or Bermudan basket option by Longstaff-Schwartz.</summary>
    /// <typeparam name="TRng">The random number traits of both simulations.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The basket option, of an early exercise.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="processes">The assets' correlated processes, the first a Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/>.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="nCalibrationSamples">The calibration samples, or <see langword="null"/> for 2,048.</param>
    /// <param name="polynomialOrder">The regression basis' highest order.</param>
    /// <param name="polynomialType">The regression basis' family.</param>
    /// <param name="seedCalibration">
    /// The calibration seed, in [1, 4294967295], or <see langword="null"/> for the Longstaff-Schwartz base's default;
    /// QuantLib's engine takes none, and its base's default is always 4294967295, the narrowed null, which passing that
    /// seed reproduces.
    /// </param>
    /// <returns>The option's results: the value, its error estimate, and the exercise probability.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The steps are given neither or both ways or aren't positive, or the basis family is another.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A number of samples is negative, or the seed is outside its range.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The first process isn't a Black-Scholes process, the exercise isn't early or pays at expiry, or the samples
    /// can't reach the tolerance.
    /// </exception>
    public static MultiAssetOptionResults Calculate<TRng, TStats>(
        BasketOption option,
        EvaluationContext context,
        StochasticProcessArray processes,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        int? nCalibrationSamples = null,
        int polynomialOrder = 2,
        LsmBasisSystemPolynomialType polynomialType = LsmBasisSystemPolynomialType.Monomial,
        long? seedCalibration = null)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(processes);
        TermStructure? clock = MCEuropeanBasketEngine.Clock(processes);
        var engine = new Simulation<TRng, TStats>(
            option,
            context,
            processes,
            clock,
            timeSteps,
            timeStepsPerYear,
            brownianBridge,
            antitheticVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed,
            nCalibrationSamples,
            polynomialOrder,
            polynomialType,
            seedCalibration);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        (MonteCarloModel<MultiPath, TStats> model, double exerciseProbability) = engine.Price();
        return option.FetchResults(new MultiAssetOptionResults
        {
            NPV = model.SampleAccumulator.Mean(),
            ErrorEstimate = TRng.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("exerciseProbability", exerciseProbability),
        });
    }

    // QuantLib's engine object over one call's option, its paths on the processes' clock.
    private sealed class Simulation<TRng, TStats>
        : MCLongstaffSchwartzEngine<BasketOption, MultiVariate<TRng>, MultiVariate<TRng>, MultiPath, Vector, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly StochasticProcessArray processes;
        private readonly TermStructure? clock;
        private readonly int polynomialOrder;
        private readonly LsmBasisSystemPolynomialType polynomialType;

        public Simulation(
            BasketOption option,
            EvaluationContext context,
            StochasticProcessArray processes,
            TermStructure? clock,
            int? timeSteps,
            int? timeStepsPerYear,
            bool brownianBridge,
            bool antitheticVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed,
            int? nCalibrationSamples,
            int polynomialOrder,
            LsmBasisSystemPolynomialType polynomialType,
            long? seedCalibration)
            : base(
                option,
                context,
                clock is null ? processes : processes.OnClock(clock),
                timeSteps,
                timeStepsPerYear,
                brownianBridge,
                antitheticVariate,
                false,
                requiredSamples,
                requiredTolerance,
                maxSamples,
                seed,
                nCalibrationSamples,
                seedCalibration: seedCalibration)
        {
            this.processes = processes;
            this.clock = clock;
            this.polynomialOrder = polynomialOrder;
            this.polynomialType = polynomialType;
        }

        // QuantLib's calculate().
        public (MonteCarloModel<MultiPath, TStats> Model, double ExerciseProbability) Price() => Run();

        protected override LongstaffSchwartzPathPricer<MultiPath, Vector> LsmPathPricer()
        {
            if (!(processes.Size > 0))
            {
                throw new NanoQLException("Stochastic process array required");
            }

            if (processes.Process(0) is not GeneralizedBlackScholesProcess process)
            {
                throw new NanoQLException("generalized Black-Scholes process required");
            }

            if (Option.Exercise is not EarlyExercise exercise)
            {
                throw new NanoQLException("wrong exercise given");
            }

            if (exercise.PayoffAtExpiry)
            {
                throw new NanoQLException("payoff at expiry not handled");
            }

            var earlyExercisePathPricer = new AmericanBasketPathPricer(
                processes.Size, Option.Payoff!, polynomialOrder, polynomialType);

            NanoQL.TimeGrid grid = TimeGrid();
            return new LongstaffSchwartzPathPricer<MultiPath, Vector>(
                grid, earlyExercisePathPricer, process.RiskFreeRate, clock!, ExerciseTimes(grid));
        }
    }
}
