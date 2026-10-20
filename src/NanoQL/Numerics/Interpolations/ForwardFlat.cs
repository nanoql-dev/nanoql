// SPDX-FileCopyrightText: Copyright (C) 2005, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/forwardflatinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The forward-flat interpolation factory and traits.</summary>
/// <remarks>
/// A stateless configuration struct: every instance, its zero value included, is QuantLib's <c>ForwardFlat()</c>.
/// </remarks>
public readonly struct ForwardFlat : IInterpolator
{
    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="false"/>.</summary>
    public static bool Global => false;

    /// <summary>Gets the fewest points the interpolation takes: 2.</summary>
    public static int RequiredPoints => 2;

    /// <summary>Builds a <see cref="ForwardFlatInterpolation"/> over the given points, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="ArgumentException">The sizes differ, or there are fewer than two points.</exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
        new ForwardFlatInterpolation(x, y);

    /// <inheritdoc/>
    public void CheckPoints(int points) => Interpolation.CheckRequiredPoints(points, RequiredPoints);
}
