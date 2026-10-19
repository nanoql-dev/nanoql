// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A bilinear or bicubic-spline interpolation over a matrix smaller than its grid. QuantLib checks no matrix against the
/// grid (<c>interpolation2d.hpp:72-83</c>): its bilinear value reads <c>zData_[j+1][i+1]</c> for the cell holding the
/// point (<c>bilinearinterpolation.hpp:50-53</c>), and its bicubic spline builds a row spline over each row's first x
/// values and reads a row per y value (<c>bicubicsplineinterpolation.hpp:58-79</c>), past the matrix's end for a matrix
/// with fewer rows than y values or fewer columns than x values, undefined: the wheel's bicubic spline over a 2 by 2
/// matrix on a 3 by 3 grid gives 3.337890625 at (0.25, 0.25). nanoql refuses such a matrix when the interpolation is
/// built, with <see cref="ArgumentException"/> naming the matrix's parameter (<c>zData</c>, and the factories'
/// <c>z</c>) and QuantLib's own texts for a mismatched matrix (<c>kernelinterpolation2d.hpp:70-73</c>). A larger matrix,
/// which QuantLib reads in its first rows and columns, is kept; the bicubic spline's derivatives in x, which QuantLib
/// computes at an x value per matrix column (<c>bicubicsplineinterpolation.hpp:81-108, 137-149</c>), reading the x values
/// past their end for a wider matrix, read its first columns, an x value each.
/// </summary>
public class Interpolation2DSizeTests
{
    private static readonly double[] X = [1.0, 2.0, 3.0];
    private static readonly double[] Y = [10.0, 20.0];

    [Fact]
    public void AMatrixSmallerThanTheGridIsRefused()
    {
        AssertRefused("zData", "Z value matrix has wrong number of rows", () => new BilinearInterpolation(X, Y, new Matrix(1, 3, 1.0)));
        AssertRefused("zData", "Z value matrix has wrong number of columns", () => new BilinearInterpolation(X, Y, new Matrix(2, 2, 1.0)));

        // The factory's refusal names its own parameter.
        AssertRefused("z", "Z value matrix has wrong number of rows", () => new Bilinear().Interpolate(X, Y, new Matrix(1, 1, 1.0)));

        // The transposed matrix, a row per x value, is smaller in its columns: refused, where QuantLib reads past it.
        AssertRefused("zData", "Z value matrix has wrong number of columns", () => new BilinearInterpolation(X, Y, new Matrix(3, 2, 1.0)));

        // A matrix as large as the grid, or larger, is read as QuantLib reads it: its first rows and columns.
        var larger = new Matrix(3, 4, 99.0);
        double[][] z = [[110.0, 210.0, 310.0], [120.0, 220.0, 320.0]];
        for (int j = 0; j < 2; j++)
        {
            for (int i = 0; i < 3; i++)
            {
                larger[j, i] = z[j][i];
            }
        }

        var f = new BilinearInterpolation(X, Y, larger);
        Assert.Equal(215.0, f.Evaluate(2.0, 15.0));
        Assert.Equal(320.0, f.Evaluate(3.0, 20.0));

        // The bicubic spline, and its factory, refuse alike, and read a larger matrix's first rows and columns.
        AssertRefused("zData", "Z value matrix has wrong number of rows", () => new BicubicSpline(X, Y, new Matrix(1, 3, 1.0)));
        AssertRefused("zData", "Z value matrix has wrong number of columns", () => new BicubicSpline(X, Y, new Matrix(2, 2, 1.0)));
        AssertRefused("z", "Z value matrix has wrong number of columns", () => new Bicubic().Interpolate(X, Y, new Matrix(3, 2, 1.0)));
        var exact = new Matrix(new[,] { { 110.0, 210.0, 310.0 }, { 120.0, 220.0, 320.0 } });
        var spline = new BicubicSpline(X, Y, larger);
        var exactSpline = new BicubicSpline(X, Y, exact);
        Assert.Equal(exactSpline.Evaluate(2.5, 12.0), spline.Evaluate(2.5, 12.0));
        Assert.Equal(exactSpline.DerivativeX(2.5, 12.0), spline.DerivativeX(2.5, 12.0));
        Assert.Equal(exactSpline.DerivativeXY(2.5, 12.0), spline.DerivativeXY(2.5, 12.0));
    }

    private static void AssertRefused(string paramName, string message, Action build)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(build);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
