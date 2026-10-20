// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/matrixutilities/bicgstab.hpp ql/math/matrixutilities/bicgstab.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The biconjugate gradient stabilized method for a linear system A·x = b, A given as its product with a vector, with
/// an optional preconditioner.
/// </summary>
/// <remarks>
/// QuantLib's final checks test the number of iterations before the error (<c>bicgstab.cpp:87-88</c>), so a system
/// whose residual meets the tolerance in the last allowed iteration is refused, "max number of iterations exceeded": a
/// 6×6 system solved to 8.97e-9 against a tolerance of 1e-8 is refused with 6 iterations allowed and solved with 7.
/// Here the error is tested first, QuantLib's own criterion; the count's refusal keeps its text. A documented
/// divergence.
/// </remarks>
public sealed class BiCGstab
{
    private readonly Func<Vector, Vector> A_;
    private readonly Func<Vector, Vector>? M_;
    private readonly int maxIter_;
    private readonly double relTol_;

    /// <summary>Initializes a new instance of the <see cref="BiCGstab"/> class.</summary>
    /// <param name="A">The matrix's product with a vector.</param>
    /// <param name="maxIter">The maximum number of iterations.</param>
    /// <param name="relTol">The tolerance on the residual's norm relative to the right-hand side's.</param>
    /// <param name="preConditioner">The preconditioner's product with a vector, or <see langword="null"/> for
    /// none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="A"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIter"/> is negative.</exception>
    public BiCGstab(Func<Vector, Vector> A, int maxIter, double relTol, Func<Vector, Vector>? preConditioner = null)
    {
        ArgumentNullException.ThrowIfNull(A);
        ArgumentOutOfRangeException.ThrowIfNegative(maxIter);
        A_ = A;
        M_ = preConditioner;
        maxIter_ = maxIter;
        relTol_ = relTol;
    }

    /// <summary>Solves A·x = <paramref name="b"/>.</summary>
    /// <param name="b">The right-hand side.</param>
    /// <param name="x0">The initial guess, or <see langword="null"/> or empty for zero.</param>
    /// <returns>The solution, with its number of iterations and relative residual.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="b"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The residual doesn't meet the tolerance.</exception>
    public BiCGStabResult Solve(Vector b, Vector? x0 = null)
    {
        ArgumentNullException.ThrowIfNull(b);
        double bnorm2 = ArrayFunctions.Norm2(b);
        if (bnorm2 == 0.0)
        {
            return new BiCGStabResult(0, 0.0, b.Clone());
        }

        Vector x = (x0 is not null && !x0.Empty) ? x0.Clone() : new Vector(b.Size, 0.0);
        Vector r = b - A_(x);

        Vector rTld = r.Clone();
        Vector p = new(), pTld, v = new(), s, sTld, t;
        double omega = 1.0;
        double rho, rhoTld = 1.0;
        double alpha = 0.0, beta;
        double error = ArrayFunctions.Norm2(r) / bnorm2;

        int i;
        for (i = 0; i < maxIter_ && error >= relTol_; ++i)
        {
            rho = ArrayFunctions.DotProduct(rTld, r);
            if (rho == 0.0 || omega == 0.0)
            {
                break;
            }

            if (i != 0)
            {
                beta = rho / rhoTld * (alpha / omega);
                p = r + (beta * (p - (omega * v)));
            }
            else
            {
                p = r.Clone();
            }

            pTld = M_ is null ? p.Clone() : M_(p);
            v = A_(pTld);

            alpha = rho / ArrayFunctions.DotProduct(rTld, v);
            s = r - (alpha * v);
            if (ArrayFunctions.Norm2(s) < relTol_ * bnorm2)
            {
                x += alpha * pTld;
                error = ArrayFunctions.Norm2(s) / bnorm2;
                break;
            }

            sTld = M_ is null ? s.Clone() : M_(s);
            t = A_(sTld);
            omega = ArrayFunctions.DotProduct(t, s) / ArrayFunctions.DotProduct(t, t);
            x += (alpha * pTld) + (omega * sTld);
            r = s - (omega * t);
            error = ArrayFunctions.Norm2(r) / bnorm2;
            rhoTld = rho;
        }

        // QuantLib tests the count first, refusing a residual that met the tolerance in the last iteration
        if (!(error < relTol_))
        {
            if (!(i < maxIter_))
            {
                throw new NanoQLException("max number of iterations exceeded");
            }

            throw new NanoQLException("could not converge");
        }

        return new BiCGStabResult(i, error, x);
    }
}
