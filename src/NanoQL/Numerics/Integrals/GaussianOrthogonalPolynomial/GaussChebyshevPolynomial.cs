// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/gaussianorthogonalpolynomial.hpp ql/math/integrals/gaussianorthogonalpolynomial.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The Gauss-Chebyshev polynomial: Jacobi's with α = β = −½, weight (1 − x²)^(−½) on [−1, 1].</summary>
public sealed class GaussChebyshevPolynomial : GaussJacobiPolynomial
{
    /// <summary>Initializes a new instance of the <see cref="GaussChebyshevPolynomial"/> class.</summary>
    public GaussChebyshevPolynomial()
        : base(-0.5, -0.5)
    {
    }
}
