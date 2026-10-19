// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/bicubicsplineinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>Bicubic-spline interpolation between discrete points.</summary>
/// <remarks>
/// <para>
/// The matrix holds a row per y value and a column per x value: the value at (x<sub>i</sub>, y<sub>j</sub>) is
/// <c>zData[j, i]</c>. Each row is a natural cubic spline in x (<see cref="CubicInterpolationDerivativeApprox.Spline"/>,
/// not monotonic, a second derivative of 0 at both ends), built with the interpolation; a value is the natural cubic
/// spline in y through the rows' values at x, built at each call, each spline extrapolating outside its range. The x
/// and y values must be sorted.
/// </para>
/// <para>
/// The five derivatives follow QuantLib's: <see cref="DerivativeX"/> and <see cref="SecondDerivativeX"/> differentiate
/// the spline in x through the values at the x nodes, <see cref="DerivativeY"/> and <see cref="SecondDerivativeY"/>
/// the spline in y of the value, and <see cref="DerivativeXY"/> the spline in x of <see cref="DerivativeY"/> at the
/// nodes. As in QuantLib, they skip the range check <see cref="Interpolation2D.Evaluate"/> makes, and the spline they
/// differentiate refuses a point outside its own range, whatever the extrapolation flag.
/// </para>
/// <para>
/// A matrix larger than the grid is read in its first rows and columns, as QuantLib reads it; a smaller one, which
/// QuantLib reads past its end, is refused. It copies its inputs, as every <see cref="Interpolation2D"/> does, so a
/// change of the caller's matrix is a new interpolation, where QuantLib's reads the matrix by reference until
/// <c>update()</c>.
/// </para>
/// </remarks>
public sealed class BicubicSpline : Interpolation2D
{
    private readonly ImmutableArray<CubicInterpolation> splines;

    /// <summary>Initializes a new instance of the <see cref="BicubicSpline"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, which must be sorted; at least 2.</param>
    /// <param name="zData">The function values, a row per y value and a column per x value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="zData"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer than 2 x or y values, or the matrix has fewer rows than y
    /// values or fewer columns than x values.</exception>
    public BicubicSpline(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix zData)
        : this(x, y, zData, nameof(zData))
    {
    }

    // The constructor, its refusal of the matrix's size naming the caller's parameter for it (the factory's z).
    internal BicubicSpline(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix zData, string zDataName)
        : base(x, y, zData)
    {
        // QuantLib builds a spline over each row's first x values and reads a row per y value, whatever the matrix's
        // size.
        if (!(zData.Rows >= y.Length))
        {
            throw new ArgumentException("Z value matrix has wrong number of rows", zDataName);
        }

        if (!(zData.Columns >= x.Length))
        {
            throw new ArgumentException("Z value matrix has wrong number of columns", zDataName);
        }

        var built = new CubicInterpolation[Y.Length];
        for (int i = 0; i < built.Length; ++i)
        {
            built[i] = Spline(X, ZRow(i)[..X.Length]);
        }

        splines = [.. built];
    }

    /// <summary>The first derivative in x at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException"><paramref name="x"/> is outside the x range.</exception>
    public double DerivativeX(double x, double y) => Spline(X, SectionAtNodes(y)).Derivative(x);

    /// <summary>The first derivative in y at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException"><paramref name="y"/> is outside the y range.</exception>
    public double DerivativeY(double x, double y) => Spline(Y, Section(x)).Derivative(y);

    /// <summary>The second derivative in x at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException"><paramref name="x"/> is outside the x range.</exception>
    public double SecondDerivativeX(double x, double y) => Spline(X, SectionAtNodes(y)).SecondDerivative(x);

    /// <summary>The second derivative in y at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException"><paramref name="y"/> is outside the y range.</exception>
    public double SecondDerivativeY(double x, double y) => Spline(Y, Section(x)).SecondDerivative(y);

    /// <summary>The mixed derivative in x and y at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException"><paramref name="x"/> or <paramref name="y"/> is outside its range.</exception>
    public double DerivativeXY(double x, double y)
    {
        ReadOnlySpan<double> xs = X;
        var section = new double[xs.Length];
        for (int i = 0; i < section.Length; ++i)
        {
            section[i] = DerivativeY(xs[i], y);
        }

        return Spline(xs, section).Derivative(x);
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x, double y) => Spline(Y, Section(x)).Evaluate(y, true);

    // QuantLib's CubicInterpolation(..., Spline, false, SecondDerivative, 0.0, SecondDerivative, 0.0).
    private static CubicInterpolation Spline(ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
        new(
            x,
            y,
            CubicInterpolationDerivativeApprox.Spline,
            false,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0);

    // The rows' splines at x, each extrapolating: QuantLib's section.
    private double[] Section(double x)
    {
        var section = new double[splines.Length];
        for (int i = 0; i < section.Length; i++)
        {
            section[i] = splines[i].Evaluate(x, true);
        }

        return section;
    }

    // The values at the x nodes, at y: QuantLib reads one per matrix column, value(xBegin_[i], y), past the x values
    // when the matrix is wider; here one per x value, its first columns.
    private double[] SectionAtNodes(double y)
    {
        ReadOnlySpan<double> xs = X;
        var section = new double[xs.Length];
        for (int i = 0; i < section.Length; ++i)
        {
            section[i] = ValueCore(xs[i], y);
        }

        return section;
    }
}
