// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianorthogonalpolynomial.hpp ql/math/integrals/gaussianorthogonalpolynomial.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// An orthogonal polynomial for Gaussian quadratures, defined by the three-term recurrence
/// P_{k+1}(x) = (x − α_k)·P_k(x) − β_k·P_{k−1}(x) and μ₀ = ∫ w(x) dx: G.H. Golub and J.H. Welsch, "Calculation of
/// Gauss quadrature rules", Math. Comput. 23 (1969), 221-230; "Numerical Recipes in C", 2nd edition, Press,
/// Teukolsky, Vetterling, Flannery.
/// </summary>
public abstract class GaussianOrthogonalPolynomial
{
    /// <summary>μ₀, the integral of the weight function.</summary>
    /// <returns>μ₀.</returns>
    public abstract double Mu_0();

    /// <summary>The recurrence's α_i.</summary>
    /// <param name="i">The index, non-negative.</param>
    /// <returns>α_i.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    public abstract double Alpha(int i);

    /// <summary>The recurrence's β_i.</summary>
    /// <param name="i">The index, non-negative.</param>
    /// <returns>β_i.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    public abstract double Beta(int i);

    /// <summary>The weight function w(x).</summary>
    /// <param name="x">The point.</param>
    /// <returns>w(x).</returns>
    public abstract double W(double x);

    /// <summary>
    /// log(w(x)), kept finite past the range where w(x) itself underflows to zero. The default is log(w(x)); a
    /// polynomial whose w can underflow while log(w) stays representable overrides it.
    /// </summary>
    /// <param name="x">The point.</param>
    /// <returns>log(w(x)).</returns>
    public virtual double LogW(double x) => Math.Log(W(x));

    /// <summary>The polynomial P_n at <paramref name="x"/>, by the recurrence.</summary>
    /// <param name="n">The degree, non-negative.</param>
    /// <param name="x">The point.</param>
    /// <returns>P_n(x).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    public double Value(int n, double x)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        if (n > 1)
        {
            return ((x - Alpha(n - 1)) * Value(n - 1, x))
                   - (Beta(n - 1) * Value(n - 2, x));
        }
        else if (n == 1)
        {
            return x - Alpha(0);
        }

        return 1;
    }

    /// <summary>The polynomial weighted by the square root of the weight, √w(x)·P_n(x).</summary>
    /// <param name="n">The degree, non-negative.</param>
    /// <param name="x">The point.</param>
    /// <returns>√w(x)·P_n(x).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    public double WeightedValue(int n, double x) => Math.Sqrt(W(x)) * Value(n, x);
}
