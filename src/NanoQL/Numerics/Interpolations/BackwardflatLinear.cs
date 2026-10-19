// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/backwardflatlinearinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The factory of the 2-D interpolation backward flat in x and linear in y.</summary>
/// <remarks>
/// A stateless configuration struct: every instance, its zero value included, is QuantLib's <c>BackwardflatLinear()</c>.
/// </remarks>
public readonly struct BackwardflatLinear : IInterpolator2D
{
    /// <summary>Builds a <see cref="BackwardflatLinearInterpolation"/> over the given grid, which it copies.</summary>
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
        return new BackwardflatLinearInterpolation(x, y, z, nameof(z));
    }
}
