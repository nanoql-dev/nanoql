// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/evolvers/lognormalcmswapratepc.hpp ql/models/marketmodels/evolvers/lognormalcmswapratepc.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The predictor-corrector evolver of a displaced log-normal constant-maturity swap market model: the model's rates
/// are the swap rates spanning a fixed number of forwards, each step predicted with the drifts at its start and
/// corrected with the average of those and the drifts on the predicted curve.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mm plan's MR1 (ii)), copying each step's pseudo-root at construction (MR1 (iv)); one step
/// allocates nothing after a path's warm-up (MR1 (v)).
/// </para>
/// <para>
/// <see cref="SetInitialState"/> reads any curve state's constant-maturity swap rates of the evolver's spanning,
/// through the base's virtual read, where QuantLib's casts the state to its own type and dereferences a null pointer
/// for any other (an access violation; the S-mm plan's MR3 (v), <c>lognormalcmswapratepc.cpp:93-94</c>;
/// <c>Divergences/SwapRatePcInitialStateTests.cs</c>).
/// </para>
/// </remarks>
public sealed class LogNormalCmSwapRatePc : MarketModelEvolver
{
    private readonly int spanningForwards;
    private readonly int initialStep;
    private readonly BrownianGenerator generator;
    private readonly double[][] fixedDrifts;
    private readonly Matrix[] pseudoRoots;
    private readonly int numberOfRates;
    private readonly CMSwapCurveState curveState;
    private readonly double[] swapRates;
    private readonly double[] displacements;
    private readonly double[] logSwapRates;
    private readonly double[] initialLogSwapRates;
    private readonly double[] drifts1;
    private readonly double[] drifts2;
    private readonly double[] initialDrifts;
    private readonly double[] brownians;
    private readonly ImmutableArray<int> alive;
    private readonly CMSMMDriftCalculator[] calculators;
    private int currentStep;

    /// <summary>Initializes a new instance of the <see cref="LogNormalCmSwapRatePc"/> class.</summary>
    /// <param name="spanningForwards">The forwards each swap spans, at least one.</param>
    /// <param name="marketModel">The model evolved, its rates the constant-maturity swap rates.</param>
    /// <param name="factory">The factory of the Brownian generator.</param>
    /// <param name="numeraires">The numeraires, one per step, compatible with the evolution.</param>
    /// <param name="initialStep">The step each path starts from, below the number of steps.</param>
    /// <exception cref="ArgumentException">The numeraires aren't compatible with the evolution.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="initialStep"/> isn't a step, or <paramref name="spanningForwards"/> is below 1.
    /// </exception>
    public LogNormalCmSwapRatePc(
        int spanningForwards, MarketModel marketModel, BrownianGeneratorFactory factory, ReadOnlySpan<int> numeraires, int initialStep = 0)
    {
        ArgumentNullException.ThrowIfNull(marketModel);
        ArgumentNullException.ThrowIfNull(factory);
        EvolutionDescription evolution = marketModel.Evolution;
        Numeraires = [.. numeraires];
        this.spanningForwards = spanningForwards;
        this.initialStep = initialStep;
        numberOfRates = marketModel.NumberOfRates;
        int numberOfFactors = marketModel.NumberOfFactors;
        curveState = new CMSwapCurveState(evolution.RateTimes.AsSpan(), spanningForwards);
        swapRates = [.. marketModel.InitialRates];
        displacements = [.. marketModel.Displacements];
        logSwapRates = new double[numberOfRates];
        initialLogSwapRates = new double[numberOfRates];
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

        calculators = new CMSMMDriftCalculator[steps];
        fixedDrifts = new double[steps][];
        pseudoRoots = new Matrix[steps];
        for (int j = 0; j < steps; ++j)
        {
            Matrix a = marketModel.PseudoRoot(j);
            pseudoRoots[j] = a;
            calculators[j] = new CMSMMDriftCalculator(
                a, displacements, evolution.RateTaus.AsSpan(), numeraires[j], alive[j], spanningForwards);
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

        SetCMSwapRates(marketModel.InitialRates.AsSpan());
    }

    /// <inheritdoc/>
    public override ImmutableArray<int> Numeraires { get; }

    /// <inheritdoc/>
    public override int CurrentStep => currentStep;

    /// <inheritdoc/>
    public override CurveState CurrentState => curveState;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The state's swap rates aren't one per rate.</exception>
    /// <remarks>Any curve state's swap rates of the evolver's spanning, where QuantLib's reads only its own type's (MR3
    /// (v)).</remarks>
    public override void SetInitialState(CurveState cs)
    {
        ArgumentNullException.ThrowIfNull(cs);
        SetCMSwapRates(cs.CmSwapRates(spanningForwards));
    }

    /// <inheritdoc/>
    public override double StartNewPath()
    {
        currentStep = initialStep;
        initialLogSwapRates.CopyTo(logSwapRates, 0);
        return generator.NextPath();
    }

    /// <inheritdoc/>
    public override double AdvanceStep()
    {
        // we're going from T1 to T2

        // a) compute drifts D1 at T1;
        if (currentStep > initialStep)
        {
            calculators[currentStep].Compute(curveState, drifts1);
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
        for (int i = aliveRate; i < numberOfRates; ++i)
        {
            logSwapRates[i] += drifts1[i] + fixedDrift[i];
            ReadOnlySpan<double> row = a.Row(i);
            double diffusion = 0.0;
            for (int f = 0; f < row.Length; ++f)
            {
                diffusion += row[f] * brownians[f];
            }

            logSwapRates[i] += diffusion;
            swapRates[i] = Math.Exp(logSwapRates[i]) - displacements[i];
        }

        // intermediate curve state update
        curveState.SetOnCMSwapRates(swapRates);

        // c) recompute drifts D2 using the predicted forwards;
        calculators[currentStep].Compute(curveState, drifts2);

        // d) correct forwards using both drifts
        for (int i = aliveRate; i < numberOfRates; ++i)
        {
            logSwapRates[i] += (drifts2[i] - drifts1[i]) / 2.0;
            swapRates[i] = Math.Exp(logSwapRates[i]) - displacements[i];
        }

        // e) update curve state
        curveState.SetOnCMSwapRates(swapRates);

        ++currentStep;

        return weight;
    }

    private void SetCMSwapRates(ReadOnlySpan<double> swapRates)
    {
        if (!(swapRates.Length == numberOfRates))
        {
            throw new ArgumentException("mismatch between swapRates and rateTimes", nameof(swapRates));
        }

        for (int i = 0; i < numberOfRates; ++i)
        {
            initialLogSwapRates[i] = Math.Log(swapRates[i] + displacements[i]);
        }

        curveState.SetOnCMSwapRates(swapRates);
        calculators[initialStep].Compute(curveState, initialDrifts);
    }
}
