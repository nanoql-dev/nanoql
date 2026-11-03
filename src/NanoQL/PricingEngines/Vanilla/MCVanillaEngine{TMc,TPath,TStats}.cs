// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mcvanillaengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The base of the Monte Carlo engines for vanilla options: a time grid to the last exercise, paths of the process on
/// it, and a control variate's value from a control engine.
/// </summary>
/// <typeparam name="TMc">The Monte Carlo traits, QuantLib's <c>MC&lt;RNG&gt;</c>: the path type and its generator.</typeparam>
/// <typeparam name="TPath">The path type.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// <para>
/// An extension point and an immutable object (over <see cref="McSimulation{TPath, TStats}"/>): a concrete engine's
/// static <c>Calculate</c> builds one per call from the option, the context, the process and QuantLib's settings, with
/// QuantLib's constructor checks, implements the path pricer, and reads <see cref="Run"/>'s model and results. The
/// vanilla option is its only instrument, as in every QuantLib instantiation. The control variate's value comes from
/// <see cref="ControlPricingEngine"/>, an engine delegate of the engines' form, on the option.
/// </para>
/// <para>
/// The process is the engine's: the Black-Scholes engines pass the process on their clock, whose times run from the
/// earliest structure's reference date, so <see cref="TimeGrid"/>'s times are that clock's.
/// </para>
/// </remarks>
public abstract class MCVanillaEngine<TMc, TPath, TStats> : McSimulation<TPath, TStats>
    where TMc : IMcTraits<TPath>
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MCVanillaEngine{TMc, TPath, TStats}"/> class.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The process the paths follow.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/> for a number of steps.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="controlVariate">Whether a control variate is used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Neither or both of the time steps and the time steps per year are given, or the one given isn't positive.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="requiredSamples"/> or <paramref name="maxSamples"/> is negative, or <paramref name="seed"/> is
    /// outside its range.
    /// </exception>
    protected MCVanillaEngine(
        VanillaOption option,
        EvaluationContext context,
        StochasticProcess process,
        int? timeSteps,
        int? timeStepsPerYear,
        bool brownianBridge,
        bool antitheticVariate,
        bool controlVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed)
        : base(antitheticVariate, controlVariate)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        CheckSteps(timeSteps, timeStepsPerYear);
        CheckSamples(requiredSamples, maxSamples);
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
    protected VanillaOption Option { get; }

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
    /// QuantLib's <c>calculate</c>: runs the simulation and returns its model and QuantLib's results, the mean as the
    /// value and, where the traits allow one, the error estimate.
    /// </summary>
    /// <returns>The call's model and results.</returns>
    /// <exception cref="NanoQLException">The simulation refuses, or the samples can't reach the tolerance.</exception>
    protected (MonteCarloModel<TPath, TStats> Model, OneAssetOptionResults Results) Run()
    {
        MonteCarloModel<TPath, TStats> model = Calculate(RequiredTolerance, RequiredSamples, MaxSamples ?? int.MaxValue);
        var results = new OneAssetOptionResults
        {
            NPV = model.SampleAccumulator.Mean(),
            ErrorEstimate = TMc.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,
        };
        return (model, results);
    }

    /// <summary>The time grid: to the last exercise, in the steps given or the steps per year, at least one.</summary>
    /// <returns>The grid.</returns>
    /// <exception cref="NanoQLException">No steps are given.</exception>
    protected override NanoQL.TimeGrid TimeGrid()
    {
        Date lastExerciseDate = Option.Exercise.LastDate();
        double t = Process.Time(lastExerciseDate);
        if (TimeSteps is int timeSteps)
        {
            return new NanoQL.TimeGrid(t, timeSteps);
        }
        else if (TimeStepsPerYear is int timeStepsPerYear)
        {
            int steps = (int)(timeStepsPerYear * t);
            return new NanoQL.TimeGrid(t, Math.Max(steps, 1));
        }
        else
        {
            throw new NanoQLException("time steps not specified");
        }
    }

    /// <summary>The path generator: the traits' sequence generator, of the process's factors per step, and paths on the grid.</summary>
    /// <returns>A new path generator.</returns>
    protected override IPathGenerator<TPath> PathGenerator()
    {
        int dimensions = Process.Factors;
        NanoQL.TimeGrid grid = TimeGrid();
        IRsg generator = TMc.MakeSequenceGenerator(dimensions * (grid.Size - 1), Seed);
        return TMc.MakePathGenerator(Process, grid, generator, BrownianBridge);
    }

    /// <summary>The control variate's value: the control engine's NPV on the option.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NanoQLException">The engine gives no control engine, or its NPV.</exception>
    protected override double? ControlVariateValue()
    {
        Func<VanillaOption, EvaluationContext, OneAssetOptionResults> controlPE = ControlPricingEngine()
            ?? throw new NanoQLException("engine does not provide control variation pricing engine");

        OneAssetOptionResults controlResults = controlPE(Option, Context);
        return controlResults.NPV ?? throw new NanoQLException("engine returns an inconsistent result type");
    }

    /// <summary>The control variate's engine, QuantLib's <c>controlPricingEngine()</c>; by default none.</summary>
    /// <returns>The engine, or <see langword="null"/>.</returns>
    protected virtual Func<VanillaOption, EvaluationContext, OneAssetOptionResults>? ControlPricingEngine() => null;

    // QuantLib's constructor checks on the steps, shared with the Longstaff-Schwartz base.
    internal static void CheckSteps(int? timeSteps, int? timeStepsPerYear)
    {
        if (!(timeSteps is not null || timeStepsPerYear is not null))
        {
            throw new ArgumentException("no time steps provided", nameof(timeSteps));
        }

        if (!(timeSteps is null || timeStepsPerYear is null))
        {
            throw new ArgumentException("both time steps and time steps per year were provided", nameof(timeStepsPerYear));
        }

        if (timeSteps is int steps && !(steps > 0))
        {
            throw new ArgumentException($"timeSteps must be positive, {steps} not allowed", nameof(timeSteps));
        }

        if (timeStepsPerYear is int perYear && !(perYear > 0))
        {
            throw new ArgumentException($"timeStepsPerYear must be positive, {perYear} not allowed", nameof(timeStepsPerYear));
        }
    }

    // The sample counts, QuantLib's unsigned sizes.
    internal static void CheckSamples(int? requiredSamples, int? maxSamples)
    {
        if (requiredSamples is int samples)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(samples, nameof(requiredSamples));
        }

        if (maxSamples is int max)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(max, nameof(maxSamples));
        }
    }
}
