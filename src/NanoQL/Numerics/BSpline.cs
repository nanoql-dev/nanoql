// SPDX-FileCopyrightText: Copyright (C) 2007 Allen Kuo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/bspline.hpp ql/math/bspline.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>
/// The B-spline basis functions N_{i,p}(x) of degree p, i = 0, 1, …, n, over a knot vector of p + n + 2 nondecreasing
/// points (x_0, x_1, …, x_{n+p+1}). Follows the treatment and notation of Weisstein, Eric W. "B-Spline." From MathWorld,
/// a Wolfram Web Resource, http://mathworld.wolfram.com/B-Spline.html.
/// </summary>
/// <remarks>
/// <para>
/// A linear B-spline has p = 1, a quadratic one p = 2, a cubic one p = 3; n + 1 is the number of control points. The
/// basis functions are defined recursively: N_{i,0}(x) is 1 if x_i ≤ x &lt; x_{i+1} and 0 otherwise, and
/// N_{i,p}(x) = N_{i,p−1}(x)·(x − x_i)/(x_{i+p} − x_i) + N_{i+1,p−1}(x)·(x_{i+p+1} − x)/(x_{i+p+1} − x_{i+1}).
/// Immutable: the knots are copied.
/// </para>
/// <para>
/// On a repeated knot one of those spans is zero, and QuantLib's recursion divides by it, giving NaN, so a cubic
/// B-spline fit on knots with a repeat throws. Here a term whose span is zero reads 0, the Cox–de Boor convention, the
/// limit of QuantLib's own values as the repeated knots close up; on distinct knots it is QuantLib's arithmetic and
/// bits. A documented divergence.
/// </para>
/// </remarks>
public sealed class BSpline
{
    // p is the degree: p = 2 is a quadratic B-spline, p = 3 a cubic one, etc.
    private readonly int p;

    // n + 1 = "control points" = max number of basis functions
    private readonly int n;
    private readonly ImmutableArray<double> knots;

    /// <summary>Initializes a new instance of the <see cref="BSpline"/> class.</summary>
    /// <param name="p">The degree, at least 1, QuantLib's <c>Natural</c>.</param>
    /// <param name="n">The number of control points less one, at least 1 and at least <paramref name="p"/>.</param>
    /// <param name="knots">The knots, p + n + 2 of them, nondecreasing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="knots"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The degree is below 1, n is below 1 or below the degree, the number of knots isn't p + n + 2, or the knots
    /// decrease.
    /// </exception>
    public BSpline(int p, int n, IEnumerable<double> knots)
    {
        ArgumentNullException.ThrowIfNull(knots);
        ImmutableArray<double> k = [.. knots];
        if (!(p >= 1))
        {
            throw new ArgumentException("lowest degree B-spline has p = 1", nameof(p));
        }

        if (!(n >= 1))
        {
            throw new ArgumentException("number of control points n+1 >= 2", nameof(n));
        }

        if (!(p <= n))
        {
            throw new ArgumentException("must have p <= n", nameof(p));
        }

        if (!(k.Length == p + n + 2))
        {
            throw new ArgumentException("number of knots must equal p+n+2", nameof(knots));
        }

        for (int i = 0; i < k.Length - 1; ++i)
        {
            if (!(k[i] <= k[i + 1]))
            {
                throw new ArgumentException("knots points must be nondecreasing", nameof(knots));
            }
        }

        this.p = p;
        this.n = n;
        this.knots = k;
    }

    /// <summary>The i-th basis function at a point.</summary>
    /// <param name="i">The basis function's index, from 0 to n.</param>
    /// <param name="x">The point.</param>
    /// <returns>N_{i,p}(x).</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative or greater than n.</exception>
    public double Evaluate(int i, double x)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        if (!(i <= n))
        {
            throw new ArgumentOutOfRangeException(nameof(i), i, "i must not be greater than n");
        }

        return N(i, p, x);
    }

    // recursive definition of N, the B-spline basis function; a term over a zero span reads 0
    private double N(int i, int p, double x)
    {
        if (p == 0)
        {
            return (knots[i] <= x && x < knots[i + 1]) ? 1.0 : 0.0;
        }

        double leftSpan = knots[i + p] - knots[i];
        double rightSpan = knots[i + p + 1] - knots[i + 1];
        double left = leftSpan == 0.0 ? 0.0 : ((x - knots[i]) / leftSpan) * N(i, p - 1, x);
        double right = rightSpan == 0.0 ? 0.0 : ((knots[i + p + 1] - x) / rightSpan) * N(i + 1, p - 1, x);
        return left + right;
    }
}
