// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.hpp ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The change in a log-normal Euler step's rates when its pseudo-root is bumped, by revaluing the step on each bumped
/// pseudo-root: the numerical form <see cref="RatePseudoRootJacobian"/> is tested against.
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (i)): its scratch is QuantLib's members, so an instance serves one caller
/// at a time.
/// </remarks>
[WorkingType]
public sealed class RatePseudoRootJacobianNumerical
{
    private readonly int aliveIndex;
    private readonly double[] displacements;
    private readonly int numberBumps;
    private readonly int factors;
    private readonly Matrix[] pseudoBumped;
    private readonly LMMDriftCalculator[] driftsComputers;

    // workspace variables
    private readonly double[] drifts;
    private readonly double[] bumpedRates;

    /// <summary>Initializes a new instance of the <see cref="RatePseudoRootJacobianNumerical"/> class.</summary>
    /// <param name="pseudoRoot">The step's pseudo-root, a row per rate.</param>
    /// <param name="aliveIndex">The step's first alive rate.</param>
    /// <param name="numeraire">The step's numeraire.</param>
    /// <param name="taus">The rates' accrual periods.</param>
    /// <param name="pseudoBumps">The bumps, each of the pseudo-root's size; added to copies of it.</param>
    /// <param name="displacements">The rates' displacements.</param>
    /// <exception cref="ArgumentException">A size isn't the rates' or the factors'.</exception>
    public RatePseudoRootJacobianNumerical(
        Matrix pseudoRoot,
        int aliveIndex,
        int numeraire,
        ReadOnlySpan<double> taus,
        IReadOnlyList<Matrix> pseudoBumps,
        ReadOnlySpan<double> displacements)
    {
        ArgumentNullException.ThrowIfNull(pseudoRoot);
        ArgumentNullException.ThrowIfNull(pseudoBumps);
        this.aliveIndex = aliveIndex;
        this.displacements = displacements.ToArray();
        numberBumps = pseudoBumps.Count;
        factors = pseudoRoot.Columns;
        int numberRates = taus.Length;
        drifts = new double[numberRates];
        bumpedRates = new double[numberRates];

        if (!(pseudoRoot.Rows == numberRates))
        {
            throw new ArgumentException("pseudoRoot_.rows()<> taus.size()", nameof(pseudoRoot));
        }

        if (!(displacements.Length == numberRates))
        {
            throw new ArgumentException("displacements_.size()<> taus.size()", nameof(displacements));
        }

        pseudoBumped = new Matrix[numberBumps];
        driftsComputers = new LMMDriftCalculator[numberBumps];
        for (int i = 0; i < numberBumps; ++i)
        {
            if (!(pseudoBumps[i].Rows == numberRates))
            {
                throw new ArgumentException($"pseudoBumps[i].rows()<> taus.size() with i ={i}", nameof(pseudoBumps));
            }

            if (!(pseudoBumps[i].Columns == factors))
            {
                throw new ArgumentException($"pseudoBumps[i].columns()<> factors with i = {i}", nameof(pseudoBumps));
            }

            Matrix pseudo = pseudoRoot.Clone();
            pseudo += pseudoBumps[i];
            pseudoBumped[i] = pseudo;
            driftsComputers[i] = new LMMDriftCalculator(pseudo, displacements, taus, numeraire, aliveIndex);
        }
    }

    /// <summary>
    /// Writes, for each bump, the change in each rate at the step's end when the step is revalued on the bumped
    /// pseudo-root from the same start rates and Gaussians.
    /// </summary>
    /// <param name="oldRates">The rates at the step's start.</param>
    /// <param name="oneStepDFs">Not read: the analytic form's one-step discount factors.</param>
    /// <param name="newRates">The rates at the step's end.</param>
    /// <param name="gaussians">The step's Gaussians, one per factor.</param>
    /// <param name="b">The changes, a row per bump and a column per rate, written.</param>
    /// <exception cref="ArgumentException"><paramref name="b"/> isn't the bumps' by the rates.</exception>
    public void GetBumps(
        ReadOnlySpan<double> oldRates,
        ReadOnlySpan<double> oneStepDFs,
        ReadOnlySpan<double> newRates,
        ReadOnlySpan<double> gaussians,
        Matrix b)
    {
        ArgumentNullException.ThrowIfNull(b);
        int numberRates = drifts.Length;

        if (!(b.Rows == numberBumps))
        {
            throw new ArgumentException("B.rows()<> numberBumps_", nameof(b));
        }

        if (!(b.Columns == numberRates))
        {
            throw new ArgumentException("B.columns()<> number of rates", nameof(b));
        }

        for (int i = 0; i < numberBumps; ++i)
        {
            Matrix pseudo = pseudoBumped[i];
            driftsComputers[i].Compute(oldRates, drifts);

            for (int j = 0; j < aliveIndex; ++j)
            {
                b[i, j] = 0.0;
            }

            for (int j = aliveIndex; j < numberRates; ++j)
            {
                bumpedRates[j] = Math.Log(oldRates[j] + displacements[j]);

                for (int k = 0; k < factors; ++k)
                {
                    bumpedRates[j] += -0.5 * pseudo[j, k] * pseudo[j, k];
                }

                bumpedRates[j] += drifts[j];

                for (int k = 0; k < factors; ++k)
                {
                    bumpedRates[j] += pseudo[j, k] * gaussians[k];
                }

                bumpedRates[j] = Math.Exp(bumpedRates[j]);
                bumpedRates[j] -= displacements[j];
                double tmp = bumpedRates[j] - newRates[j];

                b[i, j] = tmp;
            }
        }
    }
}
