// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/linearinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The linear-interpolation factory and traits.</summary>
/// <remarks>A stateless configuration struct: every instance, its zero value included, is QuantLib's <c>Linear()</c>.</remarks>
public readonly struct Linear : IInterpolator
{
    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="false"/>.</summary>
    public static bool Global => false;

    /// <summary>Gets the fewest points the interpolation takes: 2.</summary>
    public static int RequiredPoints => 2;

    /// <summary>Builds a <see cref="LinearInterpolation"/> over the given points, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="ArgumentException">The sizes differ, or there are fewer than 2 points.</exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) => new LinearInterpolation(x, y);
}
