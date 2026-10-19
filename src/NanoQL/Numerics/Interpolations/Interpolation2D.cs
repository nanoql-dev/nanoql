// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/interpolation2d.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The base class for 2-D interpolations: interpolated values from two sequences of lengths N and M, the discretized
/// values of the x and y variables, and a matrix of the tabulated function values.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: an interpolation copies its grid and its matrix and computes what it needs when it is built. QuantLib's
/// keep iterators and a reference into their caller's data and recompute on <c>update()</c>; here a change of data is
/// a new interpolation, so <c>update()</c> isn't ported. Extrapolation follows <see cref="Extrapolator"/>:
/// <see cref="ExtrapolatorExtensions.EnableExtrapolation{T}"/> returns a copy.
/// </para>
/// <para>
/// An extension point, as in QuantLib: a derived interpolation passes its inputs to the base constructor, computes what
/// it needs from <see cref="X"/>, <see cref="Y"/> and <see cref="ZRow"/>, and implements <see cref="ValueCore"/>, which
/// <see cref="Evaluate"/> calls after the range check. The x and y values must be sorted; an unsorted grid is located as
/// QuantLib's binary search locates it.
/// </para>
/// </remarks>
public abstract class Interpolation2D : Extrapolator
{
    private readonly ImmutableArray<double> xValues;
    private readonly ImmutableArray<double> yValues;

    // A private copy, never exposed.
    private readonly Matrix zData;

    /// <summary>
    /// Initializes a new instance of the <see cref="Interpolation2D"/> class from copies of its inputs: QuantLib's
    /// <c>templateImpl</c>.
    /// </summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, which must be sorted; at least 2.</param>
    /// <param name="zData">The function values; how a derived interpolation reads them is its own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="zData"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer than 2 x or y values.</exception>
    protected Interpolation2D(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix zData)
    {
        ArgumentNullException.ThrowIfNull(zData);
        if (!(x.Length >= 2))
        {
            throw new ArgumentException(
                $"not enough x points to interpolate: at least 2 required, {x.Length} provided", nameof(x));
        }

        if (!(y.Length >= 2))
        {
            throw new ArgumentException(
                $"not enough y points to interpolate: at least 2 required, {y.Length} provided", nameof(y));
        }

        xValues = [.. x];
        yValues = [.. y];
        this.zData = zData.Clone();
    }

    // The grid of another interpolation, shared: it is immutable. For a decorator, whose grid is the decorated one's.
    private protected Interpolation2D(Interpolation2D grid)
    {
        xValues = grid.xValues;
        yValues = grid.yValues;
        zData = grid.zData;
    }

    /// <summary>Gets the first x value, the smallest of the sorted values.</summary>
    public double XMin => xValues[0];

    /// <summary>Gets the last x value, the largest of the sorted values.</summary>
    public double XMax => xValues[^1];

    /// <summary>Gets the first y value, the smallest of the sorted values.</summary>
    public double YMin => yValues[0];

    /// <summary>Gets the last y value, the largest of the sorted values.</summary>
    public double YMax => yValues[^1];

    /// <summary>Gets the x values, as stored.</summary>
    protected ReadOnlySpan<double> X => xValues.AsSpan();

    /// <summary>Gets the y values, as stored.</summary>
    protected ReadOnlySpan<double> Y => yValues.AsSpan();

    /// <summary>The x values.</summary>
    /// <returns>The values the interpolation was built from.</returns>
    public ImmutableArray<double> XValues() => xValues;

    /// <summary>The y values.</summary>
    /// <returns>The values the interpolation was built from.</returns>
    public ImmutableArray<double> YValues() => yValues;

    /// <summary>The function values.</summary>
    /// <returns>A copy of the matrix the interpolation was built from; changing it changes nothing.</returns>
    public Matrix ZData() => zData.Clone();

    /// <summary>
    /// The index i of the interval [x<sub>i</sub>, x<sub>i+1</sub>] holding <paramref name="x"/>: 0 below the range,
    /// the last interval above it.
    /// </summary>
    /// <param name="x">The point.</param>
    /// <returns>The interval's index.</returns>
    public int LocateX(double x)
    {
        ReadOnlySpan<double> xs = X;
        if (x < xs[0])
        {
            return 0;
        }
        else if (x > xs[^1])
        {
            return xs.Length - 2;
        }
        else
        {
            return Interpolation.UpperBound(xs[..^1], x) - 1;
        }
    }

    /// <summary>
    /// The index j of the interval [y<sub>j</sub>, y<sub>j+1</sub>] holding <paramref name="y"/>: 0 below the range,
    /// the last interval above it.
    /// </summary>
    /// <param name="y">The point.</param>
    /// <returns>The interval's index.</returns>
    public int LocateY(double y)
    {
        ReadOnlySpan<double> ys = Y;
        if (y < ys[0])
        {
            return 0;
        }
        else if (y > ys[^1])
        {
            return ys.Length - 2;
        }
        else
        {
            return Interpolation.UpperBound(ys[..^1], y) - 1;
        }
    }

    /// <summary>
    /// Whether (<paramref name="x"/>, <paramref name="y"/>) is in the grid's range: each coordinate in its range, or
    /// close to an end.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns><see langword="true"/> when in range.</returns>
    public bool IsInRange(double x, double y)
    {
        double x1 = XMin, x2 = XMax;
        bool xIsInrange = (x >= x1 && x <= x2) || ComparisonFunctions.Close(x, x1) || ComparisonFunctions.Close(x, x2);
        if (!xIsInrange)
        {
            return false;
        }

        double y1 = YMin, y2 = YMax;
        return (y >= y1 && y <= y2) || ComparisonFunctions.Close(y, y1) || ComparisonFunctions.Close(y, y2);
    }

    /// <summary>The interpolated value at (<paramref name="x"/>, <paramref name="y"/>): QuantLib's <c>operator()</c>.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="allowExtrapolation">Whether this call may extrapolate outside the range.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NanoQLException">The point is out of range and extrapolation isn't allowed.</exception>
    public double Evaluate(double x, double y, bool allowExtrapolation = false)
    {
        CheckRange(x, y, allowExtrapolation);
        return ValueCore(x, y);
    }

    /// <summary>A row of the function values, as stored: QuantLib's <c>zData_[i]</c>.</summary>
    /// <param name="i">The row's index.</param>
    /// <returns>The row.</returns>
    protected ReadOnlySpan<double> ZRow(int i) => zData.Row(i);

    /// <summary>The interpolated value at a point, after the range check.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The value.</returns>
    protected abstract double ValueCore(double x, double y);

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    private void CheckRange(double x, double y, bool extrapolate)
    {
        if (!(extrapolate || AllowsExtrapolation || IsInRange(x, y)))
        {
            throw new NanoQLException(
                $"interpolation range is [{Show(XMin)}, {Show(XMax)}] x [{Show(YMin)}, {Show(YMax)}]: "
                + $"extrapolation at ({Show(x)}, {Show(y)}) not allowed");
        }
    }
}
