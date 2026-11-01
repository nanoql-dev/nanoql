// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/driftcomputation/smmdriftcalculator.hpp ql/models/marketmodels/driftcomputation/smmdriftcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The drifts μΔt of a displaced log-normal coterminal swap market model's swap rates over a step, under the step's
/// numeraire: Mark Joshi and Lorenzo Liesch, "Effective Implementation Of Generic Market Models".
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mm plan's MR1 (ii)): QuantLib's mutable scratch is the calculator's own, its per-call vector
/// of annuities among it (MR1 (v)), so it is owned by one computation and isn't thread-safe. The drifts' size is
/// checked at every computation, QuantLib's extra-safety check (SD8).
/// </para>
/// <para>
/// QuantLib reads the curve state's annuities from the first rate, so a state set from the step's first alive rate
/// on, all the drifts read, is refused ("invalid index"); here they are read from the first alive rate, which gives
/// QuantLib's drifts bit for bit on a state set from the first rate (MR3 (xvi); <c>Divergences/SmmDriftAliveTests.cs</c>).
/// </para>
/// </remarks>
[WorkingType]
public sealed class SMMDriftCalculator
{
    private readonly int numberOfRates;
    private readonly int numberOfFactors;
    private readonly int numeraire;
    private readonly int alive;
    private readonly double[] displacements;
    private readonly Matrix pseudo;
    private readonly Matrix wkaj;
    private readonly Matrix wkpj;
    private readonly Matrix wkajshifted;
    private readonly double[] annuities;

    /// <summary>Initializes a new instance of the <see cref="SMMDriftCalculator"/> class.</summary>
    /// <param name="pseudo">The step's pseudo-root, rates by factors; copied.</param>
    /// <param name="displacements">The rates' displacements.</param>
    /// <param name="taus">The rates' accrual periods, at least one.</param>
    /// <param name="numeraire">The step's numeraire, from the first alive rate to the number of rates.</param>
    /// <param name="alive">The step's first alive rate, below the number of rates.</param>
    /// <exception cref="ArgumentException">The sizes don't match, or an index is outside its range.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The numeraire or the first alive rate is negative.</exception>
    public SMMDriftCalculator(Matrix pseudo, ReadOnlySpan<double> displacements, ReadOnlySpan<double> taus, int numeraire, int alive)
    {
        ArgumentNullException.ThrowIfNull(pseudo);
        ArgumentOutOfRangeException.ThrowIfNegative(numeraire);
        ArgumentOutOfRangeException.ThrowIfNegative(alive);
        numberOfRates = taus.Length;
        numberOfFactors = pseudo.Columns;
        this.numeraire = numeraire;
        this.alive = alive;

        // Check requirements
        if (!(numberOfRates > 0))
        {
            throw new ArgumentException("Dim out of range", nameof(taus));
        }

        if (!(displacements.Length == numberOfRates))
        {
            throw new ArgumentException("Displacements out of range", nameof(displacements));
        }

        if (!(pseudo.Rows == numberOfRates))
        {
            throw new ArgumentException("pseudo.rows() not consistent with dim", nameof(pseudo));
        }

        if (!(pseudo.Columns > 0 && pseudo.Columns <= numberOfRates))
        {
            throw new ArgumentException("pseudo.rows() not consistent with pseudo.columns()", nameof(pseudo));
        }

        if (!(alive < numberOfRates))
        {
            throw new ArgumentException("Alive out of bounds", nameof(alive));
        }

        if (!(numeraire <= numberOfRates))
        {
            throw new ArgumentException("Numeraire larger than dim", nameof(numeraire));
        }

        if (!(numeraire >= alive))
        {
            throw new ArgumentException("Numeraire smaller than alive", nameof(numeraire));
        }

        this.displacements = displacements.ToArray();
        this.pseudo = pseudo.Clone();

        // zero initialization required for (used by) the last element
        wkaj = new Matrix(pseudo.Columns, pseudo.Rows, 0.0);
        wkpj = new Matrix(pseudo.Columns, pseudo.Rows + 1, 0.0);
        wkajshifted = new Matrix(pseudo.Columns, pseudo.Rows, 0.0);
        annuities = new double[numberOfRates];
    }

    /// <summary>The drifts of a curve state's coterminal swap rates, with factor reduction.</summary>
    /// <param name="cs">The curve state, set from the step's first alive rate or before.</param>
    /// <param name="drifts">The drifts written from the first alive rate on, one per rate.</param>
    /// <exception cref="ArgumentException">The drifts aren't one per rate of the state.</exception>
    public void Compute(CoterminalSwapCurveState cs, Span<double> drifts)
    {
        ArgumentNullException.ThrowIfNull(cs);
        if (!(drifts.Length == cs.NumberOfRates))
        {
            throw new ArgumentException("drifts.size() <> numberOfRates", nameof(drifts));
        }

        // Compute drifts with factor reduction, using the pseudo square root of the covariance matrix.
        ReadOnlySpan<double> sr = cs.CoterminalSwapRates();

        // calculates and stores wkaj and wkpj, assuming terminal bond measure: eq 5.4-5.7
        ReadOnlySpan<double> taus = cs.RateTaus.AsSpan();
        for (int j = alive; j < numberOfRates; ++j)
        {
            annuities[j] = cs.CoterminalSwapAnnuity(numberOfRates, j);
        }

        for (int k = 0; k < numberOfFactors; ++k)
        {
            // wkpj[k][n] and wkaj[k][n-1] are 0, from the constructor
            for (int j = numberOfRates - 2; j >= alive - 1; --j)
            {
                // < W(k) | P(j+1)/P(n) > = SR(j+1) a(j+1,k) A(j+1) / P(n) + SR(j+1) < W(k) | A(j+1)/P(n) >
                double annuity = annuities[j + 1];
                wkpj[k, j + 1] = (sr[j + 1] * ((pseudo[j + 1, k] * annuity) + wkaj[k, j + 1]))
                    + (pseudo[j + 1, k] * displacements[j + 1] * annuity);

                if (j >= alive)
                {
                    wkaj[k, j] = (wkpj[k, j + 1] * taus[j]) + wkaj[k, j + 1];
                }
            }
        }

        double numeraireRatio = cs.DiscountRatio(numberOfRates, numeraire);

        // change to work for general numeraire
        for (int k = 0; k < numberOfFactors; ++k)
        {
            // compute < Wk, PN/pn>
            for (int j = alive; j < numberOfRates; ++j)
            {
                wkajshifted[k, j] = (-wkaj[k, j] / annuities[j]) + (wkpj[k, numeraire] * numeraireRatio);
            }
        }

        // eq 5.3 (in log coordinates)
        for (int j = alive; j < numberOfRates; ++j)
        {
            drifts[j] = 0.0;
            for (int k = 0; k < numberOfFactors; ++k)
            {
                drifts[j] += wkajshifted[k, j] * pseudo[j, k];
            }
        }
    }
}
