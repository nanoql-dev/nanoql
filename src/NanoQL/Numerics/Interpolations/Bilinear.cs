// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/bilinearinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The bilinear-interpolation factory.</summary>
/// <remarks>A stateless configuration struct: every instance, its zero value included, is QuantLib's <c>Bilinear()</c>.</remarks>
public readonly struct Bilinear : IInterpolator2D
{
    /// <summary>Builds a <see cref="BilinearInterpolation"/> over the given grid, which it copies.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, which must be sorted; at least 2.</param>
    /// <param name="z">The function values, a row per y value and a column per x value.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="z"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer than 2 x or y values, or the matrix is smaller than the
    /// grid.</exception>
    public Interpolation2D Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix z)
    {
        ArgumentNullException.ThrowIfNull(z);
        return new BilinearInterpolation(x, y, z, nameof(z));
    }
}
