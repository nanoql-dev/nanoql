// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/sparseilupreconditioner.hpp ql/math/matrixutilities/sparseilupreconditioner.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A preconditioner by the incomplete LU factorisation ILU(k) of a sparse matrix (Saad 1996, <i>Iterative Methods for
/// Sparse Linear Systems</i>): A ≈ L·U, the fill kept to the given level, applied by a forward and a backward solve.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: the factors are computed at construction; <see cref="L"/> and <see cref="U"/> return copies, and
/// <see cref="Apply"/> returns a new vector, the form of the Krylov solvers' preconditioner.
/// </para>
/// <para>
/// QuantLib records each new entry's fill level from the levels' nonzero entries by the entries' rank among the row's
/// nonzero values, which a value that cancels to 0 shifts, so a later row reads another entry's level; here each
/// entry's level is its own (a documented divergence; QuantLib's factors wherever no entry cancels). QuantLib's reads
/// of U also insert zero elements where none is stored, past its last column too, which changes U's structure but not
/// its values; here they insert nothing. A row that keeps no entry on or right of its diagonal, where QuantLib's
/// elimination loop never ends, is refused, and <see cref="Apply"/> refuses a vector not of the matrix's size, which
/// QuantLib reads unchecked.
/// </para>
/// </remarks>
public sealed class SparseILUPreconditioner
{
    private readonly SparseMatrix l;
    private readonly SparseMatrix u;
    private readonly int[] lBands;
    private readonly int[] uBands;

    /// <summary>Initializes a new instance of the <see cref="SparseILUPreconditioner"/> class.</summary>
    /// <param name="a">The square sparse matrix A.</param>
    /// <param name="lfil">The fill level k.</param>
    /// <exception cref="ArgumentNullException"><paramref name="a"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="a"/> isn't square, or a row keeps no entry on or right of its diagonal.
    /// </exception>
    public SparseILUPreconditioner(SparseMatrix a, int lfil = 1)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (!(a.Size1 == a.Size2))
        {
            throw new ArgumentException("sparse ILU preconditioner works only with square matrices", nameof(a));
        }

        l = new SparseMatrix(a.Size1, a.Size2);
        u = new SparseMatrix(a.Size1, a.Size2);

        for (int i = 0; i < l.Size1; ++i)
        {
            l[i, i] = 1.0;
        }

        int n = a.Size1;
        var lBandSet = new SortedSet<int>();
        var uBandSet = new SortedSet<int>();

        // the fill levels, by row: an absent entry is level 0, as uBLAS's
        var levs = new Dictionary<int, int>[n];
        for (int i = 0; i < n; ++i)
        {
            levs[i] = [];
        }

        int lfilp = lfil + 1;

        for (int ii = 0; ii < n; ++ii)
        {
            var w = new double[n];
            for (int k = 0; k < n; ++k)
            {
                w[k] = a[ii, k];
            }

            var levii = new int[n];
            for (int i = 0; i < n; ++i)
            {
                if (w[i] > NumericLimits.QL_EPSILON || w[i] < -1.0 * NumericLimits.QL_EPSILON)
                {
                    levii[i] = 1;
                }
            }

            int jj = -1;
            while (jj < ii)
            {
                bool found = false;
                for (int k = jj + 1; k < n; ++k)
                {
                    if (levii[k] != 0)
                    {
                        jj = k;
                        found = true;
                        break;
                    }
                }

                // QuantLib's loop runs on the same column forever where none is left
                if (!found)
                {
                    throw new ArgumentException(
                        $"row {ii} keeps no entry on or right of its diagonal at fill level {lfil}", nameof(a));
                }

                if (jj >= ii)
                {
                    break;
                }

                int jlev = levii[jj];
                if (jlev <= lfilp)
                {
                    var nonZeros = new List<int>(uBandSet.Count + 1);
                    var nonZeroEntries = new List<double>(uBandSet.Count + 1);
                    double entry = u[jj, jj];
                    if (entry > NumericLimits.QL_EPSILON || entry < -1.0 * NumericLimits.QL_EPSILON)
                    {
                        nonZeros.Add(jj);
                        nonZeroEntries.Add(entry);
                    }

                    foreach (int band in uBandSet)
                    {
                        // QuantLib's read past U's last column inserts a zero there and reads it
                        double bandEntry = jj + band < n ? u[jj, jj + band] : 0.0;
                        if (bandEntry > NumericLimits.QL_EPSILON || bandEntry < -1.0 * NumericLimits.QL_EPSILON)
                        {
                            nonZeros.Add(jj + band);
                            nonZeroEntries.Add(bandEntry);
                        }
                    }

                    double fact = w[jj];
                    if (nonZeroEntries.Count != 0)
                    {
                        fact /= nonZeroEntries[0];
                    }

                    for (int k = 0; k < nonZeros.Count; ++k)
                    {
                        int j = nonZeros[k];
                        int temp = levs[jj].GetValueOrDefault(j) + jlev;
                        if (levii[j] == 0)
                        {
                            if (temp <= lfilp)
                            {
                                w[j] = -fact * nonZeroEntries[k];
                                levii[j] = temp;
                            }
                        }
                        else
                        {
                            w[j] -= fact * nonZeroEntries[k];
                            levii[j] = Math.Min(levii[j], temp);
                        }
                    }

                    w[jj] = fact;
                }
            }

            for (int j = 0; j < n; ++j)
            {
                double entry = w[j];
                if (entry > NumericLimits.QL_EPSILON || entry < -1.0 * NumericLimits.QL_EPSILON)
                {
                    if (j < ii)
                    {
                        l[ii, j] = entry;
                        lBandSet.Add(ii - j);
                    }
                    else
                    {
                        u[ii, j] = entry;

                        // the entry's own level, where QuantLib reads the levels' nonzero entries by the values'
                        // rank
                        levs[ii][j] = levii[j];
                        if (j - ii > 0)
                        {
                            uBandSet.Add(j - ii);
                        }
                    }
                }
            }
        }

        lBands = [.. lBandSet];
        uBands = [.. uBandSet];
    }

    /// <summary>The lower factor L, its diagonal 1.</summary>
    /// <returns>A copy of L.</returns>
    public SparseMatrix L() => l.Clone();

    /// <summary>The upper factor U.</summary>
    /// <returns>A copy of U.</returns>
    public SparseMatrix U() => u.Clone();

    /// <summary>The preconditioner applied to <paramref name="b"/>: U⁻¹·L⁻¹·b.</summary>
    /// <param name="b">The vector, of the matrix's size, at least 1.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="b"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="b"/> isn't of the matrix's size, or is empty.</exception>
    public Vector Apply(Vector b)
    {
        ArgumentNullException.ThrowIfNull(b);
        if (!(b.Size == l.Size1 && b.Size > 0))
        {
            throw new ArgumentException(
                $"a vector of size {b.Size} for a {l.Size1} x {l.Size2} preconditioner", nameof(b));
        }

        return BackwardSolve(ForwardSolve(b));
    }

    private Vector ForwardSolve(Vector b)
    {
        int n = b.Size;
        var y = new Vector(n, 0.0);
        y[0] = b[0] / l[0, 0];
        for (int i = 1; i <= n - 1; ++i)
        {
            y[i] = b[i] / l[i, i];
            for (int j = lBands.Length - 1; j >= 0 && i - lBands[j] <= i - 1; --j)
            {
                int k = i - lBands[j];
                if (k >= 0)
                {
                    y[i] -= l[i, k] * y[k] / l[i, i];
                }
            }
        }

        return y;
    }

    private Vector BackwardSolve(Vector y)
    {
        int n = y.Size;
        var x = new Vector(n, 0.0);
        x[n - 1] = y[n - 1] / u[n - 1, n - 1];
        for (int i = n - 2; i >= 0; --i)
        {
            x[i] = y[i] / u[i, i];
            for (int j = 0; j < uBands.Length && i + uBands[j] <= n - 1; ++j)
            {
                x[i] -= u[i, i + uBands[j]] * x[i + uBands[j]] / u[i, i];
            }
        }

        return x;
    }
}
