// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.hpp ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The derivative of a log-normal Euler step's end rates along given bumps of its pseudo-root, analytically, for the
/// pathwise vegas (Giles and Glasserman's B, a row per bump): the step in the discretely compounding money-market
/// account.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)): its scratch is QuantLib's members, so an instance serves one caller
/// at a time. It copies the pseudo-root and the bumps.
/// </para>
/// <para>
/// <b>The derivative</b> (MMPR4 (iv), a documented divergence; <c>Divergences/RatePseudoRootJacobianTests.cs</c>): the
/// drift's sum carries each rate's own accrual period, e_j = Σ_{k&lt;j} τ_k (f_k + d_k) P_k a_k, and the derivative of
/// rate j along rate k's root carries f_j + d_j. QuantLib's sums the terms without τ_k and multiplies by τ_j, right on
/// equal periods only, and carries f_j (<c>ratepseudorootjacobian.cpp:183</c>, <c>:191</c>, <c>:197</c>). On equal
/// periods of a power of two and no displacement, QuantLib's bits.
/// </para>
/// </remarks>
[WorkingType]
public sealed class RatePseudoRootJacobian
{
    private readonly Matrix pseudoRoot;
    private readonly int aliveIndex;
    private readonly double[] taus;
    private readonly Matrix[] pseudoBumps;
    private readonly double[] displacements;
    private readonly int numberBumps;
    private readonly int factors;

    // workspace variables
    private readonly Matrix[] allDerivatives;
    private readonly Matrix e;
    private readonly double[] ratios;

    /// <summary>Initializes a new instance of the <see cref="RatePseudoRootJacobian"/> class.</summary>
    /// <param name="pseudoRoot">The step's pseudo-root, a row per rate; copied.</param>
    /// <param name="aliveIndex">The step's first alive rate.</param>
    /// <param name="numeraire">The step's numeraire: its first alive rate, the money-market account's.</param>
    /// <param name="taus">The rates' accrual periods.</param>
    /// <param name="pseudoBumps">The bumps, each of the pseudo-root's size; copied.</param>
    /// <param name="displacements">The rates' displacements.</param>
    /// <exception cref="ArgumentException">
    /// The numeraire isn't the first alive rate, or a size isn't the rates' or the factors'.
    /// </exception>
    public RatePseudoRootJacobian(
        Matrix pseudoRoot,
        int aliveIndex,
        int numeraire,
        ReadOnlySpan<double> taus,
        IReadOnlyList<Matrix> pseudoBumps,
        ReadOnlySpan<double> displacements)
    {
        ArgumentNullException.ThrowIfNull(pseudoRoot);
        ArgumentNullException.ThrowIfNull(pseudoBumps);
        this.pseudoRoot = pseudoRoot.Clone();
        this.aliveIndex = aliveIndex;
        this.taus = taus.ToArray();
        this.displacements = displacements.ToArray();
        numberBumps = pseudoBumps.Count;
        factors = pseudoRoot.Columns;
        e = new Matrix(pseudoRoot.Rows, pseudoRoot.Columns);
        ratios = new double[taus.Length];
        int numberRates = taus.Length;

        if (!(aliveIndex == numeraire))
        {
            throw new ArgumentException(
                "we can do only do discretely compounding MM acount so aliveIndex must equal numeraire", nameof(numeraire));
        }

        if (!(pseudoRoot.Rows == numberRates))
        {
            throw new ArgumentException("pseudoRoot_.rows()<> taus.size()", nameof(pseudoRoot));
        }

        if (!(displacements.Length == numberRates))
        {
            throw new ArgumentException("displacements_.size()<> taus.size()", nameof(displacements));
        }

        this.pseudoBumps = new Matrix[numberBumps];
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

            this.pseudoBumps[i] = pseudoBumps[i].Clone();
        }

        allDerivatives = new Matrix[numberRates];
        for (int i = 0; i < numberRates; ++i)
        {
            allDerivatives[i] = new Matrix(numberRates, factors);
        }
    }

    /// <summary>Writes, for each bump, the derivative of each rate at the step's end along the bump.</summary>
    /// <param name="oldRates">The rates at the step's start.</param>
    /// <param name="discountRatios">The one-step discount factors at the step's start, 1 / (1 + τ_k f_k) at k + 1,
    /// 1 + the number of rates entries.</param>
    /// <param name="newRates">The rates at the step's end.</param>
    /// <param name="gaussians">The step's Gaussians, one per factor.</param>
    /// <param name="b">The derivatives, a row per bump and a column per rate, written.</param>
    /// <exception cref="ArgumentException"><paramref name="b"/> isn't the bumps' by the rates.</exception>
    public void GetBumps(
        ReadOnlySpan<double> oldRates,
        ReadOnlySpan<double> discountRatios,
        ReadOnlySpan<double> newRates,
        ReadOnlySpan<double> gaussians,
        Matrix b)
    {
        ArgumentNullException.ThrowIfNull(b);
        int numberRates = taus.Length;

        if (!(b.Rows == numberBumps))
        {
            throw new ArgumentException(
                $"we need B.rows() which is {b.Rows} to equal numberBumps_ which is {numberBumps}", nameof(b));
        }

        if (!(b.Columns == numberRates))
        {
            throw new ArgumentException(
                $"we need B.columns() which is {b.Columns} to equal numberRates which is {numberRates}", nameof(b));
        }

        RatePseudoRootJacobianFunctions.Derivatives(
            pseudoRoot, aliveIndex, taus, displacements, oldRates, discountRatios, newRates, gaussians, e, ratios,
            allDerivatives);

        for (int i = 0; i < numberBumps; ++i)
        {
            int j = 0;

            for (; j < aliveIndex; ++j)
            {
                b[i, j] = 0.0;
            }

            for (; j < numberRates; ++j)
            {
                double sum = 0.0;

                for (int k = aliveIndex; k < numberRates; ++k)
                {
                    for (int f = 0; f < factors; ++f)
                    {
                        sum += pseudoBumps[i][k, f] * allDerivatives[j][k, f];
                    }
                }

                b[i, j] = sum;
            }
        }
    }
}
