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
/// How a <see cref="CubicInterpolation"/> approximates the first derivatives at its nodes: QuantLib's
/// <c>CubicInterpolation::DerivativeApprox</c>, with its values.
/// </summary>
/// <remarks>
/// Local schemes (fourth-order, parabolic, Fritsch–Butland, Akima, Kruger, harmonic) use only the values near a node;
/// the non-local spline schemes solve a linear system over all of them. Local schemes give C¹ interpolants, the spline
/// schemes C² ones.
/// </remarks>
public enum CubicInterpolationDerivativeApprox
{
    /// <summary>
    /// Spline approximation (non-local, non-monotonic, linear), with the boundary conditions of
    /// <see cref="CubicInterpolationBoundaryCondition"/> on each side.
    /// </summary>
    Spline = 0,

    /// <summary>Overshooting minimization, 1st derivative.</summary>
    SplineOM1 = 1,

    /// <summary>Overshooting minimization, 2nd derivative.</summary>
    SplineOM2 = 2,

    /// <summary>Fourth-order approximation (local, non-monotonic, linear); not implemented in QuantLib.</summary>
    FourthOrder = 3,

    /// <summary>Parabolic approximation (local, non-monotonic, linear).</summary>
    Parabolic = 4,

    /// <summary>Fritsch–Butland approximation (local, monotonic, non-linear).</summary>
    FritschButland = 5,

    /// <summary>Akima approximation (local, non-monotonic, non-linear); at least 4 points.</summary>
    Akima = 6,

    /// <summary>Kruger approximation (local, monotonic, non-linear).</summary>
    Kruger = 7,

    /// <summary>Weighted harmonic mean approximation (local, monotonic, non-linear).</summary>
    Harmonic = 8,
}
