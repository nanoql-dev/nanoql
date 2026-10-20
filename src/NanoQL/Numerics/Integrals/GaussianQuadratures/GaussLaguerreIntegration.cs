// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The generalized Gauss-Laguerre integration of ∫₀^∞ f(x) dx, with weight w(x; s) = x^s·e^(−x), s &gt; −1.
/// </summary>
public sealed class GaussLaguerreIntegration : GaussianQuadrature
{
    /// <summary>Initializes a new instance of the <see cref="GaussLaguerreIntegration"/> class.</summary>
    /// <param name="n">The order, positive.</param>
    /// <param name="s">The weight's power, above −1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="s"/> isn't above −1, or <paramref name="n"/> is
    /// zero.</exception>
    public GaussLaguerreIntegration(int n, double s = 0.0)
        : base(n, new GaussLaguerrePolynomial(s))
    {
    }
}
