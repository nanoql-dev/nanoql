// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/mcforwardvanillaengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>The Monte Carlo engine base for forward-start vanilla options.</summary>
/// <typeparam name="TMc">The Monte Carlo traits: single- or multi-variate over the random-number traits.</typeparam>
/// <typeparam name="TPath">The path type the traits generate.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// An extension point, as QuantLib's template: an immutable base over <see cref="McSimulation{TPath, TStats}"/> holding
/// one call's option and settings; a derived engine gives the path pricer and, for a control variate, its path pricer
/// and engine. The grid runs to the reset and to expiry on the process's times, as the derived engine clocks it; the
/// seed is explicit.
/// </remarks>
public abstract class MCForwardVanillaEngine<TMc, TPath, TStats> : McSimulation<TPath, TStats>
    where TMc : IMcTraits<TPath>
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MCForwardVanillaEngine{TMc, TPath, TStats}"/> class.</summary>
    /// <param name="option">The option, QuantLib's engine arguments.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The process the paths follow.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="controlVariate">Whether a control variate is used.</param>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are given neither or both ways, or aren't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of samples is negative, or the seed is outside its range.</exception>
    protected MCForwardVanillaEngine(
        ForwardVanillaOption option,
        EvaluationContext context,
        StochasticProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        bool controlVariate = false)
        : base(antitheticVariate, controlVariate)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        MCVanillaEngine<TMc, TPath, TStats>.CheckSteps(timeSteps, timeStepsPerYear);
        MCVanillaEngine<TMc, TPath, TStats>.CheckSamples(requiredSamples, maxSamples);
        Seeds.Check(seed);
        Option = option;
        Context = context;
        Process = process;
        TimeSteps = timeSteps;
        TimeStepsPerYear = timeStepsPerYear;
        BrownianBridge = brownianBridge;
        RequiredSamples = requiredSamples;
        RequiredTolerance = requiredTolerance;
        MaxSamples = maxSamples;
        Seed = seed;
    }

    /// <summary>Gets the option, QuantLib's engine arguments.</summary>
    protected ForwardVanillaOption Option { get; }

    /// <summary>Gets the evaluation context.</summary>
    protected EvaluationContext Context { get; }

    /// <summary>Gets the process the paths follow.</summary>
    protected StochasticProcess Process { get; }

    /// <summary>Gets the number of time steps, or <see langword="null"/>.</summary>
    protected int? TimeSteps { get; }

    /// <summary>Gets the number of time steps per year, or <see langword="null"/>.</summary>
    protected int? TimeStepsPerYear { get; }

    /// <summary>Gets a value indicating whether the draws are ordered by a Brownian bridge.</summary>
    protected bool BrownianBridge { get; }

    /// <summary>Gets the number of samples, or <see langword="null"/>.</summary>
    protected int? RequiredSamples { get; }

    /// <summary>Gets the absolute tolerance, or <see langword="null"/>.</summary>
    protected double? RequiredTolerance { get; }

    /// <summary>Gets the most samples for a tolerance, or <see langword="null"/> for no limit.</summary>
    protected int? MaxSamples { get; }

    /// <summary>Gets the seed.</summary>
    protected long Seed { get; }

    /// <summary>
    /// QuantLib's <c>calculate</c>: runs the simulation and returns QuantLib's results, the mean as the value and, where
    /// the traits allow one, the error estimate.
    /// </summary>
    /// <returns>The call's results.</returns>
    /// <exception cref="NanoQLException">The simulation refuses, or the samples can't reach the tolerance.</exception>
    protected OneAssetOptionResults Run()
    {
        MonteCarloModel<TPath, TStats> model = Calculate(RequiredTolerance, RequiredSamples, MaxSamples ?? int.MaxValue);
        return new OneAssetOptionResults
        {
            NPV = model.SampleAccumulator.Mean(),
            ErrorEstimate = TMc.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
        };
    }

    /// <summary>The time grid: through the reset to expiry, in the steps given or the steps per year to expiry.</summary>
    /// <returns>The grid.</returns>
    protected override NanoQL.TimeGrid TimeGrid()
    {
        Date resetDate = Option.ResetDate;
        Date lastExerciseDate = Option.Exercise.LastDate();
        double t1 = Process.Time(resetDate);
        double t2 = Process.Time(lastExerciseDate);
        int totalSteps = TimeSteps ?? (int)(TimeStepsPerYear!.Value * t2);
        return new NanoQL.TimeGrid([t1, t2], totalSteps);
    }

    /// <summary>The path generator: the traits' sequence generator, of the process's factors per step, and paths on the grid.</summary>
    /// <returns>A new path generator.</returns>
    protected override IPathGenerator<TPath> PathGenerator()
    {
        int dimensions = Process.Factors;
        NanoQL.TimeGrid grid = TimeGrid();
        IRsg gen = TMc.MakeSequenceGenerator(dimensions * (grid.Size - 1), Seed);
        return TMc.MakePathGenerator(Process, grid, gen, BrownianBridge);
    }

    /// <summary>
    /// The control variate's value: the control engine's NPV on a vanilla option with the option's payoff type and
    /// expiry, struck at the moneyness times the initial spot.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="NanoQLException">The engine gives no control engine, or the payoff isn't striked.</exception>
    protected override double? ControlVariateValue()
    {
        Func<VanillaOption, EvaluationContext, OneAssetOptionResults> controlPE = ControlPricingEngine()
            ?? throw new NanoQLException("engine does not provide control variation pricing engine");

        // Create vanilla option arguments with the same payoff and expiry, but with
        // strike-reset equal to initial spot*moneyness, price analytically
        if (Option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        double spot = Process.InitialValues()[0];
        double moneyness = Option.Moneyness;
        double strike = moneyness * spot;
        var newPayoff = new PlainVanillaPayoff(payoff.OptionType, strike);
        OneAssetOptionResults controlResults = controlPE(new VanillaOption(newPayoff, Option.Exercise), Context);
        return controlResults.NPV;
    }

    /// <summary>The control variate's engine, QuantLib's <c>controlPricingEngine()</c>; by default none.</summary>
    /// <returns>The engine, or <see langword="null"/>.</returns>
    protected virtual Func<VanillaOption, EvaluationContext, OneAssetOptionResults>? ControlPricingEngine() => null;
}
