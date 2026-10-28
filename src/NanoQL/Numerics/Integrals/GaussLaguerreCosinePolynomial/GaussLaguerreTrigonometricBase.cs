// SPDX-FileCopyrightText: Copyright (C) 2020 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gausslaguerrecosinepolynomial.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The base of the Gauss-Laguerre cosine and sine polynomials: the moments of e^(−x) times cos(ux) or sin(ux), by
/// their recurrence mₙ = (2n·mₙ₋₁ − n(n − 1)·mₙ₋₂)/(1 + u²), and the factorials.
/// </summary>
/// <remarks>
/// QuantLib keeps the moments and factorials in NaN-filled memos; here each call computes moment n and n! by a loop,
/// the same products in QuantLib's order, so QuantLib's values, in O(n) with nothing kept (the S-core plan's CR5 (d)).
/// </remarks>
public abstract class GaussLaguerreTrigonometricBase : MomentBasedGaussianPolynomial
{
    /// <summary>Initializes a new instance of the <see cref="GaussLaguerreTrigonometricBase"/> class.</summary>
    /// <param name="u">The frequency u.</param>
    protected GaussLaguerreTrigonometricBase(double u) => U = u;

    /// <summary>Gets the frequency u.</summary>
    protected double U { get; }

    /// <summary>The zeroth moment of the trigonometric weight, the recurrence's start.</summary>
    /// <returns>The moment.</returns>
    protected abstract double M0();

    /// <summary>The first moment of the trigonometric weight, the recurrence's second start.</summary>
    /// <returns>The moment.</returns>
    protected abstract double M1();

    /// <summary>The n-th moment of the trigonometric weight, by the recurrence from <see cref="M0"/> and
    /// <see cref="M1"/>.</summary>
    /// <param name="n">The order, non-negative.</param>
    /// <returns>The moment.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    protected double TrigonometricMoment(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        double m0 = M0();
        if (n == 0)
        {
            return m0;
        }

        double m1 = M1();
        double denominator = 1 + (U * U);
        for (long k = 2; k <= n; ++k)
        {
            double mk = (((2 * k) * m1) - ((k * (k - 1)) * m0)) / denominator;
            m0 = m1;
            m1 = mk;
        }

        return m1;
    }

    /// <summary>The factorial n!, as QuantLib's products.</summary>
    /// <param name="n">The argument, non-negative.</param>
    /// <returns>The factorial.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    protected static double Fact(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        double f = 1.0;
        for (long k = 1; k <= n; ++k)
        {
            f = k * f;
        }

        return f;
    }
}
