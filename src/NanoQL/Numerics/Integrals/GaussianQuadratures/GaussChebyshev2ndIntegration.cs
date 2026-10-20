// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Gauss-Chebyshev integration of the second kind of ∫₋₁¹ f(x) dx, with weight w(x) = (1 − x²)^(½).
/// </summary>
public sealed class GaussChebyshev2ndIntegration : GaussianQuadrature
{
    /// <summary>Initializes a new instance of the <see cref="GaussChebyshev2ndIntegration"/> class.</summary>
    /// <param name="n">The order, positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="n"/> is zero.</exception>
    public GaussChebyshev2ndIntegration(int n)
        : base(n, new GaussJacobiPolynomial(0.5, 0.5))
    {
    }
}
