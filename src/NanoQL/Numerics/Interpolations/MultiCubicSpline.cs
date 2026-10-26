// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Roman Gitlin
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/multicubicspline.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>N-dimensional cubic spline interpolation between discrete points, in 1 to 15 dimensions.</summary>
/// <remarks>
/// <para>
/// QuantLib's recursion: at construction a natural cubic spline's second derivatives along the last dimension, for
/// every line of it; at each evaluation the splines of the lower dimensions through the values the higher ones
/// interpolate, the first dimension last, each step's arithmetic and order QuantLib's, so the values are QuantLib's.
/// </para>
/// <para>
/// Immutable and safe to share across threads: the grid and the values are copied, and each evaluation computes
/// QuantLib's coefficients in scratch of its own, where QuantLib's spline holds references to its inputs and
/// evaluates through <c>mutable</c> members. The dimension is the grid's, read at construction, where QuantLib's is a
/// template parameter.
/// </para>
/// <para>
/// The last grid point of a dimension is interpolated on its last interval, where QuantLib refuses it as
/// extrapolation (its documented bug, "cannot interpolate at the grid points on the boundary surface"), and with
/// extrapolation allowed a point beyond it reads the last value, where QuantLib's flat value reads one value past the
/// data; an argument that isn't a number is refused, where QuantLib's search reads past the grid
/// (<c>Divergences/MultiCubicSplineBoundaryTests.cs</c>).
/// </para>
/// </remarks>
public sealed class MultiCubicSpline
{
    /// <summary>The largest dimension, QuantLib's fifteen splines.</summary>
    public const int MaxDimension = 15;

    private readonly ImmutableArray<ImmutableArray<double>> grid;
    private readonly double[] values;
    private readonly double[] secondDerivatives;
    private readonly bool[] extrapolation;
    private readonly int[] sizes;
    private readonly int[] strides;
    private readonly double[][] d;
    private readonly double[][] d2;

    /// <summary>Initializes a new instance of the <see cref="MultiCubicSpline"/> class.</summary>
    /// <param name="grid">The grid, one increasing list of at least four points per dimension.</param>
    /// <param name="values">
    /// The values at the grid's points, the first dimension slowest (QuantLib's nested data table, flattened), copied.
    /// </param>
    /// <param name="extrapolation">
    /// Whether each dimension extrapolates flat beyond the grid; empty, QuantLib's default, for none.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The grid's dimension isn't 1 to 15, a dimension is empty, holds fewer than four points or isn't increasing,
    /// the values don't fill the grid, or the extrapolation flags aren't one per dimension.
    /// </exception>
    public MultiCubicSpline(
        ImmutableArray<ImmutableArray<double>> grid, ReadOnlySpan<double> values, ReadOnlySpan<bool> extrapolation = default)
    {
        if (grid.IsDefault || grid.Length < 1 || grid.Length > MaxDimension)
        {
            throw new ArgumentException(
                $"the grid's dimension ({(grid.IsDefault ? 0 : grid.Length)}) must be 1 to {MaxDimension}", nameof(grid));
        }

        int n = grid.Length;
        if (!(extrapolation.IsEmpty || extrapolation.Length == n))
        {
            // QuantLib reads the flags by dimension, unchecked
            throw new ArgumentException(
                $"{extrapolation.Length} extrapolation flags given for {n} dimensions", nameof(extrapolation));
        }

        this.extrapolation = extrapolation.IsEmpty ? new bool[n] : extrapolation.ToArray();

        // QuantLib's set_shared_increments: each dimension's increments and their doubled sums
        sizes = new int[n];
        d = new double[n][];
        d2 = new double[n][];
        for (int j = 0; j < n; ++j)
        {
            ImmutableArray<double> v = grid[j];
            if (v.IsDefaultOrEmpty)
            {
                // QuantLib's v.size() - 1 wraps, and the increments' vector can't be allocated
                throw new ArgumentException($"Dimension {j} : empty grid", nameof(grid));
            }

            int dim = v.Length - 1;
            int k = 0;
            d[j] = [];
            d2[j] = [];
            if (dim > 2)
            {
                d[j] = new double[dim];
                d2[j] = new double[dim - 1];
                for (; k < dim; ++k)
                {
                    if ((d[j][k] = v[k + 1] - v[k]) <= 0.0)
                    {
                        break;
                    }

                    if (k != 0)
                    {
                        d2[j][k - 1] = 2.0 * (v[k + 1] - v[k - 1]);
                    }
                }
            }

            if (!(dim >= 3))
            {
                throw new ArgumentException($"Dimension {j} : not enough points for interpolation", nameof(grid));
            }

            if (!(k >= dim))
            {
                throw new ArgumentException($"Dimension {j} : invalid data", nameof(grid));
            }

            sizes[j] = v.Length;
        }

        strides = new int[n];
        int total = 1;
        for (int j = n - 1; j >= 0; --j)
        {
            strides[j] = total;
            total *= sizes[j];
        }

        if (!(values.Length == total))
        {
            throw new ArgumentException(
                $"{values.Length} values given for a grid of {total} points", nameof(values));
        }

        this.grid = grid;
        this.values = values.ToArray();

        // QuantLib's c_spline: the natural spline's second derivatives along the last dimension, line by line
        secondDerivatives = new double[total];
        int last = sizes[n - 1];
        double[] scratch = new double[last];
        for (int offset = 0; offset < total; offset += last)
        {
            BaseCubicSpline(
                d[n - 1], d2[n - 1], this.values.AsSpan(offset, last), secondDerivatives.AsSpan(offset, last), scratch);
        }
    }

    /// <summary>Gets the dimension.</summary>
    public int Dimension => sizes.Length;

    /// <summary>Gets the grid.</summary>
    public ImmutableArray<ImmutableArray<double>> Grid => grid;

    /// <summary>The interpolated value at a point.</summary>
    /// <param name="x">The point, one coordinate per dimension.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> doesn't hold one coordinate per dimension, a coordinate isn't a number, or lies outside
    /// its dimension's grid where that dimension doesn't extrapolate.
    /// </exception>
    public double Evaluate(ReadOnlySpan<double> x)
    {
        int n = sizes.Length;
        if (!(x.Length == n))
        {
            // QuantLib's argument reads one coordinate per dimension, unchecked
            throw new ArgumentException($"{x.Length} coordinates given for {n} dimensions", nameof(x));
        }

        // QuantLib's set_shared_coefficients, into this call's scratch
        double[] a = new double[n], b = new double[n], a2 = new double[n], b2 = new double[n];
        int[] index = new int[n];
        for (int j = 0; j < n; ++j)
        {
            ImmutableArray<double> v = grid[j];
            int sz = v.Length - 1;
            double xj = x[j];
            if (double.IsNaN(xj))
            {
                // QuantLib's comparisons are false for a NaN, and its search reads past the grid
                throw new ArgumentException($"Dimension {j}: the coordinate is not a number", nameof(x));
            }

            if (xj < v[0] || xj > v[sz])
            {
                if (!extrapolation[j])
                {
                    throw new ArgumentException($"Dimension {j}: extrapolation is not allowed.", nameof(x));
                }

                if (xj < v[0])
                {
                    a[j] = 1.0;
                    a2[j] = b[j] = b2[j] = 0.0;
                    index[j] = 0;
                }
                else
                {
                    // the last value, on the last interval, where QuantLib's flat value reads past the data
                    b[j] = 1.0;
                    a2[j] = a[j] = b2[j] = 0.0;
                    index[j] = sz - 1;
                }
            }
            else
            {
                // std::upper_bound(v, x) - 1; the last point on the last interval, where QuantLib refuses it
                int k = xj == v[sz] ? sz - 1 : UpperBound(v, xj) - 1;
                double h = v[k + 1] - v[k];
                a[j] = (v[k + 1] - xj) / h;
                b[j] = (xj - v[k]) / h;
                a2[j] = ((a[j] * a[j] * a[j]) - a[j]) * h * h / 6.0;
                b2[j] = ((b[j] * b[j] * b[j]) - b[j]) * h * h / 6.0;
                index[j] = k;
            }
        }

        // QuantLib's output data: each level's values, second derivatives and spline scratch
        double[][] v1 = new double[n][], v2 = new double[n][], scratch = new double[n][];
        for (int j = 0; j < n; ++j)
        {
            v1[j] = new double[sizes[j]];
            v2[j] = new double[sizes[j]];
            scratch[j] = new double[sizes[j]];
        }

        return Splint(0, 0, a, b, a2, b2, index, v1, v2, scratch);
    }

    // QuantLib's base_cubic_spline: a natural cubic spline's second derivatives y2 through y on increments dx and their
    // doubled sums dx2, in its order, with v its scratch (v[0] is never written, so it reads 0).
    private static void BaseCubicSpline(
        ReadOnlySpan<double> dx, ReadOnlySpan<double> dx2, ReadOnlySpan<double> y, Span<double> y2, Span<double> v)
    {
        int dim = dx.Length;
        int j = 1, k = 2, l = 3;

        // w, u and t are QuantLib's references to y2[dim], y2[0] and v[dim]
        double w = (y[1] - y[0]) / dx[0];
        double u = (y[2] - y[1]) / dx[1];
        double t;
        y2[1] = -dx[1] / dx2[0];
        v[1] = 6.0 * (u - w) / dx2[0];
        for (; k < dim; u = w, j = k, k = l, ++l)
        {
            w = (y[l] - y[k]) / dx[k];
            u = (u - w) * 6.0;
            t = -y2[j];
            t *= dx[j];
            t -= dx2[j];
            y2[k] = dx[k] / t;
            u += dx[j] * v[j];
            v[k] = u / t;
        }

        y2[0] = y2[dim] = 0.0;
        while (k != 0)
        {
            y2[k - 1] = (y2[k - 1] * y2[l - 1]) + v[k - 1];
            --k;
            --l;
        }
    }

    // std::upper_bound: the first index whose point is greater than x
    private static int UpperBound(ImmutableArray<double> v, double x)
    {
        int lo = 0, count = v.Length;
        while (count > 0)
        {
            int step = count / 2;
            int it = lo + step;
            if (!(x < v[it]))
            {
                lo = it + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return lo;
    }

    // QuantLib's n_cubic_splint at a level, over the values at an offset: the higher levels' values interpolated into
    // this level's buffer, the spline through them, and the cubic at this level's coordinate; base_cubic_splint at the
    // last level, on the stored values and second derivatives.
    private double Splint(
        int level,
        int offset,
        double[] a,
        double[] b,
        double[] a2,
        double[] b2,
        int[] index,
        double[][] v1,
        double[][] v2,
        double[][] scratch)
    {
        int i = index[level];
        if (level == sizes.Length - 1)
        {
            return (a[level] * values[offset + i]) + (b[level] * values[offset + i + 1])
                + (a2[level] * secondDerivatives[offset + i]) + (b2[level] * secondDerivatives[offset + i + 1]);
        }

        double[] y = v1[level];
        for (int j = 0; j < sizes[level]; ++j)
        {
            y[j] = Splint(level + 1, offset + (j * strides[level]), a, b, a2, b2, index, v1, v2, scratch);
        }

        double[] y2 = v2[level];
        BaseCubicSpline(d[level], d2[level], y, y2, scratch[level]);
        return (a[level] * y[i]) + (b[level] * y[i + 1]) + (a2[level] * y2[i]) + (b2[level] * y2[i + 1]);
    }
}
