// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The generalized Gauss-Hermite integration of ∫ f(x) dx over ℝ, with weight w(x; μ) = |x|^(2μ)·e^(−x²), μ &gt; −0.5.
/// </summary>
public sealed class GaussHermiteIntegration : GaussianQuadrature
{
    /// <summary>Initializes a new instance of the <see cref="GaussHermiteIntegration"/> class.</summary>
    /// <param name="n">The order, positive.</param>
    /// <param name="mu">The weight's parameter, above −0.5.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="mu"/> isn't above −0.5, or <paramref name="n"/> is
    /// zero.</exception>
    public GaussHermiteIntegration(int n, double mu = 0.0)
        : base(n, new GaussHermitePolynomial(mu))
    {
    }
}
