// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015, 2016 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/mclongstaffschwartzengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Longstaff-Schwartz Monte Carlo engine for early-exercise options (Francis Longstaff, Eduardo Schwartz, 2001,
/// "Valuing American Options by Simulation: A Simple Least-Squares Approach", The Review of Financial Studies, Volume
/// 14, No. 1, 113-147): a calibration simulation whose paths the Longstaff-Schwartz pricer regresses, then the pricing
/// simulation on the same pricer.
/// </summary>
/// <typeparam name="TOption">The option, QuantLib's <c>GenericEngine</c>'s instrument.</typeparam>
/// <typeparam name="TMc">The pricing simulation's Monte Carlo traits, QuantLib's <c>MC&lt;RNG&gt;</c>.</typeparam>
/// <typeparam name="TMcCalibration">The calibration's traits, QuantLib's <c>MC&lt;RNG_Calibration&gt;</c>.</typeparam>
/// <typeparam name="TPath">The path type.</typeparam>
/// <typeparam name="TState">The early-exercise pricer's state type, which QuantLib's traits name for the path type.</typeparam>
/// <typeparam name="TStats">The statistics accumulator.</typeparam>
/// <remarks>
/// <para>
/// An extension point and an immutable object (the S-van plan's VR1 (ii)): a concrete engine's static
/// <c>Calculate</c> builds one per call and implements <see cref="LsmPathPricer"/>. <see cref="Run"/> builds the
/// pricer, adds the calibration samples on the calibration traits' generator, calibrates, prices on the same pricer and
/// returns the model with the call's exercise probability: QuantLib's mutable pricer and calibration model are the
/// call's working state, and the pricer reaches the pricing model as the call's argument, not as a member, so two calls
/// can share one engine object.
/// </para>
/// <para>
/// The calibration samples default to 2,048, and the calibration's Brownian bridge and antithetic variates to the
/// pricing's. The calibration seed defaults to the pricing seed plus 1,768,237,423, modulo 2³² as MSVC's 32-bit
/// <c>unsigned long</c> computes it, as QuantLib's documentation says; QuantLib's code compares the narrowed
/// <c>Null&lt;Size&gt;()</c> with <c>Null&lt;Real&gt;()</c>, so its default is always the narrowed null: a
/// documented divergence (the S-van plan's VR4 (v), <c>Divergences/LongstaffSchwartzCalibrationSeedTests.cs</c>);
/// QuantLib's engine with that seed passed reproduces this one's default. Where that default is 0, at the pricing seed
/// 2,526,729,873, it is refused (S2R4), since QuantLib reads 0 as a seed from the clock.
/// </para>
/// <para>
/// The pricer exercises only at the exercise times on the grid (<see cref="ExerciseTimes"/>): a Bermudan option's
/// dates, and an American option's grid times from its earliest exercise (the S-van plan's VR4 (xxiv)).
/// </para>
/// </remarks>
public abstract class MCLongstaffSchwartzEngine<TOption, TMc, TMcCalibration, TPath, TState, TStats> : McSimulation<TPath, TStats>
    where TOption : Option
    where TMc : IMcTraits<TPath>
    where TMcCalibration : IMcTraits<TPath>
    where TStats : IStatistics, new()
{
    // The documented offset of the calibration seed from the pricing seed (mclongstaffschwartzengine.hpp:149).
    private const long CalibrationSeedOffset = 1768237423L;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="MCLongstaffSchwartzEngine{TOption, TMc, TMcCalibration, TPath, TState, TStats}"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The process the paths follow.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/> for steps per year.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/> for a number of steps.</param>
    /// <param name="brownianBridge">Whether the pricing draws are ordered by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether the pricing uses antithetic variates.</param>
    /// <param name="controlVariate">Whether a control variate is used.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The absolute tolerance, or <see langword="null"/>; it takes precedence.</param>
    /// <param name="maxSamples">The most samples for a tolerance, or <see langword="null"/> for no limit.</param>
    /// <param name="seed">The pricing seed, in [1, 4294967295].</param>
    /// <param name="nCalibrationSamples">The calibration samples, or <see langword="null"/> for 2,048.</param>
    /// <param name="brownianBridgeCalibration">The calibration's Brownian bridge, or <see langword="null"/> for the pricing's.</param>
    /// <param name="antitheticVariateCalibration">The calibration's antithetic variates, or <see langword="null"/> for the pricing's.</param>
    /// <param name="seedCalibration">
    /// The calibration seed, in [1, 4294967295], or <see langword="null"/> for (<paramref name="seed"/> + 1768237423)
    /// mod 2³².
    /// </param>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Neither or both of the time steps and the time steps per year are given, or the one given isn't positive.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A number of samples is negative, a seed is outside its range, or the default calibration seed is 0.
    /// </exception>
    protected MCLongstaffSchwartzEngine(
        TOption option,
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
        long seed,
        int? nCalibrationSamples = null,
        bool? brownianBridgeCalibration = null,
        bool? antitheticVariateCalibration = null,
        long? seedCalibration = null)
        : base(antitheticVariate, controlVariate)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        MCVanillaEngine<TMc, TPath, TStats>.CheckSteps(timeSteps, timeStepsPerYear);
        MCVanillaEngine<TMc, TPath, TStats>.CheckSamples(requiredSamples, maxSamples);
        if (nCalibrationSamples is int calibrationSamples)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(calibrationSamples, nameof(nCalibrationSamples));
        }

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
        NCalibrationSamples = nCalibrationSamples ?? 2048;
        BrownianBridgeCalibration = brownianBridgeCalibration ?? brownianBridge;
        AntitheticVariateCalibration = antitheticVariateCalibration ?? antitheticVariate;
        SeedCalibration = DefaultedSeedCalibration(seed, seedCalibration);
    }

    /// <summary>Gets the option, QuantLib's engine arguments.</summary>
    protected TOption Option { get; }

    /// <summary>Gets the evaluation context.</summary>
    protected EvaluationContext Context { get; }

    /// <summary>Gets the process the paths follow.</summary>
    protected StochasticProcess Process { get; }

    /// <summary>Gets the number of time steps, or <see langword="null"/>.</summary>
    protected int? TimeSteps { get; }

    /// <summary>Gets the number of time steps per year, or <see langword="null"/>.</summary>
    protected int? TimeStepsPerYear { get; }

    /// <summary>Gets a value indicating whether the pricing draws are ordered by a Brownian bridge.</summary>
    protected bool BrownianBridge { get; }

    /// <summary>Gets the number of samples, or <see langword="null"/>.</summary>
    protected int? RequiredSamples { get; }

    /// <summary>Gets the absolute tolerance, or <see langword="null"/>.</summary>
    protected double? RequiredTolerance { get; }

    /// <summary>Gets the most samples for a tolerance, or <see langword="null"/> for no limit.</summary>
    protected int? MaxSamples { get; }

    /// <summary>Gets the pricing seed.</summary>
    protected long Seed { get; }

    /// <summary>Gets the number of calibration samples.</summary>
    protected int NCalibrationSamples { get; }

    /// <summary>Gets a value indicating whether the calibration draws are ordered by a Brownian bridge.</summary>
    protected bool BrownianBridgeCalibration { get; }

    /// <summary>Gets a value indicating whether the calibration uses antithetic variates.</summary>
    protected bool AntitheticVariateCalibration { get; }

    /// <summary>Gets the calibration seed.</summary>
    protected long SeedCalibration { get; }

    /// <summary>
    /// QuantLib's <c>calculate</c>: the calibration simulation, the regression and the pricing simulation on the same
    /// Longstaff-Schwartz pricer.
    /// </summary>
    /// <returns>The pricing model and the call's exercise probability.</returns>
    /// <exception cref="NanoQLException">The simulation refuses, or the samples can't reach the tolerance.</exception>
    protected (MonteCarloModel<TPath, TStats> Model, double ExerciseProbability) Run()
    {
        // calibration
        LongstaffSchwartzPathPricer<TPath, TState> pricer = LsmPathPricer();
        int dimensions = Process.Factors;
        NanoQL.TimeGrid grid = TimeGrid();
        IRsg generator = TMcCalibration.MakeSequenceGenerator(dimensions * (grid.Size - 1), SeedCalibration);
        IPathGenerator<TPath> pathGeneratorCalibration =
            TMcCalibration.MakePathGenerator(Process, grid, generator, BrownianBridgeCalibration);
        var mcModelCalibration = new MonteCarloModel<TPath, TStats>(
            pathGeneratorCalibration, pricer, new TStats(), AntitheticVariateCalibration);

        mcModelCalibration.AddSamples(NCalibrationSamples);
        pricer.Calibrate();

        // pricing
        MonteCarloModel<TPath, TStats> model = Calculate(
            RequiredTolerance, RequiredSamples, MaxSamples ?? int.MaxValue, pricer);
        return (model, pricer.ExerciseProbability());
    }

    /// <summary>The Longstaff-Schwartz pricer of this call, new.</summary>
    /// <returns>The pricer.</returns>
    protected abstract LongstaffSchwartzPathPricer<TPath, TState> LsmPathPricer();

    /// <summary>
    /// QuantLib's <c>pathPricer()</c>, which returns the pricer its <c>calculate</c> stored: <see cref="Run"/> passes
    /// the call's calibrated pricer to the pricing model, so outside it there is none.
    /// </summary>
    /// <returns>Nothing: it throws.</returns>
    /// <exception cref="NanoQLException">Always ("path pricer unknown").</exception>
    protected override PathPricer<TPath> PathPricer() => throw new NanoQLException("path pricer unknown");

    /// <summary>
    /// The time grid: to an American option's last exercise, or through a Bermudan's exercise times after the start,
    /// in the steps given or the steps per year of the last time, at least one.
    /// </summary>
    /// <returns>The grid.</returns>
    /// <exception cref="NanoQLException">No exercise time lies after the start, or no steps are given.</exception>
    protected override NanoQL.TimeGrid TimeGrid()
    {
        List<double> requiredTimes = RequiredTimes();
        if (TimeSteps is int timeSteps)
        {
            return new NanoQL.TimeGrid([.. requiredTimes], timeSteps);
        }
        else if (TimeStepsPerYear is int timeStepsPerYear)
        {
            int steps = (int)(timeStepsPerYear * requiredTimes[^1]);
            return new NanoQL.TimeGrid([.. requiredTimes], Math.Max(steps, 1));
        }
        else
        {
            throw new NanoQLException("time steps not specified");
        }
    }

    /// <summary>The pricing simulation's path generator: the traits' generator of the process's factors per step.</summary>
    /// <returns>A new path generator.</returns>
    protected override IPathGenerator<TPath> PathGenerator()
    {
        int dimensions = Process.Factors;
        NanoQL.TimeGrid grid = TimeGrid();
        IRsg generator = TMc.MakeSequenceGenerator(dimensions * (grid.Size - 1), Seed);
        return TMc.MakePathGenerator(Process, grid, generator, BrownianBridge);
    }

    /// <summary>
    /// The grid's exercise times, beside its last, for the Longstaff-Schwartz pricer (the S-van plan's VR4 (xxiv)): a
    /// Bermudan option's exercise times after the start, and an American option's grid times from its earliest exercise.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <returns>The times.</returns>
    protected IReadOnlyList<double> ExerciseTimes(NanoQL.TimeGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (Option.Exercise.Type == ExerciseType.American)
        {
            double earliest = Process.Time(Option.Exercise.Dates[0]);
            var times = new List<double>();
            for (int i = 1; i < grid.Size; i++)
            {
                if (grid[i] >= earliest || ComparisonFunctions.CloseEnough(grid[i], earliest))
                {
                    times.Add(grid[i]);
                }
            }

            return times;
        }

        return RequiredTimes();
    }

    // QuantLib's required times: an American option's last exercise, or a Bermudan's exercise times after the start.
    private List<double> RequiredTimes()
    {
        var requiredTimes = new List<double>();
        if (Option.Exercise.Type == ExerciseType.American)
        {
            Date lastExerciseDate = Option.Exercise.LastDate();
            requiredTimes.Add(Process.Time(lastExerciseDate));
        }
        else
        {
            foreach (Date date in Option.Exercise.Dates)
            {
                double t = Process.Time(date);
                if (t > 0.0)
                {
                    requiredTimes.Add(t);
                }
            }
        }

        // QuantLib reads the last of no times
        if (requiredTimes.Count == 0)
        {
            throw new NanoQLException("no exercise time after the grid's start");
        }

        return requiredTimes;
    }

    // QuantLib's documented default, (seed + 1768237423) mod 2^32 in MSVC's 32-bit unsigned long (the S-van plan's
    // VR4 (v)); 0 there, a seed from the clock in QuantLib, refused (S2R4).
    private static long DefaultedSeedCalibration(long seed, long? seedCalibration)
    {
        if (seedCalibration is long given)
        {
            Seeds.Check(given, nameof(seedCalibration));
            return given;
        }

        long seedDefault = (seed + CalibrationSeedOffset) % 4294967296L;
        if (seedDefault == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seedCalibration),
                seedCalibration,
                $"the default calibration seed, (seed + 1768237423) mod 2^32, is 0 for seed {seed}, which QuantLib reads "
                + "as a seed drawn from the clock: give the calibration seed");
        }

        return seedDefault;
    }
}
