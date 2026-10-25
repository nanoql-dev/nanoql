// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/ode/fractionaladams.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>The free functions of QuantLib's <c>fractionaladams.hpp</c>.</summary>
public static class FractionalAdamsFunctions
{
    /// <summary>
    /// The product-trapezoidal Riemann-Liouville fractional integral I^α y(t_N) = (1/Γ(α)) ∫₀^{t_N} (t_N − s)^{α−1} y(s) ds,
    /// α ≥ 0, from the values of y on a uniform grid, by integrating y's piecewise linear interpolant against the kernel:
    /// at α = 1 the trapezoidal rule, and the identity as α falls to 0.
    /// </summary>
    /// <typeparam name="T">The values' type: <see cref="double"/>, or <see cref="Complex"/>.</typeparam>
    /// <param name="y">The values on the grid, at least two.</param>
    /// <param name="alpha">The integration order, not negative.</param>
    /// <param name="dt">The grid's spacing.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentException"><paramref name="alpha"/> is negative, there are fewer than two values, or
    /// <paramref name="dt"/> isn't positive.</exception>
    /// <remarks>It builds the weights at every call; integrations on one (α, N) grid can share a
    /// <see cref="RiemannLiouvilleWeights"/>.</remarks>
    public static T RiemannLiouvilleIntegral<T>(ReadOnlySpan<T> y, double alpha, double dt)
        where T : INumberBase<T>
    {
        if (!(alpha >= 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"integration order alpha ({alpha}) must be non-negative"),
                nameof(alpha));
        }

        if (!(y.Length >= 2))
        {
            throw new ArgumentException("at least two grid values required", nameof(y));
        }

        if (!(dt > 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"grid spacing dt ({dt}) must be positive"), nameof(dt));
        }

        return new RiemannLiouvilleWeights(alpha, y.Length - 1).Integrate(y, dt);
    }
}
