// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Gauss-Jacobi integration of ∫₋₁¹ f(x) dx, with weight w(x; α, β) = (1 − x)^α·(1 + x)^β.
/// </summary>
public sealed class GaussJacobiIntegration : GaussianQuadrature
{
    /// <summary>Initializes a new instance of the <see cref="GaussJacobiIntegration"/> class.</summary>
    /// <param name="n">The order, positive.</param>
    /// <param name="alpha">The power of 1 − x, above −1.</param>
    /// <param name="beta">The power of 1 + x, above −1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException">A power isn't above −1, their sum isn't above −2, or <paramref name="n"/> is
    /// zero.</exception>
    public GaussJacobiIntegration(int n, double alpha, double beta)
        : base(n, new GaussJacobiPolynomial(alpha, beta))
    {
    }
}
