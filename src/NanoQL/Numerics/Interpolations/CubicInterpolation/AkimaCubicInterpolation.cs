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
/// Cubic interpolation with Akima's first-derivative approximation; at least 4 points.
/// </summary>
/// <remarks>
/// QuantLib's convenience class: a <see cref="CubicInterpolation"/> with
/// <see cref="CubicInterpolationDerivativeApprox.Akima"/>, monotonic <see langword="false"/>, and second-derivative
/// boundary conditions of 0, which only the spline schemes read.
/// </remarks>
public sealed class AkimaCubicInterpolation : CubicInterpolation
{
    /// <summary>Initializes a new instance of the <see cref="AkimaCubicInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 4.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <exception cref="ArgumentException">The sizes differ, or there are too few points.</exception>
    public AkimaCubicInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
        : base(
            x,
            y,
            CubicInterpolationDerivativeApprox.Akima,
            false,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0)
    {
    }
}
