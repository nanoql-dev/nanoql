// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/bilinearinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Bilinear interpolation between discrete points.</summary>
/// <remarks>
/// The matrix holds a row per y value and a column per x value: the value at (x<sub>i</sub>, y<sub>j</sub>) is
/// <c>zData[j, i]</c>. Outside the grid the value extends the nearest cell's bilinear form. A matrix larger than the
/// grid is read in its first rows and columns, as QuantLib's is; a smaller one, which QuantLib reads past its end, is
/// refused. It copies its inputs, as every <see cref="Interpolation2D"/> does.
/// </remarks>
public sealed class BilinearInterpolation : Interpolation2D
{
    /// <summary>Initializes a new instance of the <see cref="BilinearInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, which must be sorted; at least 2.</param>
    /// <param name="zData">The function values, a row per y value and a column per x value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="zData"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer than 2 x or y values, or the matrix has fewer rows than y
    /// values or fewer columns than x values.</exception>
    public BilinearInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix zData)
        : this(x, y, zData, nameof(zData))
    {
    }

    // The constructor, its refusal of the matrix's size naming the caller's parameter for it (the factory's z).
    internal BilinearInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix zData, string zDataName)
        : base(x, y, zData)
    {
        // QuantLib reads zData_[j+1][i+1] for the grid's last cell, whatever the matrix's size.
        if (!(zData.Rows >= y.Length))
        {
            throw new ArgumentException("Z value matrix has wrong number of rows", zDataName);
        }

        if (!(zData.Columns >= x.Length))
        {
            throw new ArgumentException("Z value matrix has wrong number of columns", zDataName);
        }
    }

    /// <inheritdoc/>
    protected override double ValueCore(double x, double y)
    {
        int i = LocateX(x), j = LocateY(y);

        double z1 = ZRow(j)[i];
        double z2 = ZRow(j)[i + 1];
        double z3 = ZRow(j + 1)[i];
        double z4 = ZRow(j + 1)[i + 1];

        double t = (x - X[i]) / (X[i + 1] - X[i]);
        double u = (y - Y[j]) / (Y[j + 1] - Y[j]);

        return ((1.0 - t) * (1.0 - u) * z1) + (t * (1.0 - u) * z2) + ((1.0 - t) * u * z3) + (t * u * z4);
    }
}
