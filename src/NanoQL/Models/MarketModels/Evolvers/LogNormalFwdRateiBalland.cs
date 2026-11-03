// SPDX-FileCopyrightText: Copyright (C) 2009 Sun Xiuxin
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/evolvers/lognormalfwdrateiballand.hpp ql/models/marketmodels/evolvers/lognormalfwdrateiballand.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// Balland's iterative predictor-corrector evolver of a displaced log-normal LIBOR market model under the terminal
/// measure: the forwards stepped from the last to the first, each drift from the later forwards' geometric means.
/// </summary>
/// <remarks>
/// <para>
/// A working type, copying each step's pseudo-root and covariance at construction; one step allocates nothing after a
/// path's warm-up, where QuantLib allocates its vector of drift terms at every step
/// (<c>lognormalfwdrateiballand.cpp:110</c>).
/// </para>
/// <para>
/// The geometric mean is of the displaced forwards at the step's start and end, √((f_end + d)(f_start + d)) − d, where
/// QuantLib's is √(f₀ f_end) of the undisplaced forwards with the model's time-zero forward at every step (fixed with
/// an independent check, <c>:119</c>, <c>:135</c>; <c>Divergences/BallandGeometricMeanTests.cs</c>). On one step from
/// time 0 undisplaced, QuantLib's case, it is QuantLib's arithmetic bit for bit.
/// </para>
/// </remarks>
[WorkingType]
public sealed class LogNormalFwdRateiBalland : MarketModelEvolver
{
    private readonly int initialStep;
    private readonly BrownianGenerator generator;
    private readonly double[][] fixedDrifts;
    private readonly Matrix[] pseudoRoots;
    private readonly Matrix[] covariances;
    private readonly int numberOfRates;
    private readonly LMMCurveState curveState;
    private readonly double[] forwards;
    private readonly double[] displacements;
    private readonly double[] logForwards;
    private readonly double[] initialLogForwards;
    private readonly double[] initialForwards;
    private readonly double[] startForwards;
    private readonly double[] initialDrifts;
    private readonly double[] g;
    private readonly double[] brownians;
    private readonly ImmutableArray<int> alive;
    private readonly ImmutableArray<double> rateTaus;
    private readonly LMMDriftCalculator[] calculators;
    private int currentStep;

    /// <summary>Initializes a new instance of the <see cref="LogNormalFwdRateiBalland"/> class.</summary>
    /// <param name="marketModel">The model evolved.</param>
    /// <param name="factory">The factory of the Brownian generator.</param>
    /// <param name="numeraires">The numeraires, one per step: the terminal measure's.</param>
    /// <param name="initialStep">The step each path starts from, below the number of steps.</param>
    /// <exception cref="ArgumentException">The numeraires aren't compatible with the evolution, or aren't the terminal
    /// measure's.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialStep"/> isn't a step.</exception>
    public LogNormalFwdRateiBalland(
        MarketModel marketModel, BrownianGeneratorFactory factory, ReadOnlySpan<int> numeraires, int initialStep = 0)
    {
        ArgumentNullException.ThrowIfNull(marketModel);
        ArgumentNullException.ThrowIfNull(factory);
        EvolutionDescription evolution = marketModel.Evolution;
        Numeraires = [.. numeraires];
        this.initialStep = initialStep;
        numberOfRates = marketModel.NumberOfRates;
        int numberOfFactors = marketModel.NumberOfFactors;
        curveState = new LMMCurveState(evolution.RateTimes.AsSpan());
        forwards = [.. marketModel.InitialRates];
        displacements = [.. marketModel.Displacements];
        logForwards = new double[numberOfRates];
        initialLogForwards = new double[numberOfRates];
        initialForwards = new double[numberOfRates];
        startForwards = new double[numberOfRates];
        initialDrifts = new double[numberOfRates];
        g = new double[numberOfRates];
        brownians = new double[numberOfFactors];
        alive = evolution.FirstAliveRate;
        rateTaus = evolution.RateTaus;

        EvolutionDescriptionFunctions.CheckCompatibility(evolution, numeraires);
        if (!EvolutionDescriptionFunctions.IsInTerminalMeasure(evolution, numeraires))
        {
            throw new ArgumentException("terminal measure required for iBalland ", nameof(numeraires));
        }

        int steps = evolution.NumberOfSteps;
        CheckInitialStep(initialStep, steps);
        generator = factory.Create(numberOfFactors, steps - initialStep);
        currentStep = initialStep;

        calculators = new LMMDriftCalculator[steps];
        fixedDrifts = new double[steps][];
        pseudoRoots = new Matrix[steps];
        covariances = new Matrix[steps];
        for (int j = 0; j < steps; ++j)
        {
            Matrix a = marketModel.PseudoRoot(j);
            pseudoRoots[j] = a;
            calculators[j] = new LMMDriftCalculator(a, displacements, rateTaus.AsSpan(), numeraires[j], alive[j]);
            covariances[j] = marketModel.Covariance(j);
            double[] fixedDrift = new double[numberOfRates];
            for (int k = 0; k < numberOfRates; ++k)
            {
                double variance = covariances[j][k, k];
                fixedDrift[k] = -0.5 * variance;
            }

            fixedDrifts[j] = fixedDrift;
        }

        SetForwards(marketModel.InitialRates.AsSpan());
    }

    /// <inheritdoc/>
    public override ImmutableArray<int> Numeraires { get; }

    /// <inheritdoc/>
    public override int CurrentStep => currentStep;

    /// <inheritdoc/>
    public override CurveState CurrentState => curveState;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The state's forwards aren't one per rate.</exception>
    public override void SetInitialState(CurveState cs)
    {
        ArgumentNullException.ThrowIfNull(cs);
        SetForwards(cs.ForwardRates());
    }

    /// <inheritdoc/>
    public override double StartNewPath()
    {
        currentStep = initialStep;
        initialLogForwards.CopyTo(logForwards, 0);
        return generator.NextPath();
    }

    /// <inheritdoc/>
    public override double AdvanceStep()
    {
        double weight = generator.NextStep(brownians);
        Matrix a = pseudoRoots[currentStep];
        Matrix c = covariances[currentStep];
        double[] fixedDrift = fixedDrifts[currentStep];

        int aliveRate = alive[currentStep];

        // the step's start forwards
        (currentStep > initialStep ? forwards : initialForwards).AsSpan(aliveRate).CopyTo(startForwards.AsSpan(aliveRate));

        int i = numberOfRates - 1;
        if (i >= aliveRate)
        {
            logForwards[i] += fixedDrift[i];
            logForwards[i] += Diffusion(a, i);
            forwards[i] = Math.Exp(logForwards[i]) - displacements[i];
            double blFwd = GeometricMean(i);
            g[i] = rateTaus[i] * (blFwd + displacements[i]) / (1.0 + (rateTaus[i] * blFwd));
        }

        for (i = numberOfRates - 2; i >= aliveRate; --i)
        {
            double drifts2 = 0.0;
            for (int j = i + 1; j < numberOfRates; ++j)
            {
                drifts2 -= g[j] * c[i, j];
            }

            logForwards[i] += drifts2 + fixedDrift[i];
            logForwards[i] += Diffusion(a, i);
            forwards[i] = Math.Exp(logForwards[i]) - displacements[i];

            double blFwd = GeometricMean(i);
            g[i] = rateTaus[i] * (blFwd + displacements[i]) / (1.0 + (rateTaus[i] * blFwd));
        }

        // update curve state
        curveState.SetOnForwardRates(forwards);

        ++currentStep;

        return weight;
    }

    private double Diffusion(Matrix a, int i)
    {
        ReadOnlySpan<double> row = a.Row(i);
        double sum = 0.0;
        for (int f = 0; f < row.Length; ++f)
        {
            sum += row[f] * brownians[f];
        }

        return sum;
    }

    // the geometric mean of the displaced forwards at the step's start and end, where QuantLib's is
    // sqrt(initialRates[i]*forwards_[i])
    private double GeometricMean(int i) =>
        Math.Sqrt((startForwards[i] + displacements[i]) * (forwards[i] + displacements[i])) - displacements[i];

    private void SetForwards(ReadOnlySpan<double> forwards)
    {
        if (!(forwards.Length == numberOfRates))
        {
            throw new ArgumentException("mismatch between forwards and rateTimes", nameof(forwards));
        }

        for (int i = 0; i < numberOfRates; ++i)
        {
            initialLogForwards[i] = Math.Log(forwards[i] + displacements[i]);
        }

        forwards.CopyTo(initialForwards);
        calculators[initialStep].Compute(forwards, initialDrifts);
    }
}
