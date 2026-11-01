// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/driftcomputation/cmsmmdriftcalculator.hpp ql/models/marketmodels/driftcomputation/cmsmmdriftcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The drifts μΔt of a displaced log-normal constant-maturity swap market model's swap rates over a step, under the
/// step's numeraire: Mark Joshi, "Rapid Computation of Drifts in a Reduced Factor Libor Market Model", Wilmott Magazine,
/// May 2003.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mm plan's MR1 (ii)): QuantLib's mutable scratch is the calculator's own, so it is owned by one
/// computation and isn't thread-safe. The drifts' size is checked at every computation, QuantLib's extra-safety check
/// (SD8).
/// </para>
/// <para>
/// A spanning of no forwards is refused: QuantLib's drifts then read each swap's cross variation from its own
/// uninitialized entry, an undefined read (<c>Divergences/MarketModelUndefinedReadsTests.cs</c>).
/// </para>
/// </remarks>
[WorkingType]
public sealed class CMSMMDriftCalculator
{
    private readonly int numberOfRates;
    private readonly int numberOfFactors;
    private readonly int numeraire;
    private readonly int alive;
    private readonly double[] displacements;
    private readonly Matrix pseudo;
    private readonly Matrix pjPnWk;
    private readonly Matrix wkaj;
    private readonly Matrix wkajN;
    private readonly int spanningFwds;

    /// <summary>Initializes a new instance of the <see cref="CMSMMDriftCalculator"/> class.</summary>
    /// <param name="pseudo">The step's pseudo-root, rates by factors; copied.</param>
    /// <param name="displacements">The rates' displacements.</param>
    /// <param name="taus">The rates' accrual periods, at least one.</param>
    /// <param name="numeraire">The step's numeraire, from the first alive rate to the number of rates.</param>
    /// <param name="alive">The step's first alive rate, below the number of rates.</param>
    /// <param name="spanningFwds">The forwards each swap spans, at least one.</param>
    /// <exception cref="ArgumentException">The sizes don't match, or an index is outside its range.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The numeraire or the first alive rate is negative, or <paramref name="spanningFwds"/> is below 1.
    /// </exception>
    public CMSMMDriftCalculator(
        Matrix pseudo, ReadOnlySpan<double> displacements, ReadOnlySpan<double> taus, int numeraire, int alive, int spanningFwds)
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

        // QuantLib's drifts at no spanning forwards read uninitialized entries: refused.
        if (!(spanningFwds >= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(spanningFwds), "at least one spanning forward is required");
        }

        this.displacements = displacements.ToArray();
        this.pseudo = pseudo.Clone();
        pjPnWk = new Matrix(numberOfFactors, 1 + numberOfRates, 0.0);
        wkaj = new Matrix(numberOfFactors, numberOfRates, 0.0);
        wkajN = new Matrix(numberOfFactors, numberOfRates, 0.0);
        this.spanningFwds = spanningFwds;
    }

    /// <summary>The drifts of a curve state's constant-maturity swap rates.</summary>
    /// <param name="cs">The curve state.</param>
    /// <param name="drifts">The drifts written from the first alive rate on, one per rate.</param>
    /// <exception cref="ArgumentException">The drifts aren't one per rate of the state.</exception>
    public void Compute(CMSwapCurveState cs, Span<double> drifts)
    {
        ArgumentNullException.ThrowIfNull(cs);
        if (!(drifts.Length == cs.NumberOfRates))
        {
            throw new ArgumentException("drifts.size() <> numberOfRates", nameof(drifts));
        }

        ReadOnlySpan<double> taus = cs.RateTaus.AsSpan();

        // final bond is numeraire; compute cross variations
        for (int k = 0; k < pjPnWk.Rows; ++k)
        {
            pjPnWk[k, numberOfRates] = 0.0;
            wkaj[k, numberOfRates - 1] = 0.0;

            for (int j = numberOfRates - 2; j >= alive - 1; --j)
            {
                double sr = cs.CmSwapRate(j + 1, spanningFwds);
                int endIndex = (int)Math.Min((long)j + spanningFwds + 1, numberOfRates);
                double first = sr * wkaj[k, j + 1];
                double second = cs.CmSwapAnnuity(numberOfRates, j + 1, spanningFwds)
                    * (sr + displacements[j + 1])
                    * pseudo[j + 1, k];
                double third = pjPnWk[k, endIndex];
                pjPnWk[k, j + 1] = first + second + third;

                if (j >= alive)
                {
                    wkaj[k, j] = wkaj[k, j + 1] + (pjPnWk[k, j + 1] * taus[j]);

                    if ((long)j + spanningFwds + 1 <= numberOfRates)
                    {
                        wkaj[k, j] -= pjPnWk[k, endIndex] * taus[endIndex - 1];
                    }
                }
            }
        }

        double pnOverPN = cs.DiscountRatio(numberOfRates, numeraire);

        for (int j = alive; j < numberOfRates; ++j)
        {
            for (int k = 0; k < numberOfFactors; ++k)
            {
                wkajN[k, j] = (wkaj[k, j] * pnOverPN)
                    - (pjPnWk[k, numeraire] * pnOverPN * cs.CmSwapAnnuity(numeraire, j, spanningFwds));
            }
        }

        for (int j = alive; j < numberOfRates; ++j)
        {
            drifts[j] = 0.0;
            for (int k = 0; k < numberOfFactors; ++k)
            {
                drifts[j] += pseudo[j, k] * wkajN[k, j];
            }

            drifts[j] /= -cs.CmSwapAnnuity(numeraire, j, spanningFwds);
        }
    }
}
