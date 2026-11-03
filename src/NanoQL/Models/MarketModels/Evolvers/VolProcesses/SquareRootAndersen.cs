// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/evolvers/volprocesses/squarerootandersen.hpp ql/models/marketmodels/evolvers/volprocesses/squarerootandersen.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// A square-root (CIR) variance process stepped by Andersen's quadratic-exponential scheme, sub-stepping each evolution
/// step, its step's standard deviation multiplier the weighted average of the variance at the sub-steps' ends.
/// </summary>
/// <remarks>
/// <para>A working type, with <see cref="Clone"/>.</para>
/// <para>
/// Three departures from QuantLib (<c>Divergences/SquareRootAndersenFirstStepTests.cs</c>, <c>…StepsTests.cs</c>,
/// <c>…SmallReversionTests.cs</c>): the first evolution step's sub-steps revert by e^{−κΔt}, where QuantLib leaves that
/// factor 0, reverting the variance to its mean in full over the first step (<c>squarerootandersen.cpp:51-52</c>);
/// <see cref="NumberSteps"/> is the evolution steps, where QuantLib's multiplies its sub-steps by the sub-steps again
/// (<c>:80</c>); and below |κΔt| = 1e-2 the conditional variance's (1 − e^{−κΔt})/κ is −expm1(−κΔt)/κ, Δt at κ = 0,
/// where QuantLib's cancels to 0 or NaN (<c>:96-97</c>), QuantLib's form and bits at and above the cut. A process with
/// no evolution times or a first one not positive, a step before a path starts or past the path's steps, and too few
/// variates, which QuantLib reads before or past its vectors, are refused.
/// </para>
/// <para>
/// Two more fixes (<c>Divergences/SquareRootAndersenDegenerateStepTests.cs</c>): where the conditional variance is 0
/// (no vol of variance), the variance and its mean both 0, or ψ so small that QuantLib's quadratic form overflows
/// (below about 1.5e-154), the sub-step's variance is its conditional mean, the form's limit, where QuantLib's is NaN
/// (<c>:102-103</c>); and the cut point picks each ψ's form inside its domain, clamped to [1, 2], where QuantLib's cut
/// below 1 sends ψ below 1 to the exponential form, whose p is then no probability, and a cut above 2 sends ψ above 2
/// to the quadratic form's square root of a negative (<c>:100</c>). QuantLib's bits elsewhere and at every cut in [1,
/// 2].
/// </para>
/// </remarks>
[WorkingType]
public sealed class SquareRootAndersen : MarketModelVolProcess
{
    // The cut below which the cancellation-free forms apply.
    private const double SmallReversion = 1e-2;

    private readonly double theta;
    private readonly double k;
    private readonly double epsilon;
    private readonly double v0;
    private readonly int numberSubSteps;
    private readonly int numberOfSteps;
    private readonly double[] dt;
    private readonly double[] eMinuskDt;
    private readonly double[] oneMinusEOverK;
    private readonly double w1;
    private readonly double w2;
    private readonly double psiC;
    private readonly double[] vPath;
    private readonly double[] state;
    private readonly CumulativeNormalDistribution phi;
    private double v;
    private int currentStep;
    private int subStep;
    private bool started;

    /// <summary>Initializes a new instance of the <see cref="SquareRootAndersen"/> class.</summary>
    /// <param name="meanLevel">The variance's mean level θ.</param>
    /// <param name="reversionSpeed">The reversion speed κ.</param>
    /// <param name="volVar">The volatility of the variance ε.</param>
    /// <param name="v0">The initial variance.</param>
    /// <param name="evolutionTimes">The evolution times, at least one, strictly increasing and positive.</param>
    /// <param name="numberSubSteps">The sub-steps of each evolution step.</param>
    /// <param name="w1">The weight of each sub-step's starting variance in the step's variance.</param>
    /// <param name="w2">The weight of each sub-step's ending variance.</param>
    /// <param name="cutPoint">The scheme's switch between its two forms, ψ_c.</param>
    /// <exception cref="ArgumentException">The evolution times are empty, or a step isn't of positive size.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberSubSteps"/> is negative.</exception>
    public SquareRootAndersen(
        double meanLevel,
        double reversionSpeed,
        double volVar,
        double v0,
        ReadOnlySpan<double> evolutionTimes,
        int numberSubSteps,
        double w1,
        double w2,
        double cutPoint = 1.5)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(numberSubSteps);
        if (evolutionTimes.IsEmpty)
        {
            throw new ArgumentException("at least one evolution time is required", nameof(evolutionTimes));
        }

        // QuantLib checks each later step's size; the first's is checked here.
        if (!(evolutionTimes[0] > 0.0))
        {
            throw new ArgumentException("Steps must be of positive size.", nameof(evolutionTimes));
        }

        theta = meanLevel;
        k = reversionSpeed;
        epsilon = volVar;
        this.v0 = v0;
        this.numberSubSteps = numberSubSteps;
        numberOfSteps = evolutionTimes.Length;
        this.w1 = w1;
        this.w2 = w2;
        // each form inside its domain: the quadratic for ψ ≤ 2, the exponential for ψ ≥ 1
        psiC = cutPoint < 1.0 ? 1.0 : cutPoint > 2.0 ? 2.0 : cutPoint;
        int subSteps = checked(evolutionTimes.Length * numberSubSteps);
        dt = new double[subSteps];
        eMinuskDt = new double[subSteps];
        oneMinusEOverK = new double[subSteps];
        vPath = new double[subSteps + 1];
        state = new double[1];
        phi = new CumulativeNormalDistribution();

        int j = 0;
        {
            double dt0 = evolutionTimes[0] / numberSubSteps;
            double ekdt0 = Math.Exp(-k * dt0);
            for (; j < numberSubSteps; ++j)
            {
                dt[j] = dt0;

                // QuantLib leaves the first step's factors 0.
                eMinuskDt[j] = ekdt0;
            }
        }

        for (int i = 1; i < evolutionTimes.Length; ++i)
        {
            double step = (evolutionTimes[i] - evolutionTimes[i - 1]) / numberSubSteps;
            double ekdt = Math.Exp(-k * step);
            if (!(step > 0.0))
            {
                throw new ArgumentException("Steps must be of positive size.", nameof(evolutionTimes));
            }

            for (int s = 0; s < numberSubSteps; ++s)
            {
                dt[j] = step;
                eMinuskDt[j] = ekdt;
                ++j;
            }
        }

        // below the cut, (1 − e^{−κΔt})/κ without its cancellation
        for (int s = 0; s < subSteps; ++s)
        {
            double x = k * dt[s];
            oneMinusEOverK[s] = Math.Abs(x) < SmallReversion
                ? (k == 0.0 ? dt[s] : -Expm1Functions.StdExpm1(-x) / k)
                : double.NaN;
        }

        vPath[0] = v0;
    }

    private SquareRootAndersen(SquareRootAndersen other)
    {
        theta = other.theta;
        k = other.k;
        epsilon = other.epsilon;
        v0 = other.v0;
        numberSubSteps = other.numberSubSteps;
        numberOfSteps = other.numberOfSteps;
        dt = (double[])other.dt.Clone();
        eMinuskDt = (double[])other.eMinuskDt.Clone();
        oneMinusEOverK = (double[])other.oneMinusEOverK.Clone();
        w1 = other.w1;
        w2 = other.w2;
        psiC = other.psiC;
        vPath = (double[])other.vPath.Clone();
        state = (double[])other.state.Clone();
        phi = other.phi;
        v = other.v;
        currentStep = other.currentStep;
        subStep = other.subStep;
        started = other.started;
    }

    /// <inheritdoc/>
    public override int VariatesPerStep => numberSubSteps;

    /// <inheritdoc/>
    /// <remarks>The evolution steps; QuantLib's multiplies its sub-steps by the sub-steps again.</remarks>
    public override int NumberSteps => numberOfSteps;

    /// <inheritdoc/>
    public override int NumberStateVariables => 1;

    /// <inheritdoc/>
    public override void NextPath()
    {
        v = v0;
        currentStep = 0;
        subStep = 0;
        started = true;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">No path is started, or the path's steps are taken.</exception>
    /// <exception cref="ArgumentException">There are fewer variates than the sub-steps.</exception>
    public override double Nextstep(ReadOnlySpan<double> variates)
    {
        // QuantLib's variance is uninitialized before a path, and it writes past its path's variances after the last
        // step.
        if (!started)
        {
            throw new NanoQLException("nextPath must be called before nextstep");
        }

        if (!(subStep + numberSubSteps <= dt.Length))
        {
            throw new NanoQLException("the path's steps are taken");
        }

        if (!(variates.Length >= numberSubSteps))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture, $"{variates.Length} variates given, {numberSubSteps} required"),
                nameof(variates));
        }

        for (int j = 0; j < numberSubSteps; ++j)
        {
            DoOneSubStep(ref v, variates[j], subStep);
            ++subStep;
            vPath[subStep] = v;
        }

        ++currentStep;
        return 1.0; // no importance sampling here
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">No step has been taken.</exception>
    public override double StepSd()
    {
        if (!(currentStep > 0))
        {
            throw new NanoQLException("nextStep must be called before stepSd");
        }

        double stepVariance = 0.0;
        int lastStepStart = (currentStep - 1) * numberSubSteps;
        for (int s = 0; s < numberSubSteps; ++s)
        {
            stepVariance += (w1 * vPath[s + lastStepStart]) + (w2 * vPath[s + lastStepStart + 1]);
        }

        stepVariance /= numberSubSteps;
        return Math.Sqrt(stepVariance);
    }

    /// <inheritdoc/>
    public override ReadOnlySpan<double> StateVariables()
    {
        state[0] = v;
        return state;
    }

    /// <inheritdoc/>
    public override MarketModelVolProcess Clone() => new SquareRootAndersen(this);

    private void DoOneSubStep(ref double vt, double z, int j)
    {
        double eminuskT = eMinuskDt[j];
        double m = theta + ((vt - theta) * eminuskT);
        double s2;
        double f = oneMinusEOverK[j];
        if (double.IsNaN(f))
        {
            s2 = (vt * epsilon * epsilon * eminuskT * (1 - eminuskT) / k)
                + (theta * epsilon * epsilon * (1 - eminuskT) * (1 - eminuskT) / (2 * k));
        }
        else
        {
            // (1 − e)/κ as f and (1 − e)²/(2κ) as (κf) f/2
            s2 = (vt * epsilon * epsilon * eminuskT * f) + (theta * epsilon * epsilon * (k * f) * f / 2.0);
        }

        // no conditional variance, ψ 0 or, with the mean 0 too, 0/0: the limit, as below; a negative or NaN s², an
        // invalid process's, stays QuantLib's NaN
        if (s2 == 0.0)
        {
            vt = m;
            return;
        }

        double s = Math.Sqrt(s2);
        double psi = s * s / (m * m);

        if (psi <= psiC)
        {
            double psiinv = 1.0 / psi;
            double product = 2 * psiinv * ((2 * psiinv) - 1.0);

            // ψ below about 1.5e-154 overflows QuantLib's form to NaN: its limit as ψ → 0, the conditional mean
            if (!double.IsFinite(product))
            {
                vt = m;
                return;
            }

            double b2 = (2.0 * psiinv) - 1 + Math.Sqrt(product);
            double b = Math.Sqrt(b2);
            double a = m / (1 + b2);
            vt = a * (b + z) * (b + z);
        }
        else
        {
            double p = (psi - 1.0) / (psi + 1.0);
            double beta = (1.0 - p) / m;
            double u = phi.Evaluate(z);

            if (u < p)
            {
                vt = 0;
                return;
            }

            vt = Math.Log((1.0 - p) / (1.0 - u)) / beta;
        }
    }
}
