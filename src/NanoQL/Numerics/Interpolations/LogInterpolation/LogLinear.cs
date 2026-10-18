// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/loginterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The log-linear interpolation factory and traits.</summary>
/// <remarks>A stateless configuration struct: every instance, its zero value included, is QuantLib's <c>LogLinear()</c>.</remarks>
public readonly struct LogLinear : IInterpolator
{
    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="false"/>.</summary>
    public static bool Global => false;

    /// <summary>Gets the fewest points the interpolation takes: 2.</summary>
    public static int RequiredPoints => 2;

    /// <summary>Builds a <see cref="LogLinearInterpolation"/> over the given points, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value, each positive.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="ArgumentException">The sizes differ, there are fewer than 2 points, or a y value isn't positive.</exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) => new LogLinearInterpolation(x, y);

    /// <inheritdoc/>
    public void CheckPoints(int points) => Interpolation.CheckRequiredPoints(points, Linear.RequiredPoints);
}
