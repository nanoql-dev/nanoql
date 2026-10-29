// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealing.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The hybrid simulated annealing: a sampler draws each new point, a probability accepts it, a temperature schedule
/// cools the dimensions and a reannealing rescales their steps, with a local optimizer at each new or best point:
/// QuantLib's <c>HybridSimulatedAnnealing</c>.
/// </summary>
/// <remarks>
/// <para>
/// The sampler provides a density, on the current point, for the parameters; the probability decides whether the new
/// point is accepted; the temperature is a schedule T(k) over the iteration k, which the sampler and the probability
/// read; the reannealing, a departure from Boltzmann annealing, rescales k for each dimension to improve convergence.
/// The hybrid is the local optimizer, run at every accepted point or at every new best point. A trial whose evaluation
/// throws is skipped, as QuantLib's <c>catch(...)</c> skips it. It stops at its iterations, after its stationary
/// iterations, or when every temperature is below the end temperature, then returning
/// <see cref="EndCriteriaType.None"/>, as QuantLib's does.
/// </para>
/// <para>
/// Immutable: it copies its functors at construction, and each minimization copies them again, so each starts from
/// their state at construction, where QuantLib's continues from the last minimization's.
/// </para>
/// </remarks>
/// <typeparam name="TSampler">The sampler.</typeparam>
/// <typeparam name="TProbability">The probability.</typeparam>
/// <typeparam name="TTemperature">The temperature schedule.</typeparam>
/// <typeparam name="TReannealing">The reannealing.</typeparam>
[Experimental("NQL0001")]
public class HybridSimulatedAnnealing<TSampler, TProbability, TTemperature, TReannealing> : OptimizationMethod
    where TSampler : IAnnealingSampler<TSampler>
    where TProbability : IAnnealingProbability<TProbability>
    where TTemperature : IAnnealingTemperature<TTemperature>
    where TReannealing : IAnnealingReannealing<TReannealing>
{
    private readonly TSampler sampler;
    private readonly TProbability probability;
    private readonly TTemperature temperature;
    private readonly TReannealing reannealing;
    private readonly double startTemperature;
    private readonly double endTemperature;
    private readonly int reAnnealSteps;
    private readonly HybridSimulatedAnnealingResetScheme resetScheme;
    private readonly int resetSteps;
    private readonly OptimizationMethod localOptimizer;
    private readonly HybridSimulatedAnnealingLocalOptimizeScheme optimizeScheme;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="HybridSimulatedAnnealing{TSampler, TProbability, TTemperature, TReannealing}"/> class.
    /// </summary>
    /// <param name="sampler">The sampler, which it copies.</param>
    /// <param name="probability">The probability, which it copies.</param>
    /// <param name="temperature">The temperature schedule, which it copies.</param>
    /// <param name="reannealing">The reannealing, which it copies; QuantLib's default <see cref="ReannealingTrivial"/>.</param>
    /// <param name="startTemperature">Each dimension's starting temperature.</param>
    /// <param name="endTemperature">The temperature every dimension must fall below to stop.</param>
    /// <param name="reAnnealSteps">The iterations between reannealings; 0 for never.</param>
    /// <param name="resetScheme">Where the current point resets.</param>
    /// <param name="resetSteps">The iterations between resets; 0 for never.</param>
    /// <param name="localOptimizer">
    /// The local optimizer; <see langword="null"/> for QuantLib's default, a <see cref="LevenbergMarquardt"/>. QuantLib's
    /// null optimizer, which turns local optimization off, is <paramref name="optimizeScheme"/>'s
    /// <see cref="HybridSimulatedAnnealingLocalOptimizeScheme.NoLocalOptimize"/> here.
    /// </param>
    /// <param name="optimizeScheme">When the local optimizer runs.</param>
    public HybridSimulatedAnnealing(
        TSampler sampler,
        TProbability probability,
        TTemperature temperature,
        TReannealing reannealing,
        double startTemperature = 200.0,
        double endTemperature = 0.01,
        int reAnnealSteps = 50,
        HybridSimulatedAnnealingResetScheme resetScheme = HybridSimulatedAnnealingResetScheme.ResetToBestPoint,
        int resetSteps = 150,
        OptimizationMethod? localOptimizer = null,
        HybridSimulatedAnnealingLocalOptimizeScheme optimizeScheme = HybridSimulatedAnnealingLocalOptimizeScheme.EveryBestPoint)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        ArgumentNullException.ThrowIfNull(probability);
        ArgumentNullException.ThrowIfNull(temperature);
        ArgumentNullException.ThrowIfNull(reannealing);
        ArgumentOutOfRangeException.ThrowIfNegative(reAnnealSteps);
        ArgumentOutOfRangeException.ThrowIfNegative(resetSteps);
        EnumArguments.Check(resetScheme, nameof(resetScheme));
        EnumArguments.Check(optimizeScheme, nameof(optimizeScheme));
        this.sampler = sampler.Clone();
        this.probability = probability.Clone();
        this.temperature = temperature.Clone();
        this.reannealing = reannealing.Clone();
        this.startTemperature = startTemperature;
        this.endTemperature = endTemperature;
        this.reAnnealSteps = reAnnealSteps == 0 ? int.MaxValue : reAnnealSteps;
        this.resetScheme = resetScheme;
        this.resetSteps = resetSteps == 0 ? int.MaxValue : resetSteps;
        this.localOptimizer = localOptimizer ?? new LevenbergMarquardt();
        this.optimizeScheme = optimizeScheme;
    }

    /// <inheritdoc/>
    protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria)
    {
        ArgumentNullException.ThrowIfNull(P);
        ArgumentNullException.ThrowIfNull(endCriteria);

        // this minimization's functors
        TSampler sampler = this.sampler.Clone();
        TProbability probability = this.probability.Clone();
        TTemperature temperature = this.temperature.Clone();
        TReannealing reannealing = this.reannealing.Clone();

        EndCriteriaType ecType = EndCriteriaType.None;
        P.Reset();
        reannealing.SetProblem(P);
        Vector x = P.CurrentValue.Clone();
        int n = x.Size;
        long k = 1;
        long kStationary = 1;
        long kReAnneal = 1;
        long kReset = 1;
        long maxK = endCriteria.MaxIterations;
        long maxKStationary = endCriteria.MaxStationaryStateIterations;
        bool temperatureBreached = false;
        var currentTemperature = new Vector(n, startTemperature);
        var annealStep = new Vector(n, 1.0);
        Vector bestPoint = x.Clone();
        Vector currentPoint = x.Clone();
        Vector startingPoint = x;
        Vector newPoint = x.Clone();
        double bestValue = P.Value(bestPoint);
        double currentValue = bestValue;

        // to reset to the starting point if desired
        double startingValue = bestValue;
        while (k <= maxK && kStationary <= maxKStationary && !temperatureBreached)
        {
            sampler.Sample(newPoint, currentPoint, currentTemperature);
            try
            {
                double newValue = P.Value(newPoint);
                if (probability.Accept(currentValue, newValue, currentTemperature))
                {
                    if (optimizeScheme == HybridSimulatedAnnealingLocalOptimizeScheme.EveryNewPoint)
                    {
                        P.CurrentValue = newPoint;
                        P.FunctionValue = newValue;
                        localOptimizer.Minimize(P, endCriteria);
                        newPoint = P.CurrentValue.Clone();
                        newValue = P.FunctionValue ?? double.NaN;
                    }

                    currentPoint = newPoint.Clone();
                    currentValue = newValue;
                }

                if (newValue < bestValue)
                {
                    if (optimizeScheme == HybridSimulatedAnnealingLocalOptimizeScheme.EveryBestPoint)
                    {
                        P.CurrentValue = newPoint;
                        P.FunctionValue = newValue;
                        localOptimizer.Minimize(P, endCriteria);
                        newPoint = P.CurrentValue.Clone();
                        newValue = P.FunctionValue ?? double.NaN;
                    }

                    kStationary = 0;
                    bestValue = newValue;
                    bestPoint = newPoint.Clone();
                }
            }
            catch (Exception e) when (e is NanoQLException or ArgumentException)
            {
                // QuantLib's catch(...): the trial is skipped
            }

            k++;
            kStationary++;
            for (int i = 0; i < n; i++)
            {
                annealStep[i]++;
            }

            if (kReAnneal == reAnnealSteps)
            {
                kReAnneal = 0;
                reannealing.Reanneal(annealStep, currentPoint, currentValue, currentTemperature);
            }

            kReAnneal++;
            if (kReset == resetSteps)
            {
                kReset = 0;
                switch (resetScheme)
                {
                    case HybridSimulatedAnnealingResetScheme.NoResetScheme:
                        break;
                    case HybridSimulatedAnnealingResetScheme.ResetToOrigin:
                        currentPoint = startingPoint.Clone();
                        currentValue = startingValue;
                        break;
                    case HybridSimulatedAnnealingResetScheme.ResetToBestPoint:
                        currentPoint = bestPoint.Clone();
                        currentValue = bestValue;
                        break;
                }
            }

            kReset++;
            temperature.Update(currentTemperature, currentTemperature, annealStep);
            temperatureBreached = currentTemperature.All(t => t < endTemperature);
        }

        if (k > maxK)
        {
            ecType = EndCriteriaType.MaxIterations;
        }
        else if (kStationary > maxKStationary)
        {
            ecType = EndCriteriaType.StationaryPoint;
        }

        P.CurrentValue = bestPoint;
        P.FunctionValue = bestValue;
        return ecType;
    }
}
