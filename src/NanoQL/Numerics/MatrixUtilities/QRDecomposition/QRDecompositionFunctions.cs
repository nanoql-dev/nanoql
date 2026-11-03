// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/qrdecomposition.hpp ql/math/matrixutilities/qrdecomposition.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// The QR decomposition and the least-squares solve built on MINPACK's <c>qrfac</c> and <c>qrsolv</c>
/// (<see href="http://www.netlib.org/minpack"/>): Householder transformations with optional column pivoting.
/// </summary>
/// <remarks>
/// QuantLib fills the caller's matrices; here the decomposition returns them in a record. <see cref="QRSolve"/> on a
/// rank-deficient matrix is MINPACK's documented contract, as QuantLib's: <c>qrsolv</c> tests singularity by exact
/// zeros on R's diagonal, so a diagonal of rounding size gives a large solution.
/// </remarks>
public static class QRDecompositionFunctions
{
    /// <summary>
    /// The QR decomposition of <paramref name="m"/>: an orthogonal Q, a permutation P and an upper triangular R with
    /// A·P = Q·R.
    /// </summary>
    /// <param name="m">The m-by-n matrix A.</param>
    /// <param name="pivot">Whether to pivot the columns, ordering R's diagonal by nonincreasing magnitude.</param>
    /// <returns>Q (m by n), R (n by n) and the permutation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="m"/> is null.</exception>
    public static QRDecompositionResult QRDecomposition(Matrix m, bool pivot = true)
    {
        ArgumentNullException.ThrowIfNull(m);
        Matrix mT = MatrixFunctions.Transpose(m);
        int rows = m.Rows;
        int n = m.Columns;

        var lipvt = new int[n];
        var rdiag = new double[n];
        var wa = new double[n];

        // QuantLib passes rdiag as acnorm too, which qrfac reads only before writing rdiag
        LmdifFunctions.Qrfac(rows, n, mT.AsSpan(), 0, pivot ? 1 : 0, lipvt, n, rdiag, rdiag, wa);
        var r = new Matrix(n, n);

        for (int i = 0; i < n; ++i)
        {
            r[i, i] = rdiag[i];
            if (i < rows)
            {
                for (int k = i + 1; k < n; ++k)
                {
                    r[i, k] = mT[k, i];
                }
            }
        }

        var q = new Matrix(rows, n);

        if (rows > n)
        {
            int u = Math.Min(n, rows);
            for (int i = 0; i < u; ++i)
            {
                q[i, i] = 1.0;
            }

            var v = new double[rows];
            for (int i = u - 1; i >= 0; --i)
            {
                if (Math.Abs(mT[i, i]) > NumericLimits.QL_EPSILON)
                {
                    double tau = 1.0 / mT[i, i];

                    Array.Fill(v, 0.0, 0, i);
                    mT.Row(i)[i..].CopyTo(v.AsSpan(i));

                    var w = new double[n];
                    for (int l = 0; l < n; ++l)
                    {
                        double sum = 0.0;
                        for (int k = i; k < rows; ++k)
                        {
                            sum = sum + (v[k] * q[k, l]);
                        }

                        w[l] += sum;
                    }

                    for (int k = i; k < rows; ++k)
                    {
                        double a = tau * v[k];
                        for (int l = 0; l < n; ++l)
                        {
                            q[k, l] -= a * w[l];
                        }
                    }
                }
            }
        }
        else
        {
            var w = new double[rows];
            for (int k = 0; k < rows; ++k)
            {
                Array.Fill(w, 0.0);
                w[k] = 1.0;

                for (int j = 0; j < Math.Min(n, rows); ++j)
                {
                    double t3 = mT[j, j];
                    if (t3 != 0.0)
                    {
                        double sum = 0.0;
                        for (int i = j; i < rows; ++i)
                        {
                            sum = sum + (mT[j, i] * w[i]);
                        }

                        double t = sum / t3;
                        for (int i = j; i < rows; ++i)
                        {
                            w[i] -= mT[j, i] * t;
                        }
                    }

                    q[k, j] = w[j];
                }

                q.Row(k)[Math.Min(n, rows)..].Clear();
            }
        }

        ImmutableArray<int> ipvt;
        if (pivot)
        {
            ipvt = [.. lipvt];
        }
        else
        {
            var identity = new int[n];
            for (int i = 0; i < n; ++i)
            {
                identity[i] = i;
            }

            ipvt = [.. identity];
        }

        return new QRDecompositionResult(q, r, ipvt);
    }

    /// <summary>
    /// The least-squares solution x of A·x = b, D·x = 0, D the diagonal matrix of <paramref name="d"/>: MINPACK's
    /// <c>qrsolv</c> on A's QR decomposition.
    /// </summary>
    /// <param name="a">The m-by-n matrix A.</param>
    /// <param name="b">The vector b, of size m.</param>
    /// <param name="pivot">Whether the decomposition pivots A's columns.</param>
    /// <param name="d">D's diagonal, of size n, or <see langword="null"/> (QuantLib's empty array) for 0.</param>
    /// <returns>The solution x, of size n.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="a"/> or <paramref name="b"/> is null.</exception>
    /// <exception cref="ArgumentException">The sizes of A and b, or of A and d, don't match.</exception>
    public static Vector QRSolve(Matrix a, Vector b, bool pivot = true, Vector? d = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        int m = a.Rows;
        int n = a.Columns;

        if (!(b.Size == m))
        {
            throw new ArgumentException("dimensions of A and b don't match", nameof(b));
        }

        if (!(d is null || d.Size == n || d.Size == 0))
        {
            throw new ArgumentException("dimensions of A and d don't match", nameof(d));
        }

        QRDecompositionResult qr = QRDecomposition(a, pivot);

        var ipvt = new int[n];
        qr.Ipvt.CopyTo(ipvt);

        Matrix rT = MatrixFunctions.Transpose(qr.R);

        var sdiag = new double[n];
        var wa = new double[n];

        var ld = new double[n];
        if (d is not null && d.Size != 0)
        {
            d.AsSpan().CopyTo(ld);
        }

        var x = new Vector(n);
        Vector qtb = MatrixFunctions.Transpose(qr.Q) * b;

        LmdifFunctions.Qrsolv(n, rT.AsSpan(), n, ipvt, ld, qtb.AsSpan(), x.AsSpan(), sdiag, wa);

        return x;
    }
}
