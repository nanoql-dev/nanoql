// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A Gaussian quadrature: the nodes and weights of an orthogonal polynomial's rule of a given order, from the
/// eigen-decomposition of its recurrence's Jacobi matrix (G.H. Golub and J.H. Welsch, "Calculation of Gauss quadrature
/// rules", Math. Comput. 23 (1969), 221-230), and the weighted sum of a function at the nodes.
/// </summary>
/// <remarks>
/// A weight whose quotient overflows or underflows is formed in logarithms; one that can't be recovered, its
/// eigenvector component zero, is dropped as QuantLib drops it. Order zero is refused: QuantLib allocates an array of
/// n − 1 elements, which wraps (<c>gaussianquadratures.cpp:41</c>), and throws C++'s <c>std::bad_array_new_length</c>.
/// </remarks>
public class GaussianQuadrature
{
    private readonly Vector x_;
    private readonly Vector w_;

    /// <summary>Initializes a new instance of the <see cref="GaussianQuadrature"/> class.</summary>
    /// <param name="n">The order, positive.</param>
    /// <param name="orthPoly">The orthogonal polynomial.</param>
    /// <exception cref="ArgumentNullException"><paramref name="orthPoly"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="n"/> is zero.</exception>
    public GaussianQuadrature(int n, GaussianOrthogonalPolynomial orthPoly)
    {
        ArgumentNullException.ThrowIfNull(orthPoly);
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        x_ = new Vector(n);
        w_ = new Vector(n);

        // set-up matrix to compute the roots and the weights; QuantLib's Array e(n-1) throws
        // std::bad_array_new_length at n = 0
        if (n == 0)
        {
            throw new ArgumentException("order 0 not allowed: a Gaussian quadrature needs at least one node", nameof(n));
        }

        var e = new Vector(n - 1);

        int i;
        for (i = 1; i < n; ++i)
        {
            x_[i] = orthPoly.Alpha(i);
            e[i - 1] = Math.Sqrt(orthPoly.Beta(i));
        }

        x_[0] = orthPoly.Alpha(0);

        var tqr = new TqrEigenDecomposition(
            x_,
            e,
            TqrEigenDecompositionEigenVectorCalculation.OnlyFirstRowEigenVector,
            TqrEigenDecompositionShiftStrategy.Overrelaxation);

        x_ = tqr.Eigenvalues();
        Matrix ev = tqr.Eigenvectors();

        double mu_0 = orthPoly.Mu_0();
        for (i = 0; i < n; ++i)
        {
            // orthPoly.w(x_[i]) can underflow to zero for large orders (e.g. Gauss-Laguerre
            // beyond n ~ 200) while the quotient itself stays representable; forming it in
            // logarithms keeps every intermediate value in range in that case.
            double wx = orthPoly.W(x_[i]);
            if (wx > 0.0)
            {
                w_[i] = mu_0 * ev[0, i] * ev[0, i] / wx;
            }
            else
            {
                w_[i] = Math.Exp(Math.Log(mu_0)
                                 + (2.0 * Math.Log(Math.Abs(ev[0, i])))
                                 - orthPoly.LogW(x_[i]));
            }

            if (!double.IsFinite(w_[i]))
            {
                // w(x) can come back subnormal rather than zero, in which
                // case the division above overflows and the branch on wx
                // does not catch it. Retry in logarithms.
                if (ev[0, i] != 0.0)
                {
                    w_[i] = Math.Exp(Math.Log(mu_0)
                                     + (2.0 * Math.Log(Math.Abs(ev[0, i])))
                                     - orthPoly.LogW(x_[i]));
                }

                // Once ev[0][i] has underflowed to zero the weight cannot
                // be recovered. Such a node sits far enough into the tail
                // that its contribution is negligible, so drop it rather
                // than leave a NaN to poison the whole integral.
                if (!double.IsFinite(w_[i]))
                {
                    w_[i] = 0.0;
                }
            }
        }
    }

    /// <summary>Gets the order, the number of nodes.</summary>
    public int Order => x_.Size;

    /// <summary>The weighted sum of <paramref name="f"/> at the nodes, from the last node to the first.</summary>
    /// <param name="f">The function.</param>
    /// <returns>Σ wᵢ·f(xᵢ).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    public double Evaluate(Func<double, double> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        double sum = 0.0;
        for (int i = Order - 1; i >= 0; --i)
        {
            sum += w_[i] * f(x_[i]);
        }

        return sum;
    }

    /// <summary>The weights.</summary>
    /// <returns>A copy of the weights.</returns>
    public Vector Weights() => w_.Clone();

    /// <summary>The nodes.</summary>
    /// <returns>A copy of the nodes.</returns>
    public Vector X() => x_.Clone();
}
