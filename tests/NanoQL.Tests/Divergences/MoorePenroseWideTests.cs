// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using static NanoQL.Numerics.MatrixFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Moore-Penrose inverse of a wide matrix (the S-xrt plan's XRR5 (xxxv), fixed under R8). QuantLib's reads the
/// singular values past their end and then throws "matrices with different sizes (3x2, 3x3) cannot be multiplied" on
/// a 2 × 3 matrix (the S-xrt planner's XRP9). nanoql's is ((Aᵀ)⁺)ᵀ, Penrose's identity; on wide, tall,
/// rank-deficient and zero matrices it meets Penrose's four conditions, A A⁺ A = A, A⁺ A A⁺ = A⁺ and A A⁺, A⁺ A
/// symmetric.
/// </summary>
public class MoorePenroseWideTests
{
    public static readonly TheoryData<string, double[,]> Matrices = new()
    {
        { "wide 2 x 3", new double[,] { { 1.0, 2.0, 3.0 }, { 4.0, 5.0, 6.5 } } },
        { "wide rank-deficient 2 x 4", new double[,] { { 1.0, 2.0, 0.0, -1.0 }, { 2.0, 4.0, 0.0, -2.0 } } },
        { "tall 4 x 2", new double[,] { { 1.0, 0.5 }, { 0.0, 2.0 }, { 3.0, -1.0 }, { 1.0, 1.0 } } },
        { "tall rank-deficient 3 x 3", new double[,] { { 1.0, 2.0, 3.0 }, { 2.0, 4.0, 6.0 }, { 0.0, 1.0, 1.0 } } },
        { "zero 2 x 3", new double[,] { { 0.0, 0.0, 0.0 }, { 0.0, 0.0, 0.0 } } },
    };

    [Theory]
    [MemberData(nameof(Matrices))]
    public void ThePenroseConditionsHold(string name, double[,] a)
    {
        var A = new Matrix(a);
        Matrix P = MoorePenroseInverseFunctions.MoorePenroseInverse(A);
        Assert.Equal(A.Columns, P.Rows);
        Assert.Equal(A.Rows, P.Columns);
        Close(A * P * A, A, name + ": A A+ A = A");
        Close(P * A * P, P, name + ": A+ A A+ = A+");
        Close(Transpose(A * P), A * P, name + ": A A+ symmetric");
        Close(Transpose(P * A), P * A, name + ": A+ A symmetric");
    }

    [Fact]
    public void AWideInverseIsItsTransposesTransposed()
    {
        var A = new Matrix(new double[,] { { 1.0, 2.0, 3.0 }, { 4.0, 5.0, 6.5 } });
        Matrix wide = MoorePenroseInverseFunctions.MoorePenroseInverse(A);
        Matrix tall = Transpose(MoorePenroseInverseFunctions.MoorePenroseInverse(Transpose(A)));
        for (int i = 0; i < wide.Rows; i++)
        {
            for (int j = 0; j < wide.Columns; j++)
            {
                Assert.Equal(tall[i, j], wide[i, j]);
            }
        }
    }

    private static void Close(Matrix actual, Matrix expected, string what)
    {
        for (int i = 0; i < expected.Rows; i++)
        {
            for (int j = 0; j < expected.Columns; j++)
            {
                Assert.True(Math.Abs(actual[i, j] - expected[i, j]) <= 1e-12 * (1.0 + Math.Abs(expected[i, j])), $"{what} at ({i}, {j})");
            }
        }
    }
}
