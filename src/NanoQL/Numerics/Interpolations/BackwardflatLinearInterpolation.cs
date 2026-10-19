// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/backwardflatlinearinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A 2-D interpolation backward flat in x and linear in y: between two x values the value is the next x value's, at an
/// x value its own, and between two y values it is interpolated linearly.
/// </summary>
/// <remarks>
/// The matrix holds a row per y value and a column per x value: the value at (x<sub>i</sub>, y<sub>j</sub>) is
/// <c>zData[j, i]</c>. Below the first x value the value is the first column's, above the last the last column's;
/// outside the y values it extends the nearest interval's linear form. A matrix larger than the grid is read in its first
/// rows and columns, as QuantLib's is; a smaller one, which QuantLib reads past its end, is refused, as
/// <see cref="BilinearInterpolation"/> refuses it. It copies its inputs, as every <see cref="Interpolation2D"/> does.
/// </remarks>
public sealed class BackwardflatLinearInterpolation : Interpolation2D
{
    /// <summary>Initializes a new instance of the <see cref="BackwardflatLinearInterpolation"/> class.</summary>
    /// <param name="x">The x values, which must be sorted; at least 2.</param>
    /// <param name="y">The y values, which must be sorted; at least 2.</param>
    /// <param name="zData">The function values, a row per y value and a column per x value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="zData"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer than 2 x or y values, or the matrix has fewer rows than y
    /// values or fewer columns than x values.</exception>
    public BackwardflatLinearInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix zData)
        : this(x, y, zData, nameof(zData))
    {
    }

    // The constructor, its refusal of the matrix's size naming the caller's parameter for it (the factory's z).
    internal BackwardflatLinearInterpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y, Matrix zData, string zDataName)
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
        int j = LocateY(y);
        double z1, z2;
        if (x <= X[0])
        {
            z1 = ZRow(j)[0];
            z2 = ZRow(j + 1)[0];
        }
        else
        {
            int i = LocateX(x);
            if (x == X[i])
            {
                z1 = ZRow(j)[i];
                z2 = ZRow(j + 1)[i];
            }
            else
            {
                z1 = ZRow(j)[i + 1];
                z2 = ZRow(j + 1)[i + 1];
            }
        }

        double u = (y - Y[j]) / (Y[j + 1] - Y[j]);

        return ((1.0 - u) * z1) + (u * z2);
    }
}
