// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianquadratures.hpp ql/math/integrals/gaussianquadratures.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Gauss-Gegenbauer integration of ∫₋₁¹ f(x) dx, with weight w(x) = (1 − x²)^(λ − ½).
/// </summary>
public sealed class GaussGegenbauerIntegration : GaussianQuadrature
{
    /// <summary>Initializes a new instance of the <see cref="GaussGegenbauerIntegration"/> class.</summary>
    /// <param name="n">The order, positive.</param>
    /// <param name="lambda">λ, above −½.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException">λ − ½ isn't above −1, or <paramref name="n"/> is zero.</exception>
    public GaussGegenbauerIntegration(int n, double lambda)
        : base(n, new GaussJacobiPolynomial(lambda - 0.5, lambda - 0.5))
    {
    }
}
