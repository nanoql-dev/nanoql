// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/mcdiscreteasianenginebase.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The base of the Monte Carlo engines for discrete-averaging Asian options: a time grid of the fixings to come, paths of
/// the process on it, and a control variate's value from a control engine.
/// </summary>
/// <typeparam name="TMc">The Monte Carlo traits, QuantLib's <c>MC&lt;RNG&gt;</c>: the path type and its generator.</typeparam>
/// <typeparam name="TPath">The path type.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// <para>
/// An extension point and an immutable object (the S-bar plan's BR1 (iii), the S-van plan's VR1 form over
/// <see cref="McSimulation{TPath, TStats}"/>): a concrete engine's static <c>Calculate</c> builds one per call from the
/// option, the context, the process and QuantLib's settings, implements the path pricer, and reads <see cref="Run"/>'s
/// model and results. QuantLib's arguments are the option's own, split by the context's date where it holds every
/// past fixing (SD18), read once the engine prices: <see cref="AverageType"/>, <see cref="RunningAccumulator"/>,
/// <see cref="PastFixings"/>, <see cref="FixingDates"/> and the past fixings' log-sum, <see cref="RunningLog"/>. The
/// control variate's value comes from <see cref="ControlPricingEngine"/>, an engine delegate of D14's type, on the
/// option; a controlled value is clamped at 0, and the grid is the additional result <c>"TimeGrid"</c>, as QuantLib's.
/// Every fixing in the past, or the one left today, is refused with QuantLib's "all fixings are in the past".
/// </para>
/// <para>
/// The process is the engine's: the Black-Scholes and Heston engines pass the process on their clock, whose times run
/// from the earliest structure's reference date (BR1 (v)), so <see cref="TimeGrid"/>'s times are that clock's.
/// </para>
/// <para>
/// A fixing dated from the evaluation date to the clock's origin, where the structures settle after the evaluation
/// date, is taken at the origin, the spot with no variance and no discount accrued, so as a past fixing at the spot
/// (the S-bar code review's B3-1, <c>Divergences/AsianLateSettlementTests.cs</c>), where QuantLib's grid drops it from
/// the average.
/// </para>
/// </remarks>
public abstract class MCDiscreteAveragingAsianEngineBase<TMc, TPath, TStats> : McSimulation<TPath, TStats>
    where TMc : IMcTraits<TPath>
    where TStats : IStatistics, new()
{
    private DiscreteAveragingAsianArguments? arguments;

    /// <summary>
    /// Initializes a new instance of the <see cref="MCDiscreteAveragingAsianEngineBase{TMc, TPath, TStats}"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The process the paths follow.</param>
    /// <param name="brownianBridge">Whether the draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="controlVariate">Whether a control variate is used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for the fixing times alone.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="includeExerciseDate">Whether the exercise time joins the grid where it follows the last fixing.</param>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="requiredSamples"/> or <paramref name="maxSamples"/> is negative, or <paramref name="seed"/> is
    /// outside its range.
    /// </exception>
    protected MCDiscreteAveragingAsianEngineBase(
        DiscreteAveragingAsianOption option,
        EvaluationContext context,
        StochasticProcess process,
        bool brownianBridge,
        bool antitheticVariate,
        bool controlVariate,
        int? requiredSamples,
        double? requiredTolerance,
        int? maxSamples,
        long seed,
        int? timeSteps = null,
        int? timeStepsPerYear = null,
        bool includeExerciseDate = false)
        : base(antitheticVariate, controlVariate)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        MCVanillaEngine<TMc, TPath, TStats>.CheckSamples(requiredSamples, maxSamples);
        Seeds.Check(seed);
        Option = option;
        Context = context;
        Process = process;
        BrownianBridge = brownianBridge;
        RequiredSamples = requiredSamples;
        RequiredTolerance = requiredTolerance;
        MaxSamples = maxSamples;
        Seed = seed;
        TimeSteps = timeSteps;
        TimeStepsPerYear = timeStepsPerYear;
        IncludeExerciseDate = includeExerciseDate;
    }

    /// <summary>Gets the option.</summary>
    protected DiscreteAveragingAsianOption Option { get; }

    /// <summary>Gets the evaluation context.</summary>
    protected EvaluationContext Context { get; }

    /// <summary>Gets the process the paths follow.</summary>
    protected StochasticProcess Process { get; }

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

    /// <summary>Gets the number of time steps, or <see langword="null"/>.</summary>
    protected int? TimeSteps { get; }

    /// <summary>Gets the number of time steps per year, or <see langword="null"/>.</summary>
    protected int? TimeStepsPerYear { get; }

    /// <summary>Gets a value indicating whether the exercise time joins the grid after the last fixing.</summary>
    protected bool IncludeExerciseDate { get; }

    /// <summary>Gets the average's type, QuantLib's <c>arguments_.averageType</c>.</summary>
    protected AverageType AverageType => Arguments.AverageType;

    /// <summary>Gets the running sum or product of the past fixings, QuantLib's <c>arguments_.runningAccumulator</c>.</summary>
    protected double RunningAccumulator => Arguments.RunningAccumulator;

    /// <summary>Gets the number of past fixings, QuantLib's <c>arguments_.pastFixings</c>.</summary>
    protected int PastFixings => Arguments.PastFixings;

    /// <summary>Gets the fixing dates to come, sorted, QuantLib's <c>arguments_.fixingDates</c>.</summary>
    protected ImmutableArray<Date> FixingDates => Arguments.FixingDates;

    /// <summary>
    /// Gets the log of the running product: its own log where the product is finite and normal, else the past fixings'
    /// log-sum (the S-bar plan's BR5 (xli)).
    /// </summary>
    protected double RunningLog => Arguments.RunningLog;

    // QuantLib's setupArguments, once the instrument prices: an expired option never reads it
    // and the fixings from the evaluation date to the clock's origin taken there, as past fixings at the spot
    private DiscreteAveragingAsianArguments Arguments => arguments ??= Option.Arguments(Context).AtOrigin(
        Context.EvaluationDate, d => Process.Time(d) < 0.0, Process.InitialValues()[0]);

    /// <summary>
    /// QuantLib's <c>calculate</c>: runs the simulation and returns its model and QuantLib's results, the mean as the
    /// value, at least 0 under a control variate, the error estimate where the traits allow one, and the grid.
    /// </summary>
    /// <returns>The call's model and results.</returns>
    /// <exception cref="NanoQLException">Every fixing is in the past, the simulation refuses, or the samples can't reach the tolerance.</exception>
    protected (MonteCarloModel<TPath, TStats> Model, OneAssetOptionResults Results) Run()
    {
        MonteCarloModel<TPath, TStats> model = Calculate(RequiredTolerance, RequiredSamples, MaxSamples ?? int.MaxValue);
        double value = model.SampleAccumulator.Mean();
        if (ControlVariate)
        {
            // control variate might lead to small negative
            // option values for deep OTM options
            value = Math.Max(0.0, value);
        }

        var results = new OneAssetOptionResults
        {
            NPV = value,
            ErrorEstimate = TMc.AllowsErrorEstimate ? model.SampleAccumulator.ErrorEstimate() : null,

            // Allow inspection of the timeGrid via additional results
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("TimeGrid", TimeGrid()),
        };
        return (model, results);
    }

    /// <summary>
    /// The time grid: the fixing times to come, the exercise time after them where it joins, in the steps given or the
    /// steps per year where either is.
    /// </summary>
    /// <returns>The grid.</returns>
    /// <exception cref="NanoQLException">Every fixing is in the past, or the one left is today.</exception>
    protected override NanoQL.TimeGrid TimeGrid()
    {
        var fixingTimes = new List<double>(FixingDates.Length + 1);
        foreach (Date fixingDate in FixingDates)
        {
            double t = Process.Time(fixingDate);
            if (t >= 0)
            {
                fixingTimes.Add(t);
            }
        }

        if (fixingTimes.Count == 0 || (fixingTimes.Count == 1 && fixingTimes[0] == 0.0))
        {
            throw new NanoQLException("all fixings are in the past");
        }

        // Some models (eg. Heston) might request additional points in
        // the time grid to improve the accuracy of the discretization
        Date lastExerciseDate = Option.Exercise.LastDate();
        double exerciseTime = Process.Time(lastExerciseDate);

        if (IncludeExerciseDate && exerciseTime > fixingTimes[^1])
        {
            fixingTimes.Add(exerciseTime);
        }

        if (TimeSteps is int timeSteps)
        {
            return new NanoQL.TimeGrid(fixingTimes.ToArray(), timeSteps);
        }
        else if (TimeStepsPerYear is int timeStepsPerYear)
        {
            return new NanoQL.TimeGrid(fixingTimes.ToArray(), (int)(timeStepsPerYear * exerciseTime));
        }

        return new NanoQL.TimeGrid(fixingTimes.ToArray());
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

    /// <summary>The control variate's value: the control engine's NPV on the option.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="NanoQLException">The engine gives no control engine, or the control engine no NPV.</exception>
    protected override double? ControlVariateValue()
    {
        Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> controlPE = ControlPricingEngine()
            ?? throw new NanoQLException("engine does not provide control variation pricing engine");

        OneAssetOptionResults controlResults = controlPE(Option, Context);
        return controlResults.NPV ?? throw new NanoQLException("engine returns an inconsistent result type");
    }

    /// <summary>The control variate's engine, QuantLib's <c>controlPricingEngine()</c>; by default none.</summary>
    /// <returns>The engine, or <see langword="null"/>.</returns>
    protected virtual Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults>? ControlPricingEngine() => null;

    /// <summary>
    /// The number of fixings at each node of a grid, its fixing dates to come counted by the node their time falls on,
    /// so a date given twice, or two dates on one time, counts twice (the S-bar plan's BR5 (xxviii)), where QuantLib's
    /// grid keeps each time once and its pricers count it once.
    /// </summary>
    /// <param name="grid">The engine's grid.</param>
    /// <returns>The count at each node, 0 at a node that isn't a fixing.</returns>
    protected ImmutableArray<int> FixingMultiplicities(NanoQL.TimeGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var counts = new int[grid.Size];
        foreach (Date fixingDate in FixingDates)
        {
            double t = Process.Time(fixingDate);
            if (t >= 0)
            {
                ++counts[grid.Index(t)];
            }
        }

        return [.. counts];
    }

    /// <summary>
    /// The grid's index of each fixing to come, once per fixing, so a date given twice, or two dates on one time, gives
    /// its index twice (the S-bar plan's BR5 (xxviii)), where QuantLib's engines index each of the grid's fixing times
    /// once.
    /// </summary>
    /// <param name="grid">The engine's grid.</param>
    /// <returns>The indices, in the fixings' order.</returns>
    protected ImmutableArray<int> FixingIndices(NanoQL.TimeGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ImmutableArray<int>.Builder indices = ImmutableArray.CreateBuilder<int>(FixingDates.Length);
        foreach (Date fixingDate in FixingDates)
        {
            double t = Process.Time(fixingDate);
            if (t >= 0)
            {
                indices.Add(grid.ClosestIndex(t));
            }
        }

        return indices.ToImmutable();
    }
}
