// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2010 Kakhkhor Abdijalilov
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/lsmbasissystem.hpp ql/methods/montecarlo/lsmbasissystem.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The polynomial families of the Longstaff-Schwartz basis systems: QuantLib's <c>LsmBasisSystem::PolynomialType</c>.</summary>
public enum LsmBasisSystemPolynomialType
{
    /// <summary>The monomials xⁱ.</summary>
    Monomial,

    /// <summary>The Laguerre polynomials, weighted.</summary>
    Laguerre,

    /// <summary>The Hermite polynomials, weighted.</summary>
    Hermite,

    /// <summary>The hyperbolic polynomials, weighted.</summary>
    Hyperbolic,

    /// <summary>The Legendre polynomials, weighted.</summary>
    Legendre,

    /// <summary>The Chebyshev polynomials of the first kind, weighted.</summary>
    Chebyshev,

    /// <summary>The Chebyshev polynomials of the second kind, weighted.</summary>
    Chebyshev2nd,
}
