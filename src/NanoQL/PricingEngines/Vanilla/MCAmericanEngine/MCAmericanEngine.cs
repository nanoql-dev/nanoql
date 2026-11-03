// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2022 Jonghee Lee
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mcamericanengine.hpp ql/pricingengines/vanilla/mcamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The American Monte Carlo engine: Longstaff and Schwartz's least-squares estimator on Black-Scholes paths, with an
/// optional control variate on the European option.
/// </summary>
/// <remarks>
/// <para>
/// A static class over <see cref="MCLongstaffSchwartzEngine{TOption, TMc, TMcCalibration, TPath, TState, TStats}"/>:
/// <c>Calculate</c> builds the simulation for the call, with QuantLib's checks, and returns the NPV, its error estimate
/// where the pricing traits allow one, and the additional result "exerciseProbability". With the control variate, on
/// QuantLib's analytic European engine and the European path pricer, the value is floored at 0, as QuantLib floors it.
/// The exercise must be American or Bermudan with the payoff at exercise. The Brownian bridge is off in both phases, as
/// QuantLib's engine sets it.
/// </para>
/// <para>
/// The paths and the discounts run on the engine's clock (as <see cref="MCEuropeanEngine"/>); the calibration seed's
/// default is QuantLib's documented one; the pricer exercises only at the exercise times; and the seeds are explicit.
/// </para>
/// </remarks>
public static class MCAmericanEngine
{
    /// <summary>Prices an American or Bermudan vanilla option by Longstaff-Schwartz Monte Carlo.</summary>
    /// <typeparam name="TRng">The pricing random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <typeparam name="TRngCalibration">The calibration random-number traits, QuantLib's default the pricing's.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/> for a number of steps.</param>
    /// <param name="antitheticVariate">Whether the pricing uses antithetic variates.</param>
    /// <param name="controlVariate">Whether the European option is a control variate.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The pricing seed, in [1, 4294967295].</param>
    /// <param name="polynomialOrder">The regression basis' highest order.</param>
    /// <param name="polynomialType">The regression basis' family.</param>
    /// <param name="nCalibrationSamples">The calibration samples, or <see langword="null"/> for 2,048.</param>
    /// <param name="antitheticVariateCalibration">The calibration's antithetic variates, or <see langword="null"/> for the pricing's.</param>
    /// <param name="seedCalibration">The calibration seed, or <see langword="null"/> for (seed + 1768237423) mod 2³².</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The time steps are given neither or both ways, or aren't positive; the basis family is insufficient; a
    /// regression's matrix isn't finite (the second-kind Chebyshev basis on a call).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A number of samples is negative, a seed is outside its range, or the default calibration seed is 0.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't early or pays at expiry, the control variate's payoff isn't striked, neither a tolerance nor a
    /// number of samples is given, or the samples can't reach the tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate<TRng, TStats, TRngCalibration>(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool antitheticVariate,
        bool controlVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        int polynomialOrder,
        LsmBasisSystemPolynomialType polynomialType,
        int? nCalibrationSamples = null,
        bool? antitheticVariateCalibration = null,
        long? seedCalibration = null)
        where TRng : IRngTraits
        where TStats : IStatistics, new()
        where TRngCalibration : IRngTraits
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        var engine = new Simulation<TRng, TStats, TRngCalibration>(
            option,
            context,
            process,
            FdmBlackScholesOp.Clock(process),
            timeSteps,
            timeStepsPerYear,
            antitheticVariate,
            controlVariate,
            requiredSamples,
            requiredTolerance,
            maxSamples,
            seed,
            polynomialOrder,
            polynomialType,
            nCalibrationSamples,
            antitheticVariateCalibration,
            seedCalibration);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        (MonteCarloModel<MonteCarloPath, TStats> model, double exerciseProbability) = engine.Price();
        double value = model.SampleAccumulator.Mean();
        if (controlVariate)
        {
            // control variate might lead to small negative
            // option values for deep OTM options
            value = Max(0.0, value);
        }

        return option.FetchResults(new OneAssetOptionResults
        {
            NPV = value,
            ErrorEstimate = TRng.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("exerciseProbability", exerciseProbability),
        });
    }

    // QuantLib's engine object over one call's option, on the process's clock.
    private sealed class Simulation<TRng, TStats, TRngCalibration>
        : MCLongstaffSchwartzEngine<VanillaOption, SingleVariate<TRng>, SingleVariate<TRngCalibration>, MonteCarloPath, double, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
        where TRngCalibration : IRngTraits
    {
        private readonly GeneralizedBlackScholesProcess process;
        private readonly TermStructure clock;
        private readonly int polynomialOrder;
        private readonly LsmBasisSystemPolynomialType polynomialType;

        public Simulation(
            VanillaOption option,
            EvaluationContext context,
            GeneralizedBlackScholesProcess process,
            TermStructure clock,
            int? timeSteps,
            int? timeStepsPerYear,
            bool antitheticVariate,
            bool controlVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed,
            int polynomialOrder,
            LsmBasisSystemPolynomialType polynomialType,
            int? nCalibrationSamples,
            bool? antitheticVariateCalibration,
            long? seedCalibration)
            : base(
                option,
                context,
                process.OnClock(clock),
                timeSteps,
                timeStepsPerYear,
                false,
                antitheticVariate,
                controlVariate,
                requiredSamples,
                requiredTolerance,
                maxSamples,
                seed,
                nCalibrationSamples,
                false,
                antitheticVariateCalibration,
                seedCalibration)
        {
            this.process = process;
            this.clock = clock;
            this.polynomialOrder = polynomialOrder;
            this.polynomialType = polynomialType;
        }

        // QuantLib's calculate(), but the floor the static engine applies.
        public (MonteCarloModel<MonteCarloPath, TStats> Model, double ExerciseProbability) Price() => Run();

        protected override LongstaffSchwartzPathPricer<MonteCarloPath, double> LsmPathPricer()
        {
            if (Option.Exercise is not EarlyExercise exercise)
            {
                throw new NanoQLException("wrong exercise given");
            }

            if (exercise.PayoffAtExpiry)
            {
                throw new NanoQLException("payoff at expiry not handled");
            }

            var earlyExercisePathPricer = new AmericanPathPricer(Option.Payoff!, polynomialOrder, polynomialType);

            NanoQL.TimeGrid grid = TimeGrid();
            return new LongstaffSchwartzPathPricer<MonteCarloPath, double>(
                grid, earlyExercisePathPricer, process.RiskFreeRate, clock, ExerciseTimes(grid));
        }

        protected override PathPricer<MonteCarloPath> ControlPathPricer()
        {
            if (Option.Payoff is not StrikedTypePayoff payoff)
            {
                throw new NanoQLException("StrikedTypePayoff needed for control variate");
            }

            return new EuropeanPathPricer(
                payoff.OptionType, payoff.Strike, process.RiskFreeRate.DiscountAt(clock, TimeGrid().Back()));
        }

        protected override double? ControlVariateValue()
        {
            // QuantLib's analytic European engine on the option with a European exercise at its last date
            var controlOption = new VanillaOption(
                (StrikedTypePayoff)Option.Payoff!, new EuropeanExercise(Option.Exercise.LastDate()));
            return AnalyticEuropeanEngine.Calculate(controlOption, Context, process).NPV;
        }
    }

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
