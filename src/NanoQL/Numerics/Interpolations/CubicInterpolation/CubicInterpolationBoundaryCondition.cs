// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2004, 2008, 2009, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009 Sylvain Bertrand
// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2016 Nicholas Bertocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/cubicinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A boundary condition of a <see cref="CubicInterpolationDerivativeApprox.Spline"/> cubic interpolation, on its left
/// or right end: QuantLib's <c>CubicInterpolation::BoundaryCondition</c>, with its values. The local schemes ignore it.
/// </summary>
public enum CubicInterpolationBoundaryCondition
{
    /// <summary>Make the second (or second-to-last) point an inactive knot; the condition's value is ignored.</summary>
    NotAKnot = 0,

    /// <summary>Match the value of the end slope.</summary>
    FirstDerivative = 1,

    /// <summary>Match the value of the second derivative at the end.</summary>
    SecondDerivative = 2,

    /// <summary>Match the first and second derivatives at either end; not implemented in QuantLib.</summary>
    Periodic = 3,

    /// <summary>
    /// Match the end slope to the slope of the cubic that matches the first (or last) four data; at least 4 points.
    /// </summary>
    Lagrange = 4,
}
