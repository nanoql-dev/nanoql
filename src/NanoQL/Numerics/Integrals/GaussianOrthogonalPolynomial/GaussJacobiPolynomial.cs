// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianorthogonalpolynomial.hpp ql/math/integrals/gaussianorthogonalpolynomial.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Gauss-Jacobi polynomial, with weight w(x; α, β) = (1 − x)^α·(1 + x)^β on [−1, 1], α, β &gt; −1. Unsealed:
/// QuantLib's Legendre, Chebyshev and Gegenbauer polynomials derive from it.
/// </summary>
public class GaussJacobiPolynomial : GaussianOrthogonalPolynomial
{
    private readonly double alpha_;
    private readonly double beta_;

    /// <summary>Initializes a new instance of the <see cref="GaussJacobiPolynomial"/> class.</summary>
    /// <param name="alpha">The power of 1 − x, above −1.</param>
    /// <param name="beta">The power of 1 + x, above −1.</param>
    /// <exception cref="ArgumentException">A power isn't above −1, or their sum isn't above −2.</exception>
    public GaussJacobiPolynomial(double alpha, double beta)
    {
        if (!(alpha + beta > -2.0))
        {
            throw new ArgumentException("alpha+beta must be bigger than -2", nameof(beta));
        }

        if (!(alpha > -1.0))
        {
            throw new ArgumentException("alpha must be bigger than -1", nameof(alpha));
        }

        if (!(beta > -1.0))
        {
            throw new ArgumentException("beta  must be bigger than -1", nameof(beta));
        }

        alpha_ = alpha;
        beta_ = beta;
    }

    /// <inheritdoc/>
    public override double Mu_0() =>
        Math.Pow(2.0, alpha_ + beta_ + 1)
        * Math.Exp(new GammaFunction().LogValue(alpha_ + 1)
                   + new GammaFunction().LogValue(beta_ + 1)
                   - new GammaFunction().LogValue(alpha_ + beta_ + 2));

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">α_i's denominator is zero and its numerator isn't.</exception>
    public override double Alpha(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        double num = (beta_ * beta_) - (alpha_ * alpha_);
        double denom = ((2.0 * i) + alpha_ + beta_) * ((2.0 * i) + alpha_ + beta_ + 2);

        if (ComparisonFunctions.CloseEnough(denom, 0.0))
        {
            if (!ComparisonFunctions.CloseEnough(num, 0.0))
            {
                throw new NanoQLException("can't compute a_k for jacobi integration\n");
            }
            else
            {
                // l'Hospital
                num = 2 * beta_;
                denom = 2 * ((2.0 * i) + alpha_ + beta_ + 1);

                if (ComparisonFunctions.CloseEnough(denom, 0.0))
                {
                    throw new NanoQLException("can't compute a_k for jacobi integration\n");
                }
            }
        }

        return num / denom;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">β_i's denominator is zero and its numerator isn't.</exception>
    public override double Beta(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        double num = 4.0 * i * (i + alpha_) * (i + beta_) * (i + alpha_ + beta_);
        double denom = ((2.0 * i) + alpha_ + beta_) * ((2.0 * i) + alpha_ + beta_)
                       * ((((2.0 * i) + alpha_ + beta_) * ((2.0 * i) + alpha_ + beta_)) - 1);

        if (ComparisonFunctions.CloseEnough(denom, 0.0))
        {
            if (!ComparisonFunctions.CloseEnough(num, 0.0))
            {
                throw new NanoQLException("can't compute b_k for jacobi integration\n");
            }
            else
            {
                // l'Hospital
                num = 4.0 * i * (i + beta_) * ((2.0 * i) + (2 * alpha_) + beta_);
                denom = 2.0 * ((2.0 * i) + alpha_ + beta_);
                denom *= denom - 1;

                if (ComparisonFunctions.CloseEnough(denom, 0.0))
                {
                    throw new NanoQLException("can't compute b_k for jacobi integration\n");
                }
            }
        }

        return num / denom;
    }

    /// <inheritdoc/>
    public override double W(double x) => Math.Pow(1 - x, alpha_) * Math.Pow(1 + x, beta_);
}
