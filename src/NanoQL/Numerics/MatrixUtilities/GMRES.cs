// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/gmres.hpp ql/math/matrixutilities/gmres.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// The generalized minimal residual method for a linear system A·x = b, A given as its product with a vector, with an
/// optional preconditioner. References: Y. Saad, Iterative methods for sparse linear systems (1996),
/// http://www-users.cs.umn.edu/~saad/books.html; R. Barrett et al., Templates for the Solution of Linear Systems:
/// Building Blocks for Iterative Methods, 2nd edition, SIAM (1994), http://www.netlib.org/templates/templates.pdf;
/// C. Kanzow, Numerik linearer Gleichungssysteme, chapter 6.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib stops at a breakdown, the new Krylov vector's norm below ε², without rotating the column that found it,
/// and solves with the columns before it (<c>gmres.cpp:101-131</c>). At the first step there are none: the
/// back-substitution writes before its array, undefined (the process crashes on diag(2, 3) with b = (1, 0), an
/// eigenvector). At a later step the column dropped turns a solved system into a refusal: with A = diag(1, 1, 3, 3) and
/// b = (1, 1, 1, 1), the vector is exactly zero at the second step, and <c>solve</c> throws "could not converge" where
/// <c>solveWithRestart(5)</c> returns (1, 1, 1/3, 1/3). Here, as the method defines the breakdown, the Krylov space
/// holds the solution: the column is rotated and the system solved with it. A documented divergence.
/// </para>
/// <para>
/// A restart count of zero is refused: QuantLib's bound <c>restart - 1</c> wraps to unlimited restarts
/// (<c>gmres.cpp:54</c>), where every positive count bounds them, and it refuses a zero bound on the iterations.
/// </para>
/// </remarks>
public sealed class GMRES
{
    private readonly Func<Vector, Vector> A_;
    private readonly Func<Vector, Vector>? M_;
    private readonly int maxIter_;
    private readonly double relTol_;

    /// <summary>Initializes a new instance of the <see cref="GMRES"/> class.</summary>
    /// <param name="A">The matrix's product with a vector.</param>
    /// <param name="maxIter">The maximum number of steps, positive.</param>
    /// <param name="relTol">The tolerance on the residual's norm relative to the right-hand side's.</param>
    /// <param name="preConditioner">The preconditioner's product with a vector, or <see langword="null"/> for
    /// none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="A"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIter"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="maxIter"/> is zero.</exception>
    public GMRES(Func<Vector, Vector> A, int maxIter, double relTol, Func<Vector, Vector>? preConditioner = null)
    {
        ArgumentNullException.ThrowIfNull(A);
        ArgumentOutOfRangeException.ThrowIfNegative(maxIter);
        A_ = A;
        M_ = preConditioner;
        maxIter_ = maxIter;
        relTol_ = relTol;

        if (!(maxIter_ > 0))
        {
            throw new ArgumentException("maxIter must be greater than zero", nameof(maxIter));
        }
    }

    /// <summary>Solves A·x = <paramref name="b"/> in at most the maximum number of steps.</summary>
    /// <param name="b">The right-hand side.</param>
    /// <param name="x0">The initial guess, or <see langword="null"/> or empty for zero.</param>
    /// <returns>The solution, with the relative residual after each step.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="b"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The last residual doesn't meet the tolerance.</exception>
    public GMRESResult Solve(Vector b, Vector? x0 = null)
    {
        ArgumentNullException.ThrowIfNull(b);
        GMRESResult result = SolveImpl(b, x0);

        if (!(result.Errors[^1] < relTol_))
        {
            throw new NanoQLException("could not converge");
        }

        return result;
    }

    /// <summary>
    /// Solves A·x = <paramref name="b"/>, restarting from the last solution at most <paramref name="restart"/> − 1
    /// times while the residual doesn't meet the tolerance.
    /// </summary>
    /// <param name="restart">The maximum number of solves, positive.</param>
    /// <param name="b">The right-hand side.</param>
    /// <param name="x0">The initial guess, or <see langword="null"/> or empty for zero.</param>
    /// <returns>The last solution, with the relative residuals of every solve.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="b"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="restart"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="restart"/> is zero.</exception>
    /// <exception cref="NanoQLException">The last residual doesn't meet the tolerance.</exception>
    public GMRESResult SolveWithRestart(int restart, Vector b, Vector? x0 = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(restart);
        ArgumentNullException.ThrowIfNull(b);

        // QuantLib's restart - 1 wraps to unlimited restarts at 0
        if (restart == 0)
        {
            throw new ArgumentException("restart must be greater than zero", nameof(restart));
        }

        GMRESResult result = SolveImpl(b, x0);

        ImmutableArray<double>.Builder errors = result.Errors.ToBuilder();

        for (int i = 0; i < restart - 1 && result.Errors[^1] >= relTol_; ++i)
        {
            result = SolveImpl(b, result.X);
            errors.AddRange(result.Errors);
        }

        if (!(errors[^1] < relTol_))
        {
            throw new NanoQLException("could not converge");
        }

        return result with { Errors = errors.ToImmutable() };
    }

    private GMRESResult SolveImpl(Vector b, Vector? x0)
    {
        double bn = ArrayFunctions.Norm2(b);
        if (bn == 0.0)
        {
            return new GMRESResult([0.0], b.Clone());
        }

        Vector x = (x0 is not null && !x0.Empty) ? x0.Clone() : new Vector(b.Size, 0.0);
        Vector r = b - A_(x);

        double g = ArrayFunctions.Norm2(r);
        if (g / bn < relTol_)
        {
            return new GMRESResult([g / bn], x);
        }

        var v = new List<Vector> { r / g };
        var h = new List<double[]> { new double[maxIter_] };
        double[] c = new double[maxIter_ + 1], s = new double[maxIter_ + 1], z = new double[maxIter_ + 1];

        z[0] = g;

        var errors = ImmutableArray.CreateBuilder<double>();
        errors.Add(g / bn);

        // the columns the solve uses: one per completed step, the breakdown's included
        int k = 0;
        for (int j = 0; j < maxIter_ && errors[^1] >= relTol_; ++j)
        {
            h.Add(new double[maxIter_]);
            Vector w = A_(M_ is null ? v[j] : M_(v[j]));

            for (int i = 0; i <= j; ++i)
            {
                h[i][j] = ArrayFunctions.DotProduct(w, v[i]);
                w -= h[i][j] * v[i];
            }

            h[j + 1][j] = ArrayFunctions.Norm2(w);

            // a breakdown: the Krylov space holds the solution, so the column is rotated and solved with, and no vector
            // is added (QuantLib stops here, dropping the column)
            bool breakdown = h[j + 1][j] < NumericLimits.QL_EPSILON * NumericLimits.QL_EPSILON;
            if (!breakdown)
            {
                v.Add(w / h[j + 1][j]);
            }

            for (int i = 0; i < j; ++i)
            {
                double h0 = (c[i] * h[i][j]) + (s[i] * h[i + 1][j]);
                double h1 = (-s[i] * h[i][j]) + (c[i] * h[i + 1][j]);

                h[i][j] = h0;
                h[i + 1][j] = h1;
            }

            double nu = Math.Sqrt(Squared(h[j][j]) + Squared(h[j + 1][j]));

            c[j] = h[j][j] / nu;
            s[j] = h[j + 1][j] / nu;

            h[j][j] = nu;
            h[j + 1][j] = 0.0;

            z[j + 1] = -s[j] * z[j];
            z[j] = c[j] * z[j];

            errors.Add(Math.Abs(z[j + 1] / bn));
            k = j + 1;

            if (breakdown)
            {
                break;
            }
        }

        var y = new Vector(k, 0.0);
        y[k - 1] = z[k - 1] / h[k - 1][k - 1];

        for (int i = k - 2; i >= 0; --i)
        {
            double inner = 0.0;
            for (int l = i + 1; l < k; ++l)
            {
                inner += h[i][l] * y[l];
            }

            y[i] = (z[i] - inner) / h[i][i];
        }

        var xm = new Vector(x.Size, 0.0);
        for (int i = 0; i < k; ++i)
        {
            xm = xm + (v[i] * y[i]);
        }

        xm = x + (M_ is null ? xm : M_(xm));

        return new GMRESResult(errors.ToImmutable(), xm);
    }

    private static double Squared(double x) => x * x;
}
