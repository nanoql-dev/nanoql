// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/mcsimulation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Numerics;
using static NanoQL.Utilities.DataFormattersFunctions;

namespace NanoQL.PricingEngines;

/// <summary>
/// The base class for Monte Carlo engines: deriving a class from it gives an easy way to write a Monte Carlo engine.
/// </summary>
/// <typeparam name="TPath">The path type: QuantLib's traits' <c>path_type</c>.</typeparam>
/// <typeparam name="TStats">The statistics accumulator, QuantLib's <c>S</c>.</typeparam>
/// <remarks>
/// <para>
/// An extension point and an immutable object: an engine derives from it, holds its settings and its call's instrument
/// and context, and implements the hooks, each returning a new object per call. <see cref="Calculate"/> builds a new
/// <see cref="MonteCarloModel{TPath, TStats}"/>, runs QuantLib's <c>value</c> or <c>valueWithSamples</c> loop on it and
/// returns it, so QuantLib's mutable <c>mcModel_</c> is the call's result and two calculations can share one engine;
/// its <c>errorEstimate()</c> and <c>sampleAccumulator()</c> read the returned model's accumulator. QuantLib's
/// <c>controlPricingEngine()</c>, which the base never reads, is declared by the engines that read it, with their own
/// engine delegate's type.
/// </para>
/// <para>
/// A NaN error estimate is refused, where QuantLib's tolerance check skips it and returns the mean; and so is a
/// next-batch estimate past the samples' range (a tolerance of 0 makes it infinite), where QuantLib's conversion to
/// <c>Size</c> is undefined: documented divergences (<c>Divergences/McSimulationUndefinedTests.cs</c>).
/// </para>
/// </remarks>
public abstract class McSimulation<TPath, TStats>
    where TStats : IStatistics, new()
{
    /// <summary>Initializes a new instance of the <see cref="McSimulation{TPath, TStats}"/> class.</summary>
    /// <param name="antitheticVariate">Whether antithetic variates are used.</param>
    /// <param name="controlVariate">Whether a control variate is used.</param>
    protected McSimulation(bool antitheticVariate, bool controlVariate)
    {
        AntitheticVariate = antitheticVariate;
        ControlVariate = controlVariate;
    }

    /// <summary>Gets a value indicating whether antithetic variates are used.</summary>
    protected bool AntitheticVariate { get; }

    /// <summary>Gets a value indicating whether a control variate is used.</summary>
    protected bool ControlVariate { get; }

    /// <summary>
    /// Adds samples to a model until its error estimate reaches a tolerance: at least <paramref name="minSamples"/>,
    /// then batches estimated conservatively from the error, within <paramref name="maxSamples"/>.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="tolerance">The required absolute tolerance.</param>
    /// <param name="maxSamples">The most samples.</param>
    /// <param name="minSamples">The fewest samples.</param>
    /// <returns>The mean.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxSamples"/> or <paramref name="minSamples"/> is negative.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The maximum is reached with the error still above the tolerance; or the error is NaN, or the next batch's
    /// estimate is past the samples' range (divergences: QuantLib returns the mean, or converts out of range).
    /// </exception>
    public static double Value(
        MonteCarloModel<TPath, TStats> model, double tolerance, int maxSamples = int.MaxValue, int minSamples = 1023)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSamples);
        ArgumentOutOfRangeException.ThrowIfNegative(minSamples);
        int sampleNumber = model.SampleAccumulator.Samples;
        if (sampleNumber < minSamples)
        {
            model.AddSamples(minSamples - sampleNumber);
            sampleNumber = model.SampleAccumulator.Samples;
        }

        int nextBatch;
        double order;
        double error = model.SampleAccumulator.ErrorEstimate();
        while (MaxError(error) > tolerance)
        {
            if (!(sampleNumber < maxSamples))
            {
                throw new NanoQLException(
                    $"max number of samples ({maxSamples}) reached, while error ({General(error)}) is still above "
                    + $"tolerance ({General(tolerance)})");
            }

            // conservative estimate of how many samples are needed
            order = MaxError(error * error) / tolerance / tolerance;
            double estimate = ((double)sampleNumber * order * 0.8) - sampleNumber;
            estimate = estimate < minSamples ? minSamples : estimate;

            // QuantLib converts the estimate to Size, undefined past its range.
            if (!(estimate < 18446744073709551616.0))
            {
                throw new NanoQLException($"next batch's estimate ({General(estimate)}) out of range");
            }

            // do not exceed maxSamples
            nextBatch = (int)Math.Min(Math.Floor(estimate), (double)(maxSamples - sampleNumber));
            sampleNumber += nextBatch;
            model.AddSamples(nextBatch);
            error = model.SampleAccumulator.ErrorEstimate();
        }

        return model.SampleAccumulator.Mean();
    }

    /// <summary>Adds samples to a model up to a fixed number.</summary>
    /// <param name="model">The model.</param>
    /// <param name="samples">The number of samples, at least those already simulated.</param>
    /// <returns>The mean.</returns>
    /// <exception cref="NanoQLException">More samples than asked for are already simulated.</exception>
    public static double ValueWithSamples(MonteCarloModel<TPath, TStats> model, int samples)
    {
        ArgumentNullException.ThrowIfNull(model);
        int sampleNumber = model.SampleAccumulator.Samples;

        if (!(samples >= sampleNumber))
        {
            throw new NanoQLException(
                $"number of already simulated samples ({sampleNumber}) greater than requested samples ({samples})");
        }

        model.AddSamples(samples - sampleNumber);

        return model.SampleAccumulator.Mean();
    }

    /// <summary>
    /// The basic calculation the engines run: builds a new model from the hooks and adds samples to it, to the
    /// required tolerance where one is given, otherwise to the required number of samples.
    /// </summary>
    /// <param name="requiredTolerance">The required tolerance, or <see langword="null"/>.</param>
    /// <param name="requiredSamples">The required number of samples, or <see langword="null"/>.</param>
    /// <param name="maxSamples">The most samples for a tolerance; <see cref="int.MaxValue"/>, QuantLib's null, for no
    /// limit but <see cref="Value"/>'s default, which it is.</param>
    /// <param name="pathPricer">
    /// The call's path pricer, or <see langword="null"/> for <see cref="PathPricer"/>'s: an engine that builds its
    /// pricer earlier in the call (the Longstaff-Schwartz engine's, calibrated) passes it here, where QuantLib keeps it
    /// in a member the hook returns.
    /// </param>
    /// <returns>The model, its accumulator the samples'.</returns>
    /// <exception cref="NanoQLException">
    /// Neither a tolerance nor a number of samples is given; a control variate is used and the engine gives no
    /// control price or path pricer; or <see cref="Value"/> or <see cref="ValueWithSamples"/> refuses.
    /// </exception>
    protected MonteCarloModel<TPath, TStats> Calculate(
        double? requiredTolerance, int? requiredSamples, int maxSamples, PathPricer<TPath>? pathPricer = null)
    {
        if (!(requiredTolerance is not null || requiredSamples is not null))
        {
            throw new NanoQLException("neither tolerance nor number of samples set");
        }

        //! Initialize the one-factor Monte Carlo
        MonteCarloModel<TPath, TStats> model;
        if (ControlVariate)
        {
            double controlVariateValue = ControlVariateValue()
                ?? throw new NanoQLException("engine does not provide control-variation price");

            PathPricer<TPath> controlPP = ControlPathPricer()
                ?? throw new NanoQLException("engine does not provide control-variation path pricer");

            IPathGenerator<TPath>? controlPG = ControlPathGenerator();

            // QuantLib builds the model from make_shared's arguments, which MSVC evaluates right to left: the path
            // pricer, then the path generator.
            PathPricer<TPath> pricer = pathPricer ?? PathPricer();
            IPathGenerator<TPath> generator = PathGenerator();
            model = new MonteCarloModel<TPath, TStats>(
                generator, pricer, new TStats(), AntitheticVariate, controlPP, controlVariateValue, controlPG);
        }
        else
        {
            PathPricer<TPath> pricer = pathPricer ?? PathPricer();
            IPathGenerator<TPath> generator = PathGenerator();
            model = new MonteCarloModel<TPath, TStats>(generator, pricer, new TStats(), AntitheticVariate);
        }

        if (requiredTolerance is double tolerance)
        {
            Value(model, tolerance, maxSamples);
        }
        else
        {
            ValueWithSamples(model, requiredSamples!.Value);
        }

        return model;
    }

    /// <summary>The path pricer.</summary>
    /// <returns>A new path pricer.</returns>
    protected abstract PathPricer<TPath> PathPricer();

    /// <summary>The path generator.</summary>
    /// <returns>A new path generator.</returns>
    protected abstract IPathGenerator<TPath> PathGenerator();

    /// <summary>The time grid of the paths.</summary>
    /// <returns>The time grid.</returns>
    protected abstract TimeGrid TimeGrid();

    /// <summary>The control variate's path pricer; by default none.</summary>
    /// <returns>A new path pricer, or <see langword="null"/>.</returns>
    protected virtual PathPricer<TPath>? ControlPathPricer() => null;

    /// <summary>The control variate's path generator; by default none, the control priced on the model's paths.</summary>
    /// <returns>A new path generator, or <see langword="null"/>.</returns>
    protected virtual IPathGenerator<TPath>? ControlPathGenerator() => null;

    /// <summary>The control variate's value; by default none (QuantLib's <c>Null</c>).</summary>
    /// <returns>The value, or <see langword="null"/>.</returns>
    protected virtual double? ControlVariateValue() => null;

    /// <summary>
    /// The largest of the error estimates; of one, itself: QuantLib's <c>maxError(Real)</c>. A NaN is refused, where
    /// QuantLib's tolerance check would skip it.
    /// </summary>
    /// <param name="error">The error estimate.</param>
    /// <returns>The error estimate.</returns>
    /// <exception cref="NanoQLException"><paramref name="error"/> is NaN.</exception>
    protected static double MaxError(double error)
    {
        if (double.IsNaN(error))
        {
            throw new NanoQLException("error estimate is NaN");
        }

        return error;
    }
}
