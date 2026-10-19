// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A bilinear interpolation over a matrix smaller than its grid. QuantLib checks no matrix against the grid
/// (<c>interpolation2d.hpp:72-83</c>), and its bilinear value reads <c>zData_[j+1][i+1]</c> for the cell holding the point
/// (<c>bilinearinterpolation.hpp:50-53</c>), past the matrix's end for a matrix with fewer rows than y values or fewer
/// columns than x values, undefined. nanoql refuses such a matrix when the interpolation is built, with
/// <see cref="ArgumentException"/> naming the matrix's parameter (<c>zData</c>, and the factory's <c>z</c>) and
/// QuantLib's own texts for a mismatched matrix (<c>kernelinterpolation2d.hpp:70-73</c>). A larger matrix, which
/// QuantLib reads in its first rows and columns, is kept.
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
    }

    private static void AssertRefused(string paramName, string message, Action build)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(build);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
