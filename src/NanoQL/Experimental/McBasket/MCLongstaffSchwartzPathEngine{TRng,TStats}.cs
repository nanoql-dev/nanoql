// SPDX-FileCopyrightText: Copyright (C) 2009 Andrea Odetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/mcbasket/mclongstaffschwartzpathengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The Longstaff-Schwartz Monte Carlo engine for path options (Longstaff and Schwartz, 2001): calibration paths fit
/// the exercise decisions, then the pricing paths are valued on them.
/// </summary>
/// <typeparam name="TRng">The random-number traits.</typeparam>
/// <typeparam name="TStats">The statistics.</typeparam>
/// <remarks>
/// <para>
/// An extension point and an immutable object (the S-xeq plan's §2.0, S-van's VR1 form): a concrete engine's static
/// <c>Calculate</c> builds one per call and implements <see cref="LsmPathPricer"/>. <see cref="Run"/> builds the
/// pricer, adds the calibration samples, calibrates and prices on the same pricer, QuantLib's mutable pricer the call's
/// working state.
/// </para>
/// <para>
/// One seed draws the calibration and the pricing paths, so the pricing run's first paths are the calibration's
/// (Longstaff and Schwartz's in-sample estimator, QuantLib's code); there is no calibration seed, as QuantLib's path
/// engine has none (the S-xeq plan's XED3). The calibration samples default to 2,048; none is refused, where QuantLib's
/// calibration reads the first of no path (XER5 (xxiii)).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class MCLongstaffSchwartzPathEngine<TRng, TStats> : McSimulation<MultiPath, TStats>
    where TRng : IRngTraits
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="MCLongstaffSchwartzPathEngine{TRng, TStats}"/> class.</summary>
    /// <param name="option">The path option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The process the paths follow.</param>
    /// <param name="timeSteps">The number of time steps, or <see langword="null"/>.</param>
    /// <param name="timeStepsPerYear">The number of time steps per year, or <see langword="null"/>.</param>
    /// <param name="brownianBridge">Whether to build the paths by a Brownian bridge.</param>
    /// <param name="antitheticVariate">Whether to use antithetic paths.</param>
    /// <param name="controlVariate">Whether to use a control variate, which this engine doesn't provide.</param>
    /// <param name="requiredSamples">The number of samples, or <see langword="null"/>.</param>
    /// <param name="requiredTolerance">The tolerance, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The largest number of samples, or <see langword="null"/>.</param>
    /// <param name="seed">The seed, in [1, 4294967295].</param>
    /// <param name="nCalibrationSamples">The calibration samples, positive, or <see langword="null"/> for 2,048.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The time steps are neither or both given, or not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The seed or a sample count is out of range, or
    /// <paramref name="nCalibrationSamples"/> isn't positive (XER5 (xxiii)).</exception>
    protected MCLongstaffSchwartzPathEngine(
        PathMultiAssetOption option,
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
        int? nCalibrationSamples = null)
        : base(antitheticVariate, controlVariate)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        MCVanillaEngine<MultiVariate<TRng>, MultiPath, TStats>.CheckSteps(timeSteps, timeStepsPerYear);
        MCVanillaEngine<MultiVariate<TRng>, MultiPath, TStats>.CheckSamples(requiredSamples, maxSamples);
        if (nCalibrationSamples is int calibrationSamples)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(calibrationSamples, nameof(nCalibrationSamples));
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
    }

    /// <summary>Gets the path option.</summary>
    protected PathMultiAssetOption Option { get; }

    /// <summary>Gets the evaluation context.</summary>
    protected EvaluationContext Context { get; }

    /// <summary>Gets the process the paths follow.</summary>
    protected StochasticProcess Process { get; }

    /// <summary>Gets the number of time steps, or <see langword="null"/>.</summary>
    protected int? TimeSteps { get; }

    /// <summary>Gets the number of time steps per year, or <see langword="null"/>.</summary>
    protected int? TimeStepsPerYear { get; }

    /// <summary>Gets whether the paths are built by a Brownian bridge.</summary>
    protected bool BrownianBridge { get; }

    /// <summary>Gets the number of samples, or <see langword="null"/>.</summary>
    protected int? RequiredSamples { get; }

    /// <summary>Gets the tolerance, or <see langword="null"/>.</summary>
    protected double? RequiredTolerance { get; }

    /// <summary>Gets the largest number of samples, or <see langword="null"/>.</summary>
    protected int? MaxSamples { get; }

    /// <summary>Gets the seed.</summary>
    protected long Seed { get; }

    /// <summary>Gets the number of calibration samples.</summary>
    protected int NCalibrationSamples { get; }

    /// <summary>
    /// QuantLib's <c>calculate()</c>: the pricer, the calibration samples on the pricing generator, the calibration,
    /// and the pricing on the same pricer.
    /// </summary>
    /// <returns>The pricing model.</returns>
    protected MonteCarloModel<MultiPath, TStats> Run()
    {
        LongstaffSchwartzMultiPathPricer pricer = LsmPathPricer();
        var mcModel = new MonteCarloModel<MultiPath, TStats>(PathGenerator(), pricer, new TStats(), AntitheticVariate);
        mcModel.AddSamples(NCalibrationSamples);
        pricer.Calibrate();
        return Calculate(RequiredTolerance, RequiredSamples, MaxSamples ?? int.MaxValue, pricer);
    }

    /// <summary>The engine's path pricer, in its calibration phase.</summary>
    /// <returns>The pricer.</returns>
    protected abstract LongstaffSchwartzMultiPathPricer LsmPathPricer();

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always ("path pricer unknown"): <see cref="Run"/> passes its pricer.</exception>
    protected override PathPricer<MultiPath> PathPricer() => throw new NanoQLException("path pricer unknown");

    /// <inheritdoc/>
    protected override TimeGrid TimeGrid() =>
        MCPathBasketEngine.FixingGrid(Process, Option.FixingDates, TimeSteps, TimeStepsPerYear);

    /// <inheritdoc/>
    protected override IPathGenerator<MultiPath> PathGenerator()
    {
        int dimensions = Process.Factors;
        TimeGrid grid = TimeGrid();
        IRsg generator = MultiVariate<TRng>.MakeSequenceGenerator(dimensions * (grid.Size - 1), Seed);
        return MultiVariate<TRng>.MakePathGenerator(Process, grid, generator, BrownianBridge);
    }
}
