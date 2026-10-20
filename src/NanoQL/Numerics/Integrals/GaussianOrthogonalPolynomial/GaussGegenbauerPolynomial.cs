// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianorthogonalpolynomial.hpp ql/math/integrals/gaussianorthogonalpolynomial.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The Gauss-Gegenbauer polynomial: Jacobi's with α = β = λ − ½, weight (1 − x²)^(λ − ½) on [−1, 1].
/// </summary>
public sealed class GaussGegenbauerPolynomial : GaussJacobiPolynomial
{
    /// <summary>Initializes a new instance of the <see cref="GaussGegenbauerPolynomial"/> class.</summary>
    /// <param name="lambda">λ, above −½.</param>
    /// <exception cref="ArgumentException">λ − ½ isn't above −1.</exception>
    public GaussGegenbauerPolynomial(double lambda)
        : base(lambda - 0.5, lambda - 0.5)
    {
    }
}
