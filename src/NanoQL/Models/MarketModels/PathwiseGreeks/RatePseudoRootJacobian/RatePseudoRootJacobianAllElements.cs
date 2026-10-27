// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.hpp ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The derivative of each end rate of a log-normal Euler step with respect to each element of its pseudo-root,
/// analytically, for the pathwise vegas: the step in the discretely compounding money-market account.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)): its scratch is QuantLib's members, so an instance serves one caller
/// at a time. It copies the pseudo-root.
/// </para>
/// <para>
/// <b>The derivative</b> (MMPR4 (iv), a documented divergence): <see cref="RatePseudoRootJacobian"/>'s, where QuantLib's
/// sums the drift's terms without their accrual periods and carries f_j, not f_j + d_j
/// (<c>ratepseudorootjacobian.cpp:280</c>, <c>:294</c>, <c>:298</c>).
/// </para>
/// </remarks>
public sealed class RatePseudoRootJacobianAllElements
{
    private readonly Matrix pseudoRoot;
    private readonly int aliveIndex;
    private readonly double[] taus;
    private readonly double[] displacements;
    private readonly int factors;

    // workspace
    private readonly Matrix e;
    private readonly double[] ratios;

    /// <summary>Initializes a new instance of the <see cref="RatePseudoRootJacobianAllElements"/> class.</summary>
    /// <param name="pseudoRoot">The step's pseudo-root, a row per rate; copied.</param>
    /// <param name="aliveIndex">The step's first alive rate.</param>
    /// <param name="numeraire">The step's numeraire: its first alive rate, the money-market account's.</param>
    /// <param name="taus">The rates' accrual periods.</param>
    /// <param name="displacements">The rates' displacements.</param>
    /// <exception cref="ArgumentException">
    /// The numeraire isn't the first alive rate, or a size isn't the rates'.
    /// </exception>
    public RatePseudoRootJacobianAllElements(
        Matrix pseudoRoot,
        int aliveIndex,
        int numeraire,
        ReadOnlySpan<double> taus,
        ReadOnlySpan<double> displacements)
    {
        ArgumentNullException.ThrowIfNull(pseudoRoot);
        this.pseudoRoot = pseudoRoot.Clone();
        this.aliveIndex = aliveIndex;
        this.taus = taus.ToArray();
        this.displacements = displacements.ToArray();
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
    }

    /// <summary>
    /// Writes, for each rate, the derivatives of its value at the step's end with respect to each pseudo-root element.
    /// </summary>
    /// <param name="oldRates">The rates at the step's start.</param>
    /// <param name="discountRatios">The one-step discount factors at the step's start, 1 / (1 + τ_k f_k) at k + 1,
    /// 1 + the number of rates entries.</param>
    /// <param name="newRates">The rates at the step's end.</param>
    /// <param name="gaussians">The step's Gaussians, one per factor.</param>
    /// <param name="b">One matrix per rate, of the rates by the factors, written.</param>
    /// <exception cref="ArgumentException"><paramref name="b"/>'s sizes aren't the rates' and the factors'.</exception>
    public void GetBumps(
        ReadOnlySpan<double> oldRates,
        ReadOnlySpan<double> discountRatios,
        ReadOnlySpan<double> newRates,
        ReadOnlySpan<double> gaussians,
        IReadOnlyList<Matrix> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        int numberRates = taus.Length;

        if (!(b.Count == numberRates))
        {
            throw new ArgumentException(
                $"we need B.size() which is {b.Count} to equal numberRates which is {numberRates}", nameof(b));
        }

        for (int r = 0; r < b.Count; ++r)
        {
            Matrix j = b[r];
            if (!(j.Columns == factors && j.Rows == numberRates))
            {
                throw new ArgumentException(
                    $"we need B[j].rows() which is {j.Rows} to equal numberRates which is {numberRates} and B[j].columns()"
                    + $" which is {j.Columns} to be equal to factors which is {factors}",
                    nameof(b));
            }
        }

        RatePseudoRootJacobianFunctions.Derivatives(
            pseudoRoot, aliveIndex, taus, displacements, oldRates, discountRatios, newRates, gaussians, e, ratios, b);
    }
}
