// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/ode/fractionaladams.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>
/// The quadrature weights of the product-trapezoidal Riemann-Liouville integral, which depend only on the order α and
/// the number of grid steps: built once, they serve every integration on that grid, as the rough Heston characteristic
/// function's at each quadrature node.
/// </summary>
/// <remarks>Immutable and thread-safe.</remarks>
public sealed class RiemannLiouvilleWeights
{
    private readonly int steps;
    private readonly double gamma;

    // y[steps] carries weight exactly 1 and isn't stored, so the weights are one shorter than y
    private readonly ImmutableArray<double> w;

    /// <summary>Initializes a new instance of the <see cref="RiemannLiouvilleWeights"/> class.</summary>
    /// <param name="alpha">The integration order, not negative.</param>
    /// <param name="steps">The number of grid steps.</param>
    /// <exception cref="ArgumentException"><paramref name="alpha"/> is negative, or <paramref name="steps"/> isn't
    /// positive.</exception>
    public RiemannLiouvilleWeights(double alpha, int steps)
    {
        if (!(alpha >= 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"integration order alpha ({alpha}) must be non-negative"),
                nameof(alpha));
        }

        if (!(steps > 0))
        {
            throw new ArgumentException("at least one grid step required", nameof(steps));
        }

        Alpha = alpha;
        this.steps = steps;
        gamma = new GammaFunction().Value(alpha + 2.0);
        var weights = new double[steps];

        // powers m ^ (alpha + 1) for m = 0, ..., steps + 1, reused across the
        // shifted (k - 1, k, k + 1) terms in the weights below
        var powAlphaPlus1 = new double[steps + 2];
        for (int m = 0; m <= steps + 1; ++m)
        {
            powAlphaPlus1[m] = Math.Pow(m, alpha + 1.0);
        }

        // weight of y_0: (n - 1) ^ (alpha + 1) - (n - 1 - alpha) n ^ alpha
        weights[0] = powAlphaPlus1[steps - 1] - ((steps - 1 - alpha) * Math.Pow(steps, alpha));
        for (int j = 1; j < steps; ++j)
        {
            int k = steps - j;
            weights[j] = powAlphaPlus1[k + 1] + powAlphaPlus1[k - 1] - (2.0 * powAlphaPlus1[k]);
        }

        w = ImmutableArray.Create(weights);
    }

    /// <summary>Gets the integration order α.</summary>
    public double Alpha { get; }

    /// <summary>I^α y(t_N), for the values of y on a grid of spacing <paramref name="dt"/>.</summary>
    /// <typeparam name="T">The values' type: <see cref="double"/>, or <see cref="Complex"/>.</typeparam>
    /// <param name="y">The values on the grid, one more than the steps.</param>
    /// <param name="dt">The grid's spacing.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentException">The values aren't one more than the steps, or <paramref name="dt"/> isn't
    /// positive.</exception>
    public T Integrate<T>(ReadOnlySpan<T> y, double dt)
        where T : INumberBase<T>
    {
        if (!(y.Length == steps + 1))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"grid size ({y.Length}) does not match the weights ({steps + 1} values expected)"),
                nameof(y));
        }

        if (!(dt > 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"grid spacing dt ({dt}) must be positive"), nameof(dt));
        }

        T sum = T.Zero;
        for (int j = 0; j < steps; ++j)
        {
            sum += FractionalAdams<T>.Scale(w[j], y[j]);
        }

        sum += y[steps];
        return FractionalAdams<T>.Scale(Math.Pow(dt, Alpha) / gamma, sum);
    }
}
