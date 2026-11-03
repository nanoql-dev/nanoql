// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/driftcomputation/lmmnormaldriftcalculator.hpp ql/models/marketmodels/driftcomputation/lmmnormaldriftcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The drifts μΔt of a normal LIBOR market model's forwards over a step, under the step's numeraire: Mark Joshi,
/// "Rapid Computation of Drifts in a Reduced Factor Libor Market Model", Wilmott Magazine, May 2003, modified for normal
/// forward-rate dynamics.
/// </summary>
/// <remarks>
/// A working type: QuantLib's mutable scratch is the calculator's own, so it is owned by one computation and isn't
/// thread-safe; one evolver builds one per step. The forwards' and the drifts' sizes are checked at every computation,
/// QuantLib's extra-safety checks, the plain and reduced forms included.
/// </remarks>
[WorkingType]
public sealed class LMMNormalDriftCalculator
{
    private readonly int numberOfRates;
    private readonly int numberOfFactors;
    private readonly bool isFullFactor;
    private readonly int numeraire;
    private readonly int alive;
    private readonly double[] oneOverTaus;
    private readonly Matrix c;
    private readonly Matrix pseudo;
    private readonly double[] tmp;
    private readonly Matrix e;
    private readonly int[] downs;
    private readonly int[] ups;

    /// <summary>Initializes a new instance of the <see cref="LMMNormalDriftCalculator"/> class.</summary>
    /// <param name="pseudo">The step's pseudo-root, rates by factors; copied.</param>
    /// <param name="taus">The rates' accrual periods, at least one.</param>
    /// <param name="numeraire">The step's numeraire, from the first alive rate to the number of rates.</param>
    /// <param name="alive">The step's first alive rate, below the number of rates.</param>
    /// <exception cref="ArgumentException">The sizes don't match, or an index is outside its range.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The numeraire or the first alive rate is negative.</exception>
    public LMMNormalDriftCalculator(Matrix pseudo, ReadOnlySpan<double> taus, int numeraire, int alive)
    {
        ArgumentNullException.ThrowIfNull(pseudo);
        ArgumentOutOfRangeException.ThrowIfNegative(numeraire);
        ArgumentOutOfRangeException.ThrowIfNegative(alive);
        numberOfRates = taus.Length;
        numberOfFactors = pseudo.Columns;
        isFullFactor = numberOfFactors == numberOfRates;
        this.numeraire = numeraire;
        this.alive = alive;

        // Check requirements
        if (!(numberOfRates > 0))
        {
            throw new ArgumentException("Dim out of range", nameof(taus));
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

        this.pseudo = pseudo.Clone();
        tmp = new double[numberOfRates];
        e = new Matrix(pseudo.Columns, pseudo.Rows, 0.0);
        downs = new int[numberOfRates];
        ups = new int[numberOfRates];

        // Precompute 1/taus
        oneOverTaus = new double[numberOfRates];
        for (int i = 0; i < numberOfRates; ++i)
        {
            oneOverTaus[i] = 1.0 / taus[i];
        }

        // Compute covariance matrix from pseudoroot
        c = this.pseudo * MatrixFunctions.Transpose(this.pseudo);

        // Compute lower and upper extrema for (non reduced) drift calculation
        for (int i = alive; i < numberOfRates; ++i)
        {
            downs[i] = Math.Min(i + 1, numeraire);
            ups[i] = Math.Max(i + 1, numeraire);
        }
    }

    /// <summary>The drifts of a curve state's forwards: the plain form at full factors, the reduced one otherwise.</summary>
    /// <param name="cs">The curve state.</param>
    /// <param name="drifts">The drifts written from the first alive rate on, one per rate.</param>
    /// <exception cref="ArgumentException">The sizes aren't the number of rates.</exception>
    public void Compute(LMMCurveState cs, Span<double> drifts)
    {
        ArgumentNullException.ThrowIfNull(cs);
        Compute(cs.ForwardRates(), drifts);
    }

    /// <summary>The drifts of forwards: the plain form at full factors, the reduced one otherwise.</summary>
    /// <param name="fwds">The forwards, one per rate.</param>
    /// <param name="drifts">The drifts written from the first alive rate on, one per rate.</param>
    /// <exception cref="ArgumentException">The sizes aren't the number of rates.</exception>
    public void Compute(ReadOnlySpan<double> fwds, Span<double> drifts)
    {
        if (isFullFactor)
        {
            ComputePlain(fwds, drifts);
        }
        else
        {
            ComputeReduced(fwds, drifts);
        }
    }

    /// <summary>The drifts of a curve state's forwards without factor reduction (eqs. 2 and 4 of the reference, for normal dynamics).</summary>
    /// <param name="cs">The curve state.</param>
    /// <param name="drifts">The drifts written from the first alive rate on, one per rate.</param>
    /// <exception cref="ArgumentException">The sizes aren't the number of rates.</exception>
    public void ComputePlain(LMMCurveState cs, Span<double> drifts)
    {
        ArgumentNullException.ThrowIfNull(cs);
        ComputePlain(cs.ForwardRates(), drifts);
    }

    /// <summary>The drifts of forwards without factor reduction (eqs. 2 and 4 of the reference, for normal dynamics), on the covariance.</summary>
    /// <param name="fwds">The forwards, one per rate.</param>
    /// <param name="drifts">The drifts written from the first alive rate on, one per rate.</param>
    /// <exception cref="ArgumentException">The sizes aren't the number of rates.</exception>
    public void ComputePlain(ReadOnlySpan<double> fwds, Span<double> drifts)
    {
        CheckSizes(fwds, drifts);
        int i;
        for (i = alive; i < numberOfRates; ++i)
        {
            tmp[i] = 1.0 / (oneOverTaus[i] + fwds[i]);
        }

        for (i = alive; i < numberOfRates; ++i)
        {
            ReadOnlySpan<double> row = c.Row(i);
            double sum = 0.0;
            for (int k = downs[i]; k < ups[i]; ++k)
            {
                sum += tmp[k] * row[k];
            }

            drifts[i] = sum;
            if (numeraire > i + 1)
            {
                drifts[i] = -drifts[i];
            }
        }
    }

    /// <summary>The drifts of a curve state's forwards with factor reduction (eq. 7 of the reference, for normal dynamics).</summary>
    /// <param name="cs">The curve state.</param>
    /// <param name="drifts">The drifts written from the first alive rate on, one per rate.</param>
    /// <exception cref="ArgumentException">The sizes aren't the number of rates.</exception>
    public void ComputeReduced(LMMCurveState cs, Span<double> drifts)
    {
        ArgumentNullException.ThrowIfNull(cs);
        ComputeReduced(cs.ForwardRates(), drifts);
    }

    /// <summary>The drifts of forwards with factor reduction (eq. 7 of the reference, for normal dynamics), on the pseudo-root.</summary>
    /// <param name="fwds">The forwards, one per rate.</param>
    /// <param name="drifts">The drifts written from the first alive rate on, one per rate.</param>
    /// <exception cref="ArgumentException">The sizes aren't the number of rates.</exception>
    public void ComputeReduced(ReadOnlySpan<double> fwds, Span<double> drifts)
    {
        CheckSizes(fwds, drifts);
        for (int i = alive; i < numberOfRates; ++i)
        {
            tmp[i] = 1.0 / (oneOverTaus[i] + fwds[i]);
        }

        // Initialize the e matrix at the numeraire's column.
        for (int r = 0; r < numberOfFactors; ++r)
        {
            e[r, Math.Max(0, numeraire - 1)] = 0.0;
        }

        // Now compute the drifts: the numeraire's own forward's is 0...
        if (numeraire > 0)
        {
            drifts[numeraire - 1] = 0.0;
        }

        // ...those below it from the numeraire downwards...
        for (int i = numeraire - 2; i >= alive; --i)
        {
            drifts[i] = 0.0;
            for (int r = 0; r < numberOfFactors; ++r)
            {
                e[r, i] = e[r, i + 1] + (tmp[i + 1] * pseudo[i + 1, r]);
                drifts[i] -= e[r, i] * pseudo[i, r];
            }
        }

        // ...and those above it from the numeraire upwards.
        for (int i = numeraire; i < numberOfRates; ++i)
        {
            drifts[i] = 0.0;
            for (int r = 0; r < numberOfFactors; ++r)
            {
                if (i == 0)
                {
                    e[r, i] = tmp[i] * pseudo[i, r];
                }
                else
                {
                    e[r, i] = e[r, i - 1] + (tmp[i] * pseudo[i, r]);
                }

                drifts[i] += e[r, i] * pseudo[i, r];
            }
        }
    }

    private void CheckSizes(ReadOnlySpan<double> fwds, Span<double> drifts)
    {
        if (!(fwds.Length == numberOfRates))
        {
            throw new ArgumentException("numberOfRates <> dim", nameof(fwds));
        }

        if (!(drifts.Length == numberOfRates))
        {
            throw new ArgumentException("drifts.size() <> dim", nameof(drifts));
        }
    }
}
