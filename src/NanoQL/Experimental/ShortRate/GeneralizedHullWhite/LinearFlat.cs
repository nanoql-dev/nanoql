// SPDX-FileCopyrightText: Copyright (C) 2010 SunTrust Bank
// SPDX-FileCopyrightText: Copyright (C) 2010, 2014 Cavit Hafizoglu
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/shortrate/generalizedhullwhite.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The linear interpolation with flat extrapolation's factory and traits: QuantLib's <c>LinearFlat</c>.
/// </summary>
/// <remarks>
/// A stateless configuration struct: every instance, its zero value included, is QuantLib's <c>LinearFlat()</c>.
/// </remarks>
[Experimental("NQL0001")]
public readonly struct LinearFlat : IInterpolator
{
    /// <summary>Gets a value indicating whether the interpolation is global: <see langword="false"/>.</summary>
    public static bool Global => false;

    /// <summary>Gets the fewest points the interpolation takes: 1.</summary>
    public static int RequiredPoints => 1;

    /// <summary>Builds a <see cref="LinearFlatInterpolation"/> over the given points, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="ArgumentException">The sizes differ, or there are no points.</exception>
    public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
        new LinearFlatInterpolation(x, y);

    /// <inheritdoc/>
    public void CheckPoints(int points) => Interpolation.CheckRequiredPoints(points, RequiredPoints);
}
