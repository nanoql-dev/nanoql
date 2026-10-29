// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Bernd Engelmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/polynomial2Dspline.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>The <see cref="Polynomial2DSpline"/> interpolation's factory: QuantLib's <c>Polynomial</c>.</summary>
[Experimental("NQL0001")]
public readonly struct Polynomial : IInterpolator2D
{
    /// <summary>Builds a <see cref="Polynomial2DSpline"/> over the given grid, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, which must be sorted; at least 2.</param>
    /// <param name="z">The function values, a row per y value and a column per x value.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="z"/> is null.</exception>
    /// <exception cref="ArgumentException">The sizes don't match.</exception>
    public Interpolation2D Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix z) =>
        new Polynomial2DSpline(x, y, z);
}
