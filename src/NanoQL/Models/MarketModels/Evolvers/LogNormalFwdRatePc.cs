// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/evolvers/lognormalfwdratepc.hpp ql/models/marketmodels/evolvers/lognormalfwdratepc.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The predictor-corrector evolver of a displaced log-normal LIBOR market model: each step's log forwards predicted
/// with the drifts at its start, then corrected with the average of those and the drifts at the predicted forwards.
/// </summary>
/// <remarks>
/// A working type (the S-mm plan's MR1 (ii)). It builds its Brownian generator from the factory it is given (HR3) and
/// copies each step's pseudo-root from the model at construction (MR1 (iv)), so a step reads nothing shared and one
/// model serves many evolvers on many threads. One step allocates nothing after a path's warm-up (MR1 (v);
/// <c>Conventions/MarketModelAllocationTests.cs</c>).
/// </remarks>
public sealed class LogNormalFwdRatePc : MarketModelEvolver
{
    private readonly int initialStep;
    private readonly BrownianGenerator generator;
    private readonly double[][] fixedDrifts;
    private readonly Matrix[] pseudoRoots;
    private readonly int numberOfRates;
    private readonly LMMCurveState curveState;
    private readonly double[] forwards;
    private readonly double[] displacements;
    private readonly double[] logForwards;
    private readonly double[] initialLogForwards;
    private readonly double[] drifts1;
    private readonly double[] drifts2;
    private readonly double[] initialDrifts;
    private readonly double[] brownians;
    private readonly ImmutableArray<int> alive;
    private readonly LMMDriftCalculator[] calculators;
    private int currentStep;

    /// <summary>Initializes a new instance of the <see cref="LogNormalFwdRatePc"/> class.</summary>
    /// <param name="marketModel">The model evolved.</param>
    /// <param name="factory">The factory of the Brownian generator.</param>
    /// <param name="numeraires">The numeraires, one per step, compatible with the evolution.</param>
    /// <param name="initialStep">The step each path starts from, below the number of steps.</param>
    /// <exception cref="ArgumentException">The numeraires aren't compatible with the evolution.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialStep"/> isn't a step.</exception>
    public LogNormalFwdRatePc(
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
        drifts1 = new double[numberOfRates];
        drifts2 = new double[numberOfRates];
        initialDrifts = new double[numberOfRates];
        brownians = new double[numberOfFactors];
        alive = evolution.FirstAliveRate;

        EvolutionDescriptionFunctions.CheckCompatibility(evolution, numeraires);

        int steps = evolution.NumberOfSteps;
        CheckInitialStep(initialStep, steps);
        generator = factory.Create(numberOfFactors, steps - initialStep);
        currentStep = initialStep;

        calculators = new LMMDriftCalculator[steps];
        fixedDrifts = new double[steps][];
        pseudoRoots = new Matrix[steps];
        for (int j = 0; j < steps; ++j)
        {
            Matrix a = marketModel.PseudoRoot(j);
            pseudoRoots[j] = a;
            calculators[j] = new LMMDriftCalculator(a, displacements, evolution.RateTaus.AsSpan(), numeraires[j], alive[j]);
            double[] fixedDrift = new double[numberOfRates];
            for (int k = 0; k < numberOfRates; ++k)
            {
                ReadOnlySpan<double> row = a.Row(k);
                double variance = 0.0;
                for (int f = 0; f < row.Length; ++f)
                {
                    variance += row[f] * row[f];
                }

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
        // we're going from T1 to T2

        // a) compute drifts D1 at T1;
        if (currentStep > initialStep)
        {
            calculators[currentStep].Compute(forwards, drifts1);
        }
        else
        {
            initialDrifts.CopyTo(drifts1, 0);
        }

        // b) evolve forwards up to T2 using D1;
        double weight = generator.NextStep(brownians);
        Matrix a = pseudoRoots[currentStep];
        double[] fixedDrift = fixedDrifts[currentStep];

        int aliveRate = alive[currentStep];
        for (int i = aliveRate; i < numberOfRates; i++)
        {
            logForwards[i] += drifts1[i] + fixedDrift[i];
            ReadOnlySpan<double> row = a.Row(i);
            double diffusion = 0.0;
            for (int f = 0; f < row.Length; ++f)
            {
                diffusion += row[f] * brownians[f];
            }

            logForwards[i] += diffusion;
            forwards[i] = Math.Exp(logForwards[i]) - displacements[i];
        }

        // c) recompute drifts D2 using the predicted forwards;
        calculators[currentStep].Compute(forwards, drifts2);

        // d) correct forwards using both drifts
        for (int i = aliveRate; i < numberOfRates; ++i)
        {
            logForwards[i] += (drifts2[i] - drifts1[i]) / 2.0;
            forwards[i] = Math.Exp(logForwards[i]) - displacements[i];
        }

        // e) update curve state
        curveState.SetOnForwardRates(forwards);

        ++currentStep;

        return weight;
    }

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

        calculators[initialStep].Compute(forwards, initialDrifts);
    }
}
