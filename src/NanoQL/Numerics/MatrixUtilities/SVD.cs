// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/svd.hpp ql/math/matrixutilities/svd.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.MatrixFunctions;

namespace NanoQL.Numerics;

/// <summary>The singular value decomposition of a real matrix, M = U·S·Vᵀ.</summary>
/// <remarks>
/// <para>
/// Refer to Golub and Van Loan, "Matrix computation", The Johns Hopkins University Press. QuantLib's implementation
/// is adapted from the TNT project (JAMA), developed at the National Institute of Standards and Technology and in the
/// public domain. It requires at least as many rows as columns; a matrix with fewer rows is decomposed transposed, and
/// its U and V swapped, since M = (U·S·Vᵀ)ᵀ = V·S·Uᵀ.
/// </para>
/// <para>
/// The decomposition is computed in the constructor; <see cref="U"/>, <see cref="V"/>, <see cref="SingularValues"/>
/// and <see cref="S"/> return copies.
/// </para>
/// <para>
/// A matrix with a NaN or infinite entry is refused on entry, where QuantLib's main loop, which has no test for too
/// many iterations, never ends; and so is a finite matrix whose decomposition leaves the range of doubles, a singular
/// value beyond it (entries near 1e307), refused at the main loop's first non-finite singular value or superdiagonal
/// entry, where QuantLib's loop never ends either. Documented divergences
/// (<c>Divergences/SvdNonFiniteRefusalTests.cs</c>). Every matrix QuantLib decomposes keeps its arithmetic.
/// </para>
/// </remarks>
public sealed class SVD
{
    private readonly Matrix u;
    private readonly Matrix v;
    private readonly Vector s;
    private readonly int m;
    private readonly int n;
    private readonly bool transpose;

    /// <summary>Initializes a new instance of the <see cref="SVD"/> class: decomposes a matrix.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <exception cref="ArgumentNullException"><paramref name="matrix"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// An entry of <paramref name="matrix"/> is NaN or infinite, or the decomposition leaves the range of doubles
    /// (divergences: QuantLib's loop never ends).
    /// </exception>
    public SVD(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        for (int r = 0; r < matrix.Rows; r++)
        {
            for (int c = 0; c < matrix.Columns; c++)
            {
                if (!double.IsFinite(matrix[r, c]))
                {
                    throw new ArgumentException(
                        $"matrix entry ({r}, {c}) is {matrix[r, c]}: the singular value decomposition of a non-finite "
                        + "matrix is refused, since its iteration never ends",
                        nameof(matrix));
                }
            }
        }

        Matrix a;

        /* The implementation requires that rows > columns.
           If this is not the case, we decompose M^T instead.
           Swapping the resulting U and V gives the desired
           result for M as

           M^T = U S V^T           (decomposition of M^T)

           M = (U S V^T)^T         (transpose)

           M = (V^T^T S^T U^T)     ((AB)^T = B^T A^T)

           M = V S U^T             (idempotence of transposition,
                                    symmetry of diagonal matrix S)
        */

        if (matrix.Rows >= matrix.Columns)
        {
            a = matrix.Clone();
            transpose = false;
        }
        else
        {
            a = Transpose(matrix);
            transpose = true;
        }

        m = a.Rows;
        n = a.Columns;

        // we're sure that m_ >= n_

        s = new Vector(n);
        u = new Matrix(m, n, 0.0);
        v = new Matrix(n, n);
        double[] e = new double[n];
        double[] work = new double[m];
        int i, j, k;

        // Reduce A to bidiagonal form, storing the diagonal elements
        // in s and the super-diagonal elements in e.

        int nct = Math.Min(m - 1, n);
        int nrt = Math.Max(0, n - 2);
        for (k = 0; k < Math.Max(nct, nrt); k++)
        {
            if (k < nct)
            {
                // Compute the transformation for the k-th column and
                // place the k-th diagonal in s[k].
                // Compute 2-norm of k-th column without under/overflow.
                s[k] = 0;
                for (i = k; i < m; i++)
                {
                    s[k] = Hypot(s[k], a[i, k]);
                }

                if (s[k] != 0.0)
                {
                    if (a[k, k] < 0.0)
                    {
                        s[k] = -s[k];
                    }

                    for (i = k; i < m; i++)
                    {
                        a[i, k] /= s[k];
                    }

                    a[k, k] += 1.0;
                }

                s[k] = -s[k];
            }

            for (j = k + 1; j < n; j++)
            {
                if ((k < nct) && (s[k] != 0.0))
                {
                    // Apply the transformation.

                    double t = 0;
                    for (i = k; i < m; i++)
                    {
                        t += a[i, k] * a[i, j];
                    }

                    t = -t / a[k, k];
                    for (i = k; i < m; i++)
                    {
                        a[i, j] += t * a[i, k];
                    }
                }

                // Place the k-th row of A into e for the
                // subsequent calculation of the row transformation.

                e[j] = a[k, j];
            }

            if (k < nct)
            {
                // Place the transformation in U for subsequent back
                // multiplication.

                for (i = k; i < m; i++)
                {
                    u[i, k] = a[i, k];
                }
            }

            if (k < nrt)
            {
                // Compute the k-th row transformation and place the
                // k-th super-diagonal in e[k].
                // Compute 2-norm without under/overflow.
                e[k] = 0;
                for (i = k + 1; i < n; i++)
                {
                    e[k] = Hypot(e[k], e[i]);
                }

                if (e[k] != 0.0)
                {
                    if (e[k + 1] < 0.0)
                    {
                        e[k] = -e[k];
                    }

                    for (i = k + 1; i < n; i++)
                    {
                        e[i] /= e[k];
                    }

                    e[k + 1] += 1.0;
                }

                e[k] = -e[k];
                if ((k + 1 < m) && (e[k] != 0.0))
                {
                    // Apply the transformation.

                    for (i = k + 1; i < m; i++)
                    {
                        work[i] = 0.0;
                    }

                    for (j = k + 1; j < n; j++)
                    {
                        for (i = k + 1; i < m; i++)
                        {
                            work[i] += e[j] * a[i, j];
                        }
                    }

                    for (j = k + 1; j < n; j++)
                    {
                        double t = -e[j] / e[k + 1];
                        for (i = k + 1; i < m; i++)
                        {
                            a[i, j] += t * work[i];
                        }
                    }
                }

                // Place the transformation in V for subsequent back
                // multiplication.

                for (i = k + 1; i < n; i++)
                {
                    v[i, k] = e[i];
                }
            }
        }

        // Set up the final bidiagonal matrix or order n.

        if (nct < n)
        {
            s[nct] = a[nct, nct];
        }

        if (nrt + 1 < n)
        {
            e[nrt] = a[nrt, n - 1];
        }

        e[n - 1] = 0.0;

        // generate U

        for (j = nct; j < n; j++)
        {
            for (i = 0; i < m; i++)
            {
                u[i, j] = 0.0;
            }

            u[j, j] = 1.0;
        }

        for (k = nct - 1; k >= 0; --k)
        {
            if (s[k] != 0.0)
            {
                for (j = k + 1; j < n; ++j)
                {
                    double t = 0;
                    for (i = k; i < m; i++)
                    {
                        t += u[i, k] * u[i, j];
                    }

                    t = -t / u[k, k];
                    for (i = k; i < m; i++)
                    {
                        u[i, j] += t * u[i, k];
                    }
                }

                for (i = k; i < m; i++)
                {
                    u[i, k] = -u[i, k];
                }

                u[k, k] = 1.0 + u[k, k];
                for (i = 0; i < k - 1; i++)
                {
                    u[i, k] = 0.0;
                }
            }
            else
            {
                for (i = 0; i < m; i++)
                {
                    u[i, k] = 0.0;
                }

                u[k, k] = 1.0;
            }
        }

        // generate V

        for (k = n - 1; k >= 0; --k)
        {
            if ((k < nrt) && (e[k] != 0.0))
            {
                for (j = k + 1; j < n; ++j)
                {
                    double t = 0;
                    for (i = k + 1; i < n; i++)
                    {
                        t += v[i, k] * v[i, j];
                    }

                    t = -t / v[k + 1, k];
                    for (i = k + 1; i < n; i++)
                    {
                        v[i, j] += t * v[i, k];
                    }
                }
            }

            for (i = 0; i < n; i++)
            {
                v[i, k] = 0.0;
            }

            v[k, k] = 1.0;
        }

        // Main iteration loop for the singular values.

        int p = n, pp = p - 1;
        int iter = 0;
        double eps = Math.Pow(2.0, -52.0);
        while (p > 0)
        {
            int kase;

            // Here is where a test for too many iterations would go.

            // a non-finite singular value or superdiagonal entry refused, since from it QuantLib's loop never ends
            for (int z = 0; z < p; z++)
            {
                if (!double.IsFinite(s[z]) || (z < p - 1 && !double.IsFinite(e[z])))
                {
                    string entry = double.IsFinite(s[z]) ? $"superdiagonal entry {z} is {e[z]}" : $"singular value {z} is {s[z]}";
                    throw new ArgumentException(
                        $"the singular value decomposition leaves the range of doubles ({entry}): it is refused, since its "
                        + "iteration never ends",
                        "matrix");
                }
            }

            // This section of the program inspects for
            // negligible elements in the s and e arrays.  On
            // completion the variables kase and k are set as follows.

            // kase = 1     if s(p) and e[k-1] are negligible and k<p
            // kase = 2     if s(k) is negligible and k<p
            // kase = 3     if e[k-1] is negligible, k<p, and
            //              s(k), ..., s(p) are not negligible (qr step).
            // kase = 4     if e(p-1) is negligible (convergence).

            for (k = p - 2; k >= -1; --k)
            {
                if (k == -1)
                {
                    break;
                }

                if (Math.Abs(e[k]) <= eps * (Math.Abs(s[k]) + Math.Abs(s[k + 1])))
                {
                    e[k] = 0.0;
                    break;
                }
            }

            if (k == p - 2)
            {
                kase = 4;
            }
            else
            {
                int ks;
                for (ks = p - 1; ks >= k; --ks)
                {
                    if (ks == k)
                    {
                        break;
                    }

                    double t = (ks != p ? Math.Abs(e[ks]) : 0.0) + (ks != k + 1 ? Math.Abs(e[ks - 1]) : 0.0);
                    if (Math.Abs(s[ks]) <= eps * t)
                    {
                        s[ks] = 0.0;
                        break;
                    }
                }

                if (ks == k)
                {
                    kase = 3;
                }
                else if (ks == p - 1)
                {
                    kase = 1;
                }
                else
                {
                    kase = 2;
                    k = ks;
                }
            }

            k++;

            // Perform the task indicated by kase.

            switch (kase)
            {
                // Deflate negligible s(p).

                case 1:
                {
                    double f = e[p - 2];
                    e[p - 2] = 0.0;
                    for (j = p - 2; j >= k; --j)
                    {
                        double t = Hypot(s[j], f);
                        double cs = s[j] / t;
                        double sn = f / t;
                        s[j] = t;
                        if (j != k)
                        {
                            f = -sn * e[j - 1];
                            e[j - 1] = cs * e[j - 1];
                        }

                        for (i = 0; i < n; i++)
                        {
                            t = (cs * v[i, j]) + (sn * v[i, p - 1]);
                            v[i, p - 1] = (-sn * v[i, j]) + (cs * v[i, p - 1]);
                            v[i, j] = t;
                        }
                    }
                }

                break;

                // Split at negligible s(k).

                case 2:
                {
                    double f = e[k - 1];
                    e[k - 1] = 0.0;
                    for (j = k; j < p; j++)
                    {
                        double t = Hypot(s[j], f);
                        double cs = s[j] / t;
                        double sn = f / t;
                        s[j] = t;
                        f = -sn * e[j];
                        e[j] = cs * e[j];
                        for (i = 0; i < m; i++)
                        {
                            t = (cs * u[i, j]) + (sn * u[i, k - 1]);
                            u[i, k - 1] = (-sn * u[i, j]) + (cs * u[i, k - 1]);
                            u[i, j] = t;
                        }
                    }
                }

                break;

                // Perform one qr step.

                case 3:
                {
                    // Calculate the shift.
                    double scale = Math.Max(
                        Math.Max(Math.Max(Math.Max(Math.Abs(s[p - 1]), Math.Abs(s[p - 2])), Math.Abs(e[p - 2])), Math.Abs(s[k])),
                        Math.Abs(e[k]));
                    double sp = s[p - 1] / scale;
                    double spm1 = s[p - 2] / scale;
                    double epm1 = e[p - 2] / scale;
                    double sk = s[k] / scale;
                    double ek = e[k] / scale;
                    double b = (((spm1 + sp) * (spm1 - sp)) + (epm1 * epm1)) / 2.0;
                    double c = (sp * epm1) * (sp * epm1);
                    double shift = 0.0;
                    if ((b != 0.0) || (c != 0.0))
                    {
                        shift = Math.Sqrt((b * b) + c);
                        if (b < 0.0)
                        {
                            shift = -shift;
                        }

                        shift = c / (b + shift);
                    }

                    double f = ((sk + sp) * (sk - sp)) + shift;
                    double g = sk * ek;

                    // Chase zeros.

                    for (j = k; j < p - 1; j++)
                    {
                        double t = Hypot(f, g);
                        double cs = f / t;
                        double sn = g / t;
                        if (j != k)
                        {
                            e[j - 1] = t;
                        }

                        f = (cs * s[j]) + (sn * e[j]);
                        e[j] = (cs * e[j]) - (sn * s[j]);
                        g = sn * s[j + 1];
                        s[j + 1] = cs * s[j + 1];
                        for (i = 0; i < n; i++)
                        {
                            t = (cs * v[i, j]) + (sn * v[i, j + 1]);
                            v[i, j + 1] = (-sn * v[i, j]) + (cs * v[i, j + 1]);
                            v[i, j] = t;
                        }

                        t = Hypot(f, g);
                        cs = f / t;
                        sn = g / t;
                        s[j] = t;
                        f = (cs * e[j]) + (sn * s[j + 1]);
                        s[j + 1] = (-sn * e[j]) + (cs * s[j + 1]);
                        g = sn * e[j + 1];
                        e[j + 1] = cs * e[j + 1];
                        if (j < m - 1)
                        {
                            for (i = 0; i < m; i++)
                            {
                                t = (cs * u[i, j]) + (sn * u[i, j + 1]);
                                u[i, j + 1] = (-sn * u[i, j]) + (cs * u[i, j + 1]);
                                u[i, j] = t;
                            }
                        }
                    }

                    e[p - 2] = f;
                    iter = iter + 1;
                }

                break;

                // Convergence.

                case 4:
                {
                    // Make the singular values positive.

                    if (s[k] <= 0.0)
                    {
                        s[k] = s[k] < 0.0 ? -s[k] : 0.0;
                        for (i = 0; i <= pp; i++)
                        {
                            v[i, k] = -v[i, k];
                        }
                    }

                    // Order the singular values.

                    while (k < pp)
                    {
                        if (s[k] >= s[k + 1])
                        {
                            break;
                        }

                        (s[k], s[k + 1]) = (s[k + 1], s[k]);
                        if (k < n - 1)
                        {
                            for (i = 0; i < n; i++)
                            {
                                (v[i, k], v[i, k + 1]) = (v[i, k + 1], v[i, k]);
                            }
                        }

                        if (k < m - 1)
                        {
                            for (i = 0; i < m; i++)
                            {
                                (u[i, k], u[i, k + 1]) = (u[i, k + 1], u[i, k]);
                            }
                        }

                        k++;
                    }

                    iter = 0;
                    --p;
                }

                break;
            }
        }
    }

    /// <summary>The left singular vectors: m × n for the m × n matrix with m ≥ n, a copy.</summary>
    /// <returns>U.</returns>
    public Matrix U() => (transpose ? v : u).Clone();

    /// <summary>The right singular vectors: n × n for the m × n matrix with m ≥ n, a copy.</summary>
    /// <returns>V.</returns>
    public Matrix V() => (transpose ? u : v).Clone();

    /// <summary>The singular values, in decreasing order, a copy.</summary>
    /// <returns>The singular values.</returns>
    public Vector SingularValues() => new(s.AsSpan());

    /// <summary>The diagonal matrix of the singular values.</summary>
    /// <returns>S.</returns>
    public Matrix S()
    {
        var sMatrix = new Matrix(n, n);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                sMatrix[i, j] = 0.0;
            }

            sMatrix[i, i] = s[i];
        }

        return sMatrix;
    }

    /// <summary>The 2-norm, the largest singular value.</summary>
    /// <returns>The norm.</returns>
    public double Norm2() => s[0];

    /// <summary>The condition number, the ratio of the largest singular value to the smallest.</summary>
    /// <returns>The condition number.</returns>
    public double Cond() => s[0] / s[n - 1];

    /// <summary>The numerical rank: the number of singular values above the rows times the largest times ε.</summary>
    /// <returns>The rank.</returns>
    public int Rank()
    {
        const double eps = NumericLimits.QL_EPSILON;
        double tol = m * s[0] * eps;
        int r = 0;
        foreach (double i in s)
        {
            if (i > tol)
            {
                r++;
            }
        }

        return r;
    }

    /// <summary>
    /// Solves M·x = b in the least-squares sense through the pseudo-inverse, the singular values past the numerical
    /// rank dropped.
    /// </summary>
    /// <param name="b">The right-hand side.</param>
    /// <returns>The solution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="b"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The size of <paramref name="b"/> isn't the matrix's rows.</exception>
    public Vector SolveFor(Vector b)
    {
        ArgumentNullException.ThrowIfNull(b);
        var w = new Matrix(n, n, 0.0);
        int numericalRank = Rank();
        for (int i = 0; i < numericalRank; i++)
        {
            w[i, i] = 1.0 / s[i];
        }

        Matrix inverse = V() * w * Transpose(U());
        return inverse * b;
    }

    /*  returns hypotenuse of real (non-complex) scalars a and b by
        avoiding underflow/overflow
        using (a * sqrt( 1 + (b/a) * (b/a))), rather than
        sqrt(a*a + b*b).
    */
    private static double Hypot(double a, double b)
    {
        if (a == 0)
        {
            return Math.Abs(b);
        }
        else
        {
            double c = b / a;
            return Math.Abs(a) * Math.Sqrt(1 + (c * c));
        }
    }
}
