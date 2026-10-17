// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2016 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/matrices.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testEigenvectors, testCholeskyDecomposition, testCholeskySolverFor, testCholeskySolverForIncomplete, testHouseholderTransformation, testHouseholderReflection (B4.7's matrix utilities); testSqrt, testHighamSqrt, testSVD, testPrincipalMatrixSqrt (B4.1); testIterativeSolvers, testSparseMatrixMemory (S-eq); testOrthogonalProjection (B5.2); testQRDecomposition, testQRSolve (B6.2); testMoorePenroseInverse (B7.3)

using NanoQL.Numerics;
using static NanoQL.Numerics.MatrixFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class MatricesTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    private const Size N = 3;

    // QuantLib's setup(), for the matrices the ported cases use; xUnit builds the class anew for each case, where
    // QuantLib's cases call setup() first. QuantLib's M1 = M2 = I = Matrix(N, N) assigns three copies.
    private readonly Matrix M1 = new(N, N), M2 = new(N, N), I = new(N, N);
    private readonly Matrix M5 = new(4, 4, 0.0), M6 = new(4, 4, 0.0);

    // BOOST_CHECK_EQUAL and QL_CHECK_CLOSE record a failure and go on; the case fails at its end.
    private readonly List<string> errors = [];

    public MatricesTests()
    {
        M1[0, 0] = 1.0;  M1[0, 1] = 0.9;  M1[0, 2] = 0.7;
        M1[1, 0] = 0.9;  M1[1, 1] = 1.0;  M1[1, 2] = 0.4;
        M1[2, 0] = 0.7;  M1[2, 1] = 0.4;  M1[2, 2] = 1.0;

        M2[0, 0] = 1.0;  M2[0, 1] = 0.9;  M2[0, 2] = 0.7;
        M2[1, 0] = 0.9;  M2[1, 1] = 1.0;  M2[1, 2] = 0.3;
        M2[2, 0] = 0.7;  M2[2, 1] = 0.3;  M2[2, 2] = 1.0;

        I[0, 0] = 1.0;  I[0, 1] = 0.0;  I[0, 2] = 0.0;
        I[1, 0] = 0.0;  I[1, 1] = 1.0;  I[1, 2] = 0.0;
        I[2, 0] = 0.0;  I[2, 1] = 0.0;  I[2, 2] = 1.0;

        // from Higham - nearest correlation matrix
        M5[0, 0] = 2;   M5[0, 1] = -1;  M5[0, 2] = 0.0; M5[0, 3] = 0.0;
        M5[1, 0] = M5[0, 1];  M5[1, 1] = 2;   M5[1, 2] = -1;  M5[1, 3] = 0.0;
        M5[2, 0] = M5[0, 2]; M5[2, 1] = M5[1, 2];  M5[2, 2] = 2;   M5[2, 3] = -1;
        M5[3, 0] = M5[0, 3]; M5[3, 1] = M5[1, 3]; M5[3, 2] = M5[2, 3];  M5[3, 3] = 2;

        // from Higham - nearest correlation matrix to M5
        M6[0, 0] = 1;        M6[0, 1] = -0.8084124981;  M6[0, 2] = 0.1915875019;   M6[0, 3] = 0.106775049;
        M6[1, 0] = M6[0, 1]; M6[1, 1] = 1;        M6[1, 2] = -0.6562326948;  M6[1, 3] = M6[0, 2];
        M6[2, 0] = M6[0, 2]; M6[2, 1] = M6[1, 2]; M6[2, 2] = 1;        M6[2, 3] = M6[0, 1];
        M6[3, 0] = M6[0, 3]; M6[3, 1] = M6[1, 3]; M6[3, 2] = M6[2, 3]; M6[3, 3] = 1;
    }

    [Fact]
    public void TestInverse()
    {
        // Testing LU inverse calculation...

        Real tol = 1.0e-12;
        Matrix[] testMatrices = [M1, M2, I, M5];

        foreach (Matrix A in testMatrices)
        {
            Matrix invA = Inverse(A);

            Matrix I1 = invA * A;
            Matrix I2 = A * invA;

            var identity = new Matrix(A.Rows, A.Rows, 0.0);
            for (Size i = 0; i < A.Rows; ++i)
            {
                identity[i, i] = 1.0;
            }

            if (Norm(I1 - identity) > tol)
            {
                Assert.Fail($"inverse(A)*A does not recover unit matrix (norm = {Norm(I1 - identity)})");
            }

            if (Norm(I2 - identity) > tol)
            {
                Assert.Fail($"A*inverse(A) does not recover unit matrix (norm = {Norm(I1 - identity)})");
            }
        }
    }

    // Adapted: without its second loop, lines 410-440, the determinants of 100 random 3x3 matrices drawn from
    // MersenneTwisterUniformRng(1234) against the explicit formula, which waits for MersenneTwisterUniformRng (S2's
    // random numbers, after this port). The five matrices with Octave's determinants are kept.
    [Fact]
    public void TestDeterminant()
    {
        // Testing LU determinant calculation...

        Real tol = 1e-10;

        Matrix[] testMatrices = [M1, M2, M5, M6, I];

        // expected results calculated with octave
        Real[] expected = [0.044, -0.012, 5.0, 5.7621e-11, 1.0];

        for (Size j = 0; j < testMatrices.Length; ++j)
        {
            Real calculated = Determinant(testMatrices[j]);
            if (Math.Abs(expected[j] - calculated) > tol)
            {
                Assert.Fail(
                    $"determinant calculation failed \n matrix     :\n{testMatrices[j]}\n calculated : {calculated}\n expected   : {expected[j]}");
            }
        }
    }

    [Fact]
    public void TestInitializers()
    {
        // Testing matrix initializers...

        var m1 = new Matrix();
        Assert.True(m1.Rows == 0, "critical check m1.rows() == 0 has failed");
        Assert.True(m1.Columns == 0, "critical check m1.columns() == 0 has failed");

        var m2 = new Matrix(new double[,]
        {
            { 1.0, 2.0, 3.0 },
            { 4.0, 5.0, 6.0 },
        });
        Assert.True(m2.Rows == 2, "critical check m2.rows() == 2 has failed");
        Assert.True(m2.Columns == 3, "critical check m2.columns() == 3 has failed");
        CheckEqual(m2[0, 0], 1.0);
        CheckEqual(m2[0, 1], 2.0);
        CheckEqual(m2[0, 2], 3.0);
        CheckEqual(m2[1, 0], 4.0);
        CheckEqual(m2[1, 1], 5.0);
        CheckEqual(m2[1, 2], 6.0);

        Assert.Empty(errors);
    }

    [Fact]
    public void TestOperators()
    {
        // Testing matrix operators...

        static Matrix GetMatrix() => new(2, 3, 4.0);

        Matrix m = GetMatrix();

        var negative = new Matrix(2, 3, -4.0);
        Matrix lvalue_negative = -m;
        Matrix rvalue_negative = -GetMatrix();

        CheckCloseMatrix(lvalue_negative, negative);
        CheckCloseMatrix(rvalue_negative, negative);

        var matrix_sum = new Matrix(2, 3, 8.0);
        Matrix lvalue_lvalue_sum = m + m;
        Matrix lvalue_rvalue_sum = m + GetMatrix();
        Matrix rvalue_lvalue_sum = GetMatrix() + m;
        Matrix rvalue_rvalue_sum = GetMatrix() + GetMatrix();

        CheckCloseMatrix(lvalue_lvalue_sum, matrix_sum);
        CheckCloseMatrix(lvalue_rvalue_sum, matrix_sum);
        CheckCloseMatrix(rvalue_lvalue_sum, matrix_sum);
        CheckCloseMatrix(rvalue_rvalue_sum, matrix_sum);

        var matrix_difference = new Matrix(2, 3, 0.0);
        Matrix lvalue_lvalue_difference = m - m;
        Matrix lvalue_rvalue_difference = m - GetMatrix();
        Matrix rvalue_lvalue_difference = GetMatrix() - m;
        Matrix rvalue_rvalue_difference = GetMatrix() - GetMatrix();

        CheckCloseMatrix(lvalue_lvalue_difference, matrix_difference);
        CheckCloseMatrix(lvalue_rvalue_difference, matrix_difference);
        CheckCloseMatrix(rvalue_lvalue_difference, matrix_difference);
        CheckCloseMatrix(rvalue_rvalue_difference, matrix_difference);

        var scalar_product = new Matrix(2, 3, 6.0);
        Matrix lvalue_real_product = m * 1.5;
        Matrix rvalue_real_product = GetMatrix() * 1.5;
        Matrix real_lvalue_product = 1.5 * m;
        Matrix real_rvalue_product = 1.5 * GetMatrix();

        CheckCloseMatrix(lvalue_real_product, scalar_product);
        CheckCloseMatrix(rvalue_real_product, scalar_product);
        CheckCloseMatrix(real_lvalue_product, scalar_product);
        CheckCloseMatrix(real_rvalue_product, scalar_product);

        var scalar_quotient = new Matrix(2, 3, 2.0);
        Matrix lvalue_real_quotient = m / 2.0;
        Matrix rvalue_real_quotient = GetMatrix() / 2.0;

        CheckCloseMatrix(lvalue_real_quotient, scalar_quotient);
        CheckCloseMatrix(rvalue_real_quotient, scalar_quotient);

        Assert.Empty(errors);
    }

    // QuantLib's norm(const Matrix&): the Frobenius norm.
    private static Real Norm(Matrix m)
    {
        Real sum = 0.0;
        for (Size i = 0; i < m.Rows; i++)
        {
            for (Size j = 0; j < m.Columns; j++)
            {
                sum += m[i, j] * m[i, j];
            }
        }

        return Math.Sqrt(sum);
    }

    private void CheckEqual(double actual, double expected)
    {
        if (!(actual == expected))
        {
            errors.Add($"check {actual:R} == {expected:R} has failed");
        }
    }

    // QL_CHECK_CLOSE_MATRIX: the dimensions required equal, then each element within 100 epsilon percent of the
    // expected one, by BOOST_CHECK_CLOSE's strong test (Boost's safe_fpt_division).
    private void CheckCloseMatrix(Matrix actual, Matrix expected)
    {
        Assert.True(
            actual.Rows == expected.Rows && actual.Columns == expected.Columns,
            "critical check actual.rows() == expected.rows() && actual.columns() == expected.columns() has failed");
        double fraction = 100 * QL_EPSILON * 0.01;
        for (int i = 0; i < actual.Rows; i++)
        {
            for (int j = 0; j < actual.Columns; j++)
            {
                double a = actual[i, j], e = expected[i, j], diff = Math.Abs(a - e);
                if (!(SafeDivision(diff, Math.Abs(e)) <= fraction && SafeDivision(diff, Math.Abs(a)) <= fraction))
                {
                    errors.Add($"difference between {a:R} and {e:R} exceeds {100 * QL_EPSILON:R}%");
                }
            }
        }
    }

    // Boost's safe_fpt_division: f1 / f2, without overflow or underflow.
    private static double SafeDivision(double f1, double f2)
    {
        const double normalMin = 2.2250738585072014e-308;
        if (f2 < 1.0 && f1 > f2 * double.MaxValue)
        {
            return double.MaxValue;
        }

        if (Math.Abs(f1) <= normalMin || (f2 > 1.0 && f1 < f2 * normalMin))
        {
            return 0.0;
        }

        return f1 / f2;
    }
}
