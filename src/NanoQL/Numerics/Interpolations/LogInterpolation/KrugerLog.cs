// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/loginterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's preset log-cubic factory: Kruger's approximation of the logarithms, not monotonic, with second-derivative
/// boundary conditions of 0.
/// </summary>
/// <remarks>
/// A parameterless configuration struct, so that it can be a type argument where QuantLib names it as one; every
/// instance, its zero value included, is QuantLib's <c>KrugerLog()</c>. QuantLib derives it from <c>LogCubic</c>: here
/// it converts to the <see cref="LogCubic"/> it is.
/// </remarks>
public readonly struct KrugerLog : IInterpolator
{
    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="true"/>, as <see cref="LogCubic"/>'s.</summary>
    public static bool Global => LogCubic.Global;

    /// <summary>Gets the fewest points the interpolation takes: 2, as <see cref="LogCubic"/>'s.</summary>
    public static int RequiredPoints => LogCubic.RequiredPoints;

    /// <summary>The <see cref="LogCubic"/> the preset is.</summary>
    /// <param name="preset">The preset.</param>
    /// <returns>A log-cubic factory with Kruger's approximation, not monotonic, and second-derivative conditions of 0.</returns>
    public static implicit operator LogCubic(KrugerLog preset) =>
        new(
            CubicInterpolationDerivativeApprox.Kruger,
            false,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0);

    /// <summary>Builds the preset's <see cref="LogCubicInterpolation"/> over the given points, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value, each positive.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="ArgumentException">The sizes differ, there are fewer than 2 points, or a y value isn't positive.</exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) => ((LogCubic)this).Interpolate(x, y);

    /// <inheritdoc/>
    public void CheckPoints(int points) => ((LogCubic)this).CheckPoints(points);
}
