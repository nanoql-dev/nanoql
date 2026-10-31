// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/evolvers/lognormalfwdrateeulerconstrained.hpp ql/models/marketmodels/evolvers/lognormalfwdrateeulerconstrained.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The constrained Euler evolver of a displaced log-normal LIBOR market model, for the Fries-Joshi proxy simulation of
/// Greeks: at each step whose constraint is active, the log forwards are shifted along the constrained rate's
/// covariances so that it lands on its constraint, and the step's weight takes the likelihood ratio of the shift.
/// </summary>
/// <remarks>
/// <para>A working type (the S-mm plan's MR1 (ii)), copying each step's pseudo-root at construction (MR1 (iv)).</para>
/// <para>
/// Two departures from QuantLib (<c>Divergences/ConstrainedEulerCovarianceTests.cs</c>,
/// <c>…DisplacementTests.cs</c>): each step's covariances with its
/// constrained rate are its own pseudo-root's, where QuantLib builds every step's from the pseudo-root of the step
/// <see cref="SetConstraintType"/> is called at, so a later step's constraint misses (MR3 (vi);
/// <c>lognormalfwdrateeulerconstrained.cpp:117</c>); and the log constraint is displaced by the constrained rate's
/// displacement, where QuantLib's reads the step's index into the displacements (MR3 (vii); <c>:146</c>). A step
/// before <see cref="SetThisConstraint"/>, an active constraint before <see cref="SetConstraintType"/> and a
/// constrained rate past the rates, which QuantLib reads before or past its vectors, are refused (MR3 (xx)).
/// </para>
/// <para>
/// <b>The likelihood ratio</b> (the S-mmp plan's MMPR3, ruled by the user, a documented divergence;
/// <c>Divergences/ProxyGreekVegaTests.cs</c>): the evolver takes the reference model, the unbumped model whose
/// distribution the proxy simulation keeps, and a constrained step's weight carries the ratio of the two models'
/// transition densities of the constrained rate, which beside QuantLib's ratio of shifted to unshifted Gaussian densities
/// (<c>lognormalfwdrateeulerconstrained.cpp:200-212</c>) holds √(var_reference / var_own) of the rate's step variance.
/// QuantLib's weight misses that factor, right for a forward bump, whose variances are equal, and wrong for a volatility
/// bump: its proxy vegas take the wrong sign. Where the reference is the evolver's own model, QuantLib's weight bit for
/// bit.
/// </para>
/// </remarks>
public sealed class LogNormalFwdRateEulerConstrained : ConstrainedEvolver
{
    private readonly int initialStep;
    private readonly BrownianGenerator generator;
    private readonly double[][] fixedDrifts;
    private readonly double[][] variances;
    private readonly double[][] densityRatios;
    private readonly Matrix[] pseudoRoots;
    private readonly int numberOfRates;
    private readonly int numberOfFactors;
    private readonly LMMCurveState curveState;
    private readonly double[] forwards;
    private readonly double[] displacements;
    private readonly double[] logForwards;
    private readonly double[] initialLogForwards;
    private readonly double[] drifts1;
    private readonly double[] initialDrifts;
    private readonly double[] brownians;
    private readonly ImmutableArray<int> alive;
    private readonly LMMDriftCalculator[] calculators;
    private readonly CumulativeNormalDistribution phi = new();
    private int[] startIndexOfSwapRate = [];
    private double[][] covariances = [];
    private readonly double[] rateConstraints;
    private readonly bool[] isConstraintActive;
    private bool constraintTypeSet;
    private bool constraintSet;
    private int currentStep;

    /// <summary>Initializes a new instance of the <see cref="LogNormalFwdRateEulerConstrained"/> class.</summary>
    /// <param name="marketModel">The model evolved.</param>
    /// <param name="referenceModel">The reference model whose distribution the weights keep, of the model's rates and
    /// steps: the unbumped model of a proxy simulation, or <paramref name="marketModel"/> for QuantLib's weight.</param>
    /// <param name="factory">The factory of the Brownian generator.</param>
    /// <param name="numeraires">The numeraires, one per step, compatible with the evolution.</param>
    /// <param name="initialStep">The step each path starts from, below the number of steps.</param>
    /// <exception cref="ArgumentException">
    /// The numeraires aren't compatible with the evolution, or the reference model's rates or steps aren't the model's.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialStep"/> isn't a step.</exception>
    public LogNormalFwdRateEulerConstrained(
        MarketModel marketModel,
        MarketModel referenceModel,
        BrownianGeneratorFactory factory,
        ReadOnlySpan<int> numeraires,
        int initialStep = 0)
    {
        ArgumentNullException.ThrowIfNull(marketModel);
        ArgumentNullException.ThrowIfNull(referenceModel);
        ArgumentNullException.ThrowIfNull(factory);
        if (!(referenceModel.NumberOfRates == marketModel.NumberOfRates && referenceModel.NumberOfSteps == marketModel.NumberOfSteps))
        {
            throw new ArgumentException(
                $"the reference model's {referenceModel.NumberOfRates} rates and {referenceModel.NumberOfSteps} steps aren't"
                + $" the model's {marketModel.NumberOfRates} and {marketModel.NumberOfSteps}",
                nameof(referenceModel));
        }

        EvolutionDescription evolution = marketModel.Evolution;
        Numeraires = [.. numeraires];
        rateConstraints = new double[numeraires.Length];
        isConstraintActive = new bool[numeraires.Length];
        this.initialStep = initialStep;
        numberOfRates = marketModel.NumberOfRates;
        numberOfFactors = marketModel.NumberOfFactors;
        curveState = new LMMCurveState(evolution.RateTimes.AsSpan());
        forwards = [.. marketModel.InitialRates];
        displacements = [.. marketModel.Displacements];
        logForwards = new double[numberOfRates];
        initialLogForwards = new double[numberOfRates];
        drifts1 = new double[numberOfRates];
        initialDrifts = new double[numberOfRates];
        brownians = new double[numberOfFactors];
        alive = evolution.FirstAliveRate;

        EvolutionDescriptionFunctions.CheckCompatibility(evolution, numeraires);

        int steps = evolution.NumberOfSteps;
        CheckInitialStep(initialStep, steps);
        generator = factory.Create(numberOfFactors, steps - initialStep);
        currentStep = initialStep;

        calculators = new LMMDriftCalculator[steps];
        variances = new double[steps][];
        densityRatios = new double[steps][];
        fixedDrifts = new double[steps][];
        pseudoRoots = new Matrix[steps];
        for (int j = 0; j < steps; ++j)
        {
            Matrix a = marketModel.PseudoRoot(j);
            pseudoRoots[j] = a;
            calculators[j] = new LMMDriftCalculator(a, displacements, evolution.RateTaus.AsSpan(), numeraires[j], alive[j]);
            double[] fixedDrift = new double[numberOfRates];
            double[] variance = new double[numberOfRates];
            for (int k = 0; k < numberOfRates; ++k)
            {
                ReadOnlySpan<double> row = a.Row(k);
                double sum = 0.0;
                for (int f = 0; f < row.Length; ++f)
                {
                    sum += row[f] * row[f];
                }

                variance[k] = sum;
                fixedDrift[k] = -0.5 * sum;
            }

            variances[j] = variance;
            fixedDrifts[j] = fixedDrift;

            // the reference's step variance of each rate over the model's: 1 where the reference is the model (MMPR3)
            Matrix reference = ReferenceEquals(referenceModel, marketModel) ? a : referenceModel.PseudoRoot(j);
            double[] ratio = new double[numberOfRates];
            for (int k = 0; k < numberOfRates; ++k)
            {
                ReadOnlySpan<double> row = reference.Row(k);
                double sum = 0.0;
                for (int f = 0; f < row.Length; ++f)
                {
                    sum += row[f] * row[f];
                }

                ratio[k] = Math.Sqrt(sum / variance[k]);
            }

            densityRatios[j] = ratio;
        }

        SetForwards(marketModel.InitialRates.AsSpan());
    }

    /// <inheritdoc/>
    public override ImmutableArray<int> Numeraires { get; }

    /// <inheritdoc/>
    public override int CurrentStep => currentStep;

    /// <inheritdoc/>
    public override CurveState CurrentState => curveState;

    /// <summary>Sets, once, the swap rates each step constrains.</summary>
    /// <param name="startIndexOfSwapRate">Each step's constrained forward rate.</param>
    /// <param name="endIndexOfSwapRate">Each step's constrained rate plus one: only forward rates are
    /// constrained.</param>
    /// <exception cref="ArgumentException">
    /// The sizes aren't the steps', or a step's swap isn't a forward rate.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A constrained rate is outside the rates.</exception>
    /// <remarks>Each step's covariances are its own pseudo-root's (MR3 (vi)).</remarks>
    public override void SetConstraintType(ReadOnlySpan<int> startIndexOfSwapRate, ReadOnlySpan<int> endIndexOfSwapRate)
    {
        if (!(startIndexOfSwapRate.Length == Numeraires.Length))
        {
            throw new ArgumentException("Size mismatch in constraint specification.", nameof(startIndexOfSwapRate));
        }

        if (!(endIndexOfSwapRate.Length == Numeraires.Length))
        {
            throw new ArgumentException("Size mismatch in constraint specification.", nameof(endIndexOfSwapRate));
        }

        double[][] cov = new double[startIndexOfSwapRate.Length][];
        for (int i = 0; i < startIndexOfSwapRate.Length; ++i)
        {
            if (!(startIndexOfSwapRate[i] + 1 == endIndexOfSwapRate[i]))
            {
                throw new ArgumentException("constrained euler currently only implemented for forward rates", nameof(endIndexOfSwapRate));
            }

            // QuantLib reads the pseudo-root's row of the constrained rate, past it for a rate past the rates.
            if (!(startIndexOfSwapRate[i] >= 0 && startIndexOfSwapRate[i] < numberOfRates))
            {
                throw new ArgumentOutOfRangeException(nameof(startIndexOfSwapRate), "the constrained rate must be one of the rates");
            }

            // step i's own pseudo-root, where QuantLib's is the current step's (MR3 (vi))
            Matrix a = pseudoRoots[i];
            double[] c = new double[numberOfRates];
            for (int j = 0; j < numberOfRates; ++j)
            {
                double sum = 0.0;
                for (int k = 0; k < numberOfFactors; ++k)
                {
                    sum += a[startIndexOfSwapRate[i], k] * a[j, k];
                }

                c[j] = sum;
            }

            cov[i] = c;
        }

        this.startIndexOfSwapRate = startIndexOfSwapRate.ToArray();
        covariances = cov;
        constraintTypeSet = true;
    }

    /// <summary>Sets, before each path, the value each step constrains its swap rate to, and which steps are active.</summary>
    /// <param name="rateConstraints">Each step's constrained rate's target.</param>
    /// <param name="isConstraintActive">Whether each step's constraint is active.</param>
    /// <exception cref="ArgumentException">The sizes aren't the steps'.</exception>
    /// <remarks>
    /// The targets' logs are displaced by the constrained rates' displacements, at each step, where QuantLib's
    /// displaces each step's by the displacement of the rate whose index is the step's (MR3 (vii)).
    /// </remarks>
    public override void SetThisConstraint(ReadOnlySpan<double> rateConstraints, ReadOnlySpan<bool> isConstraintActive)
    {
        if (!(rateConstraints.Length == Numeraires.Length))
        {
            throw new ArgumentException("wrong number of constraints specified", nameof(rateConstraints));
        }

        if (!(isConstraintActive.Length == Numeraires.Length))
        {
            throw new ArgumentException("wrong number of isConstraintActive specified", nameof(isConstraintActive));
        }

        // QuantLib's one caller sets them before each path: copied into held buffers
        rateConstraints.CopyTo(this.rateConstraints);
        isConstraintActive.CopyTo(this.isConstraintActive);
        constraintSet = true;
    }

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
    /// <exception cref="NanoQLException">
    /// The constraints aren't set yet, or a step's constraint is active before the constraints' type is set.
    /// </exception>
    public override double AdvanceStep()
    {
        // QuantLib reads its activity flags, an empty valarray before setThisConstraint (MR3 (xx)).
        if (!constraintSet)
        {
            throw new NanoQLException("setThisConstraint must be called before advanceStep");
        }

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
        }

        // check constraint active
        if (isConstraintActive[currentStep])
        {
            if (!constraintTypeSet)
            {
                throw new NanoQLException("setConstraintType must be called before a constraint is active");
            }

            int index = startIndexOfSwapRate[currentStep];

            // compute error; the target's log displaced by its own rate's displacement (MR3 (vii))
            double requiredShift = Math.Log(rateConstraints[currentStep] + displacements[index]) - logForwards[index];

            double multiplier = requiredShift / variances[currentStep][index];

            // now shift each rate by multiplier * weighting of index rate across the step
            double[] c = covariances[currentStep];
            for (int i = aliveRate; i < numberOfRates; i++)
            {
                // we only need a small part of cov matrix
                logForwards[i] += multiplier * c[i];
            }

            // now we need to calculate the LR shift, we have moved Gaussian Z_k by multiplier * a_{index,k}; divide
            // original density by density of shifted normal
            double weightsEffect = 1.0;
            for (int k = 0; k < numberOfFactors; k++)
            {
                double shift = multiplier * a[index, k];
                double originalDensity = phi.Derivative(brownians[k] + shift);

                // the density of the draw after changes in original measure
                double newDensity = phi.Derivative(brownians[k]);

                // the density of the draw after changes in new measure, shifts cancel
                weightsEffect *= originalDensity / newDensity;
            }

            // the transition densities' ratio of the constrained rate, reference over own (MMPR3)
            weightsEffect *= densityRatios[currentStep][index];
            weight *= weightsEffect;
        }

        for (int i = aliveRate; i < numberOfRates; i++)
        {
            forwards[i] = Math.Exp(logForwards[i]) - displacements[i];
        }

        // c) update curve state
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
