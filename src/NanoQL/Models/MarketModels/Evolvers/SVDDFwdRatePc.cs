// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/evolvers/svddfwdratepc.hpp ql/models/marketmodels/evolvers/svddfwdratepc.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The predictor-corrector evolver of a displaced-diffusion LIBOR market model with an uncorrelated stochastic
/// volatility: each step's variance scaled by its volatility process's step multiplier, the process drawn from some of
/// each step's variates.
/// </summary>
/// <remarks>
/// <para>
/// A working type. It copies each step's pseudo-root at construction and owns a copy of its volatility process, so a
/// step reads nothing shared; one step allocates nothing after a path's warm-up.
/// </para>
/// <para>
/// QuantLib's design, kept: <c>volatilityFactorStep</c> isn't read, the process's variates spread evenly from the first
/// volatility factor by the constructor's own rule (<c>svddfwdratepc.cpp:85-92</c>), any spread a valid estimator. A
/// process of no variates per step, whose spread QuantLib divides by, is refused.
/// </para>
/// </remarks>
[WorkingType]
public sealed class SVDDFwdRatePc : MarketModelEvolver
{
    private readonly int initialStep;
    private readonly BrownianGenerator generator;
    private readonly MarketModelVolProcess volProcess;
    private readonly double[][] fixedDrifts;
    private readonly bool[] isVolVariate;
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
    private readonly double[] allBrownians;
    private readonly double[] brownians;
    private readonly double[] volBrownians;
    private readonly ImmutableArray<int> alive;
    private readonly LMMDriftCalculator[] calculators;
    private int currentStep;

    /// <summary>Initializes a new instance of the <see cref="SVDDFwdRatePc"/> class.</summary>
    /// <param name="marketModel">The model evolved.</param>
    /// <param name="factory">The factory of the Brownian generator.</param>
    /// <param name="volProcess">The volatility process; copied.</param>
    /// <param name="firstVolatilityFactor">The first of each step's variates the process draws.</param>
    /// <param name="volatilityFactorStep">Not read, as in QuantLib.</param>
    /// <param name="numeraires">The numeraires, one per step, compatible with the evolution.</param>
    /// <param name="initialStep">The step each path starts from: 0, as QuantLib supports.</param>
    /// <exception cref="ArgumentException">
    /// The initial step isn't 0, the numeraires aren't compatible with the evolution, or the process draws no variates.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A factor index is negative.</exception>
    public SVDDFwdRatePc(
        MarketModel marketModel,
        BrownianGeneratorFactory factory,
        MarketModelVolProcess volProcess,
        int firstVolatilityFactor,
        int volatilityFactorStep,
        ReadOnlySpan<int> numeraires,
        int initialStep = 0)
    {
        ArgumentNullException.ThrowIfNull(marketModel);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(volProcess);
        ArgumentOutOfRangeException.ThrowIfNegative(firstVolatilityFactor);
        ArgumentOutOfRangeException.ThrowIfNegative(volatilityFactorStep);
        EvolutionDescription evolution = marketModel.Evolution;
        this.volProcess = volProcess.Clone();
        int volFactorsPerStep = this.volProcess.VariatesPerStep;
        Numeraires = [.. numeraires];
        this.initialStep = initialStep;
        numberOfRates = marketModel.NumberOfRates;
        int numberOfFactors = marketModel.NumberOfFactors;
        isVolVariate = new bool[volFactorsPerStep + numberOfFactors];
        curveState = new LMMCurveState(evolution.RateTimes.AsSpan());
        forwards = [.. marketModel.InitialRates];
        displacements = [.. marketModel.Displacements];
        logForwards = new double[numberOfRates];
        initialLogForwards = new double[numberOfRates];
        drifts1 = new double[numberOfRates];
        drifts2 = new double[numberOfRates];
        initialDrifts = new double[numberOfRates];
        allBrownians = new double[volFactorsPerStep + numberOfFactors];
        brownians = new double[numberOfFactors];
        volBrownians = new double[volFactorsPerStep];
        alive = evolution.FirstAliveRate;

        if (!(initialStep == 0))
        {
            throw new ArgumentException("initial step zero only supported currently. ", nameof(initialStep));
        }

        EvolutionDescriptionFunctions.CheckCompatibility(evolution, numeraires);

        // QuantLib divides the variates' spread by the process's variates.
        if (!(volFactorsPerStep > 0))
        {
            throw new ArgumentException("the volatility process must draw at least one variate per step", nameof(volProcess));
        }

        int steps = evolution.NumberOfSteps;
        CheckInitialStep(initialStep, steps);
        generator = factory.Create(numberOfFactors + volFactorsPerStep, steps - initialStep);
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

        int variatesPerStep = numberOfFactors + volFactorsPerStep;

        // std::min(firstVolatilityFactor, variatesPerStep - volFactorsPerStep)
        int first = numberOfFactors < firstVolatilityFactor ? numberOfFactors : firstVolatilityFactor;
        int volIncrement = (variatesPerStep - first) / volFactorsPerStep;
        for (int i = 0; i < volFactorsPerStep; ++i)
        {
            isVolVariate[first + (i * volIncrement)] = true;
        }
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
        volProcess.NextPath();
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
        double weight = generator.NextStep(allBrownians);

        // divide Brownians between vol process and forward process
        for (int i = 0, j = 0, k = 0; i < allBrownians.Length; ++i)
        {
            if (isVolVariate[i])
            {
                volBrownians[j] = allBrownians[i];
                ++j;
            }
            else
            {
                brownians[k] = allBrownians[i];
                ++k;
            }
        }

        // get sd for step
        double weight2 = volProcess.Nextstep(volBrownians);
        double sdMultiplier = volProcess.StepSd();
        double varianceMultiplier = sdMultiplier * sdMultiplier;

        Matrix a = pseudoRoots[currentStep];
        double[] fixedDrift = fixedDrifts[currentStep];

        int aliveRate = alive[currentStep];
        for (int i = aliveRate; i < numberOfRates; ++i)
        {
            logForwards[i] += varianceMultiplier * (drifts1[i] + fixedDrift[i]);
            ReadOnlySpan<double> row = a.Row(i);
            double diffusion = 0.0;
            for (int f = 0; f < row.Length; ++f)
            {
                diffusion += row[f] * brownians[f];
            }

            logForwards[i] += sdMultiplier * diffusion;
            forwards[i] = Math.Exp(logForwards[i]) - displacements[i];
        }

        // c) recompute drifts D2 using the predicted forwards;
        calculators[currentStep].Compute(forwards, drifts2);

        // d) correct forwards using both drifts
        for (int i = aliveRate; i < numberOfRates; ++i)
        {
            logForwards[i] += varianceMultiplier * (drifts2[i] - drifts1[i]) / 2.0;
            forwards[i] = Math.Exp(logForwards[i]) - displacements[i];
        }

        // e) update curve state
        curveState.SetOnForwardRates(forwards);

        ++currentStep;

        return weight * weight2;
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
