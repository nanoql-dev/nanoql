// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mchestonhullwhiteengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>The Monte Carlo engine for European options under the hybrid Heston-Hull-White process.</summary>
/// <remarks>
/// <para>
/// A static engine over a private simulation deriving <see cref="MCVanillaEngine{TMc, TPath, TStats}"/>, QuantLib's
/// <c>MCVanillaEngine&lt;MultiVariate, RNG, S&gt;</c>; the seed is explicit (<see
/// cref="MakeMCHestonHullWhiteEngine{TRng, TStats}"/>). The control variate is QuantLib's: the same hybrid at zero
/// equity-rate correlation, its paths on the pricing seed, priced by <see cref="AnalyticHestonHullWhiteEngine"/>; with
/// it the value is floored at 0, where deep out-of-the-money options can price slightly negative.
/// </para>
/// <para>
/// The path grid runs on the process's times, its model clock.
/// </para>
/// </remarks>
public static class MCHestonHullWhiteEngine
{
    /// <summary>Prices a European option.</summary>
    /// <typeparam name="TRng">The random-number traits.</typeparam>
    /// <typeparam name="TStats">The statistics accumulator.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The hybrid process.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> where they're given per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="controlVariate">Whether the control variate is used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/> where a tolerance is given.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The most samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>The option's results: its value and error estimate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The exercise isn't European, the process's step fails, or QuantLib's Monte
    /// Carlo checks fail.</exception>
    public static OneAssetOptionResults Calculate<TRng, TStats>(
        VanillaOption option,
        EvaluationContext context,
        HybridHestonHullWhiteProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
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
            timeSteps,
            timeStepsPerYear,
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

    // QuantLib's engine object over one call's option.
    private sealed class Simulation<TRng, TStats> : MCVanillaEngine<MultiVariate<TRng>, MultiPath, TStats>
        where TRng : IRngTraits
        where TStats : IStatistics, new()
    {
        private readonly HybridHestonHullWhiteProcess process;
        private readonly bool controlVariate;

        public Simulation(
            VanillaOption option,
            EvaluationContext context,
            HybridHestonHullWhiteProcess process,
            int? timeSteps,
            int? timeStepsPerYear,
            bool antitheticVariate,
            bool controlVariate,
            int? requiredSamples,
            double? requiredTolerance,
            int? maxSamples,
            long seed)
            : base(option, context, process, timeSteps, timeStepsPerYear, false, antitheticVariate, controlVariate,
                requiredSamples, requiredTolerance, maxSamples, seed)
        {
            this.process = process;
            this.controlVariate = controlVariate;
        }

        // QuantLib's calculate(): with the control variate, the value floored at 0, deep out-of-the-money options
        // pricing slightly negative
        public OneAssetOptionResults Price()
        {
            OneAssetOptionResults results = Run().Results;
            return controlVariate ? results with { NPV = Math.Max(0.0, results.NPV!.Value) } : results;
        }

        protected override PathPricer<MultiPath> PathPricer() => HybridPathPricer();

        protected override PathPricer<MultiPath>? ControlPathPricer() => HybridPathPricer();

        protected override Func<VanillaOption, EvaluationContext, OneAssetOptionResults>? ControlPricingEngine()
        {
            HestonProcess hestonProcess = process.HestonProcess;
            HullWhiteForwardProcess hullWhiteProcess = process.HullWhiteProcess;

            var hestonModel = new HestonModel(hestonProcess);
            var hwModel = new HullWhite(hestonProcess.RiskFreeRate, hullWhiteProcess.A, hullWhiteProcess.Sigma);

            var engine = new AnalyticHestonHullWhiteEngine(hestonModel, hwModel, 144);
            return engine.Calculate;
        }

        protected override IPathGenerator<MultiPath>? ControlPathGenerator()
        {
            int dimensions = process.Factors;
            NanoQL.TimeGrid grid = TimeGrid();
            IRsg generator = MultiVariate<TRng>.MakeSequenceGenerator(dimensions * (grid.Size - 1), Seed);

            var cvProcess = new HybridHestonHullWhiteProcess(
                process.HestonProcess, process.HullWhiteProcess, 0.0, process.Discretization);

            return MultiVariate<TRng>.MakePathGenerator(cvProcess, grid, generator, false);
        }

        // the path pricer at the exercise's time, for the option and the control alike
        private HestonHullWhitePathPricer HybridPathPricer()
        {
            if (!(Option.Exercise.Type == ExerciseType.European))
            {
                throw new NanoQLException("only european exercise is supported");
            }

            double exerciseTime = process.Time(Option.Exercise.LastDate());
            return new HestonHullWhitePathPricer(exerciseTime, Option.Payoff!, process);
        }
    }
}
