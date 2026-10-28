// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2016 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/matrices.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testMoorePenroseInverse (S-xrt)
// Moved: testSparseMatrixMemory line 755 (Divergences/SparseMatrixProductTests.cs)

using NanoQL.Numerics;
using static NanoQL.Numerics.CholeskyDecompositionFunctions;
using static NanoQL.Numerics.MatrixFunctions;
using static NanoQL.Numerics.PseudoSqrtFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class MatricesTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    // M_PI and M_SQRT2 (mathconstants.hpp).
    private const double M_PI = 3.141592653589793238462643383280;
    private const double M_SQRT2 = 1.41421356237309504880;

    private const Size N = 3;

    // QuantLib's setup(), for the matrices the ported cases use; xUnit builds the class anew for each case, where
    // QuantLib's cases call setup() first. QuantLib's M1 = M2 = I = Matrix(N, N) assigns three copies.
    private readonly Matrix M1 = new(N, N), M2 = new(N, N), I = new(N, N);
    private readonly Matrix M3 = new(3, 4), M4 = new(4, 3);
    private readonly Matrix M5 = new(4, 4, 0.0), M6 = new(4, 4, 0.0);
    private readonly Matrix M7;

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

        M3[0, 0] = 1; M3[0, 1] = 2; M3[0, 2] = 3; M3[0, 3] = 4;
        M3[1, 0] = 2; M3[1, 1] = 0; M3[1, 2] = 2; M3[1, 3] = 1;
        M3[2, 0] = 0; M3[2, 1] = 1; M3[2, 2] = 0; M3[2, 3] = 0;

        M4[0, 0] = 1;  M4[0, 1] = 2;  M4[0, 2] = 400;
        M4[1, 0] = 2;  M4[1, 1] = 0;  M4[1, 2] = 1;
        M4[2, 0] = 30; M4[2, 1] = 2;  M4[2, 2] = 0;
        M4[3, 0] = 2;  M4[3, 1] = 0;  M4[3, 2] = 1.05;

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

        M7 = M1.Clone();
        M7[0, 1] = 0.3; M7[0, 2] = 0.2; M7[2, 1] = 1.2;
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

        var rng = new MersenneTwisterUniformRng(1234);
        for (Size j = 0; j < 100; ++j)
        {
            var m = new Matrix(3, 3, 0.0);
            foreach (ref double iter in m)
            {
                iter = rng.Next().Value;
            }

            if ((j % 3) == 0)
            {
                // every third matrix is a singular matrix
                Size row = (Size)(3 * rng.Next().Value);
                m.Row(row).Fill(0.0);
            }

            Real a = m[0, 0];
            Real b = m[0, 1];
            Real c = m[0, 2];
            Real d = m[1, 0];
            Real e = m[1, 1];
            Real f = m[1, 2];
            Real g = m[2, 0];
            Real h = m[2, 1];
            Real i = m[2, 2];

            Real expectedValue = (a * e * i) + (b * f * g) + (c * d * h) - ((g * e * c) + (h * f * a) + (i * d * b));
            Real calculated = Determinant(m);

            if (Math.Abs(expectedValue - calculated) > tol)
            {
                Assert.Fail(
                    $"determinant calculation failed \n matrix     :\n{m}\n calculated : {calculated}\n expected   : {expectedValue}");
            }
        }
    }

    [Fact]
    public void TestOrthogonalProjection()
    {
        // Testing orthogonal projections...
        int dimension = 1000;
        int numberVectors = 50;
        double multiplier = 100;
        double tolerance = 1e-6;
        long seed = 1;

        double errorAcceptable = 1E-11;

        var test = new Matrix(numberVectors, dimension);

        var rng = new MersenneTwisterUniformRng(seed);

        for (int i = 0; i < numberVectors; ++i)
        {
            for (int j = 0; j < dimension; ++j)
            {
                test[i, j] = rng.Next().Value;
            }
        }

        var projector = new OrthogonalProjections(test, multiplier, tolerance);

        int numberFailures = 0;
        int failuresTwo = 0;

        for (int i = 0; i < numberVectors; ++i)
        {
            // check output vector i is orthogonal to all other vectors
            if (projector.ValidVectors[i])
            {
                for (int j = 0; j < numberVectors; ++j)
                {
                    if (projector.ValidVectors[j] && i != j)
                    {
                        double dotProduct = 0.0;
                        for (int k = 0; k < dimension; ++k)
                        {
                            dotProduct += test[j, k] * projector.GetVector(i)[k];
                        }

                        if (Math.Abs(dotProduct) > errorAcceptable)
                        {
                            ++numberFailures;
                        }
                    }
                }

                double innerProductWithOriginal = 0.0;
                double normSq = 0.0;

                for (int j = 0; j < dimension; ++j)
                {
                    innerProductWithOriginal += projector.GetVector(i)[j] * test[i, j];
                    normSq += test[i, j] * test[i, j];
                }

                if (Math.Abs(innerProductWithOriginal - normSq) > errorAcceptable)
                {
                    ++failuresTwo;
                }
            }
        }

        if (numberFailures > 0 || failuresTwo > 0)
        {
            Assert.Fail($"OrthogonalProjections test failed with {numberFailures} failures  of orthogonality and {failuresTwo} failures of projection size.");
        }
    }

    [Fact]
    public void TestIterativeSolvers()
    {
        // Testing iterative solvers...

        var b = new Vector(3);
        b[0] = 1.0;
        b[1] = 0.5;
        b[2] = 3.0;

        const double relTol = 1e4 * QL_EPSILON;

        Vector x = new BiCGstab(MatrixMult(M1), 3, relTol).Solve(b).X;
        if (ArrayFunctions.Norm2((M1 * x) - b) / ArrayFunctions.Norm2(b) > relTol)
        {
            Assert.Fail(
                "Failed to calculate inverse using BiCGstab"
                + $"\n  rel error     : {ArrayFunctions.Norm2((M1 * x) - b) / ArrayFunctions.Norm2(b)}"
                + $"\n  rel tolerance : {relTol}");
        }

        GMRESResult u = new GMRES(MatrixMult(M1), 3, relTol).Solve(b, b);
        if (ArrayFunctions.Norm2((M1 * u.X) - b) / ArrayFunctions.Norm2(b) > relTol)
        {
            Assert.Fail(
                "Failed to calculate inverse using gmres"
                + $"\n  rel error     : {ArrayFunctions.Norm2((M1 * u.X) - b) / ArrayFunctions.Norm2(b)}"
                + $"\n  rel tolerance : {relTol}");
        }

        var errors = new Vector(u.Errors.AsSpan());
        foreach (Real error in errors)
        {
            Vector x10 = new GMRES(MatrixMult(M1), 10, 1.01 * error).Solve(b, b).X;

            Real calculated = ArrayFunctions.Norm2((M1 * x10) - b) / ArrayFunctions.Norm2(b);
            Real expected = error;

            if (Math.Abs(calculated - expected) > relTol)
            {
                Assert.Fail(
                    "Failed to calculate solution error"
                    + $"\n  calculated error: {calculated}"
                    + $"\n  expected error  : {expected}");
            }
        }

        Vector v = new GMRES(MatrixMult(M1), 1, relTol, MatrixMult(Inverse(M1))).Solve(b, b).X;

        if (ArrayFunctions.Norm2((M1 * v) - b) / ArrayFunctions.Norm2(b) > relTol)
        {
            Assert.Fail(
                "Failed to calculate inverse using gmres with exact preconditioning"
                + $"\n  rel error     : {ArrayFunctions.Norm2((M1 * v) - b) / ArrayFunctions.Norm2(b)}"
                + $"\n  rel tolerance : {relTol}");
        }

        Vector w = new GMRES(MatrixMult(M1), 3, relTol, MatrixMult(M1)).Solve(b, b).X;
        if (ArrayFunctions.Norm2((M1 * w) - b) / ArrayFunctions.Norm2(b) > relTol)
        {
            Assert.Fail(
                "Failed to calculate inverse using gmres with nonsense preconditioning"
                + $"\n  rel error     : {ArrayFunctions.Norm2((M1 * w) - b) / ArrayFunctions.Norm2(b)}"
                + $"\n  rel tolerance : {relTol}");
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
    public void TestSparseMatrixMemory()
    {
        // Testing sparse matrix memory layout...

        var m = new SparseMatrix(8, 4);
        CheckEqual(m.Filled1, 1);
        CheckEqual(m.Size1, 8);
        CheckEqual(m.Size2, 4);
        CheckEqual(Rows(m).Count(), m.Size1);

        CoordinateTuple coords = SparseMatrixToCoordinateTuple(m);
        CheckEqual(coords.RowIndices.Count, 0);

        m[3, 1] = 42;
        coords = SparseMatrixToCoordinateTuple(m);
        CheckEqual(Rows(m).Count(), m.Size1);
        CheckEqual(coords.RowIndices.Count, 1);
        CheckEqual(coords.RowIndices[0], 3);
        CheckEqual(coords.ColumnIndices[0], 1);
        CheckEqual(coords.Data[0], 42);

        m[1, 2] = 6;
        coords = SparseMatrixToCoordinateTuple(m);
        CheckEqual(coords.RowIndices.Count, 2);
        CheckEqual(coords.RowIndices[0], 1);
        CheckEqual(coords.ColumnIndices[0], 2);
        CheckEqual(coords.Data[0], 6);

        // QuantLib checks prod(m, x) == Array({0, 18, 0, 84}), a result sized by the columns of a matrix of 8 rows;
        // Divergences/SparseMatrixProductTests asserts it, sized by the rows.
        Vector x = [1, 2, 3, 4];
        _ = SparseMatrixFunctions.Prod(m, x);

        m[3, 2] = 43;
        coords = SparseMatrixToCoordinateTuple(m);
        CheckEqual(coords.RowIndices.Count, 3);
        CheckEqual(coords.RowIndices[2], 3);
        CheckEqual(coords.ColumnIndices[2], 2);
        CheckEqual(coords.Data[2], 43);

        m[7, 3] = 44;
        coords = SparseMatrixToCoordinateTuple(m);
        CheckEqual(coords.RowIndices.Count, 4);
        CheckEqual(coords.RowIndices[3], 7);
        CheckEqual(coords.ColumnIndices[3], 3);
        CheckEqual(coords.Data[3], 44);

        Size entries = 0;
        foreach ((int Begin, int End) row in Rows(m))
        {
            entries += row.End - row.Begin;
        }

        CheckEqual(entries, 4);

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

    // QuantLib's MatrixMult: the product of a copy of the matrix with a vector.
    [Fact]
    public void TestEigenvectors()
    {
        // Testing eigenvalues and eigenvectors calculation...

        Matrix[] testMatrices = [M1, M2];

        foreach (Matrix M in testMatrices)
        {
            var dec = new SymmetricSchurDecomposition(M);
            Vector eigenValues = dec.Eigenvalues();
            Matrix eigenVectors = dec.Eigenvectors();
            Real minHolder = double.MaxValue;

            for (Size i = 0; i < N; i++)
            {
                var v = new Vector(N);
                for (Size j = 0; j < N; j++)
                {
                    v[j] = eigenVectors[j, i];
                }

                // check definition
                Vector a = M * v;
                Vector b = eigenValues[i] * v;
                if (Norm(a - b) > 1.0e-15)
                {
                    Assert.Fail("Eigenvector definition not satisfied");
                }

                // check decreasing ordering
                if (eigenValues[i] >= minHolder)
                {
                    Assert.Fail($"Eigenvalues not ordered: {eigenValues}");
                }
                else
                {
                    minHolder = eigenValues[i];
                }
            }

            // check normalization
            Matrix m = eigenVectors * Transpose(eigenVectors);
            if (Norm(m - I) > 1.0e-15)
            {
                Assert.Fail("Eigenvector not normalized");
            }
        }
    }

    [Fact]
    public void TestSqrt()
    {
        // Testing matricial square root...

        Matrix m = PseudoSqrt(M1, SalvagingAlgorithmType.None);
        Matrix temp = m * Transpose(m);
        Real error = Norm(temp - M1);
        Real tolerance = 1.0e-12;
        if (error > tolerance)
        {
            Assert.Fail(
                $"Matrix square root calculation failed\noriginal matrix:\n{M1}pseudoSqrt:\n{m}pseudoSqrt*pseudoSqrt:\n{temp}"
                + $"\nerror:     {error}\ntolerance: {tolerance}");
        }
    }

    [Fact]
    public void TestHighamSqrt()
    {
        // Testing Higham matricial square root...

        Matrix tempSqrt = PseudoSqrt(M5, SalvagingAlgorithmType.Higham);
        Matrix ansSqrt = PseudoSqrt(M6, SalvagingAlgorithmType.None);
        Real error = Norm(ansSqrt - tempSqrt);
        Real tolerance = 1.0e-4;
        if (error > tolerance)
        {
            Assert.Fail(
                $"Higham matrix correction failed\noriginal matrix:\n{M5}pseudoSqrt:\n{tempSqrt}should be:\n{ansSqrt}"
                + $"\nerror:     {error}\ntolerance: {tolerance}");
        }
    }

    [Fact]
    public void TestSVD()
    {
        // Testing singular value decomposition...

        Real tol = 1.0e-12;
        Matrix[] testMatrices = [M1, M2, M3, M4];

        foreach (Matrix A in testMatrices)
        {
            // m >= n required (rows >= columns)
            var svd = new SVD(A);

            // U is m x n
            Matrix U = svd.U();

            // s is n long
            Vector s = svd.SingularValues();

            // S is n x n
            Matrix S = svd.S();

            // V is n x n
            Matrix V = svd.V();

            for (Size i = 0; i < S.Rows; i++)
            {
                if (S[i, i] != s[i])
                {
                    Assert.Fail("S not consistent with s");
                }
            }

            // tests
            Matrix U_Utranspose = Transpose(U) * U;
            if (Norm(U_Utranspose - I) > tol)
            {
                Assert.Fail($"U not orthogonal (norm of U^T*U-I = {Norm(U_Utranspose - I)})");
            }

            Matrix V_Vtranspose = Transpose(V) * V;
            if (Norm(V_Vtranspose - I) > tol)
            {
                Assert.Fail($"V not orthogonal (norm of V^T*V-I = {Norm(V_Vtranspose - I)})");
            }

            Matrix A_reconstructed = U * S * Transpose(V);
            if (Norm(A_reconstructed - A) > tol)
            {
                Assert.Fail($"Product does not recover A: (norm of U*S*V^T-A = {Norm(A_reconstructed - A)})");
            }
        }
    }

    [Fact]
    public void TestQRDecomposition()
    {
        // Testing QR decomposition...

        Real tol = 1.0e-12;
        Matrix[] testMatrices = [M1, M2, I, M3, Transpose(M3), M4, Transpose(M4), M5];

        foreach (Matrix A in testMatrices)
        {
            bool pivot = true;
            QRDecompositionResult qr = QRDecompositionFunctions.QRDecomposition(A, pivot);
            Matrix Q = qr.Q, R = qr.R;

            var P = new Matrix(A.Columns, A.Columns, 0.0);

            // reverse column pivoting
            for (Size i = 0; i < P.Columns; ++i)
            {
                P[qr.Ipvt[i], i] = 1.0;
            }

            if (Norm((Q * R) - (A * P)) > tol)
            {
                Assert.Fail($"Q*R does not match matrix A*P (norm = {Norm((Q * R) - (A * P))})");
            }

            pivot = false;
            qr = QRDecompositionFunctions.QRDecomposition(A, pivot);
            Q = qr.Q;
            R = qr.R;

            if (Norm((Q * R) - A) > tol)
            {
                Assert.Fail($"Q*R does not match matrix A (norm = {Norm((Q * R) - A)})");
            }
        }
    }

    [Fact]
    public void TestQRSolve()
    {
        // Testing QR solve...

        Real tol = 1.0e-12;
        var rng = new MersenneTwisterUniformRng(1234);
        var bigM = new Matrix(50, 100, 0.0);
        for (Size i = 0; i < Math.Min(bigM.Rows, bigM.Columns); ++i)
        {
            bigM[i, i] = i + 1.0;
        }

        var randM = new Matrix(50, 200);
        for (Size i = 0; i < randM.Rows; ++i)
        {
            for (Size j = 0; j < randM.Columns; ++j)
            {
                randM[i, j] = rng.Next().Value;
            }
        }

        Matrix[] testMatrices =
        [
            M1, M2, M3, Transpose(M3), M4, Transpose(M4), M5, I, M7, bigM, Transpose(bigM), randM, Transpose(randM),
        ];

        foreach (Matrix A in testMatrices)
        {
            var b = new Vector(A.Rows);

            for (Size k = 0; k < 10; ++k)
            {
                for (Size iter = 0; iter < b.Size; ++iter)
                {
                    b[iter] = rng.Next().Value;
                }

                Vector x = QRDecompositionFunctions.QRSolve(A, b, true);

                if (A.Columns >= A.Rows)
                {
                    if (Norm((A * x) - b) > tol)
                    {
                        Assert.Fail($"A*x does not match vector b (norm = {Norm((A * x) - b)})");
                    }
                }
                else
                {
                    // use the SVD to calculate the reference values
                    Size n = A.Columns;
                    var xr = new Vector(n, 0.0);

                    var svd = new SVD(A);
                    Matrix V = svd.V();
                    Matrix U = svd.U();
                    Vector w = svd.SingularValues();
                    Real threshold = n * QL_EPSILON;

                    for (Size i = 0; i < n; ++i)
                    {
                        if (w[i] > threshold)
                        {
                            Real inner = 0.0;
                            for (Size r = 0; r < U.Rows; ++r)
                            {
                                inner = inner + (U[r, i] * b[r]);
                            }

                            Real u = inner / w[i];

                            for (Size j = 0; j < n; ++j)
                            {
                                xr[j] += u * V[j, i];
                            }
                        }
                    }

                    if (Norm(xr - x) > tol)
                    {
                        Assert.Fail($"least square solution does not match (norm = {Norm(x - xr)})");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestCholeskyDecomposition()
    {
        // Testing Cholesky Decomposition...

        // This test case fails prior to release 1.8

        // The eigenvalues of this matrix are
        // 0.0438523; 0.0187376; 0.000245617; 0.000127656; 8.35899e-05; 6.14215e-05;
        // 1.94241e-05; 1.14417e-06; 9.79481e-18; 1.31141e-18; 5.81155e-19

        Real[,] tmp =
        {
            {
                6.4e-05, 5.28e-05, 2.28e-05, 0.00032, 0.00036, 6.4e-05,
                6.3968010664e-06, 7.2e-05, 7.19460269899e-06, 1.2e-05,
                1.19970004999e-06,
            },
            {
                5.28e-05, 0.000121, 1.045e-05, 0.00044, 0.000165, 2.2e-05,
                2.19890036657e-06, 1.65e-05, 1.64876311852e-06, 1.1e-05,
                1.09972504583e-06,
            },
            {
                2.28e-05, 1.045e-05, 9.025e-05, 0, 0.0001425, 9.5e-06,
                9.49525158294e-07, 2.85e-05, 2.84786356835e-06, 4.75e-06,
                4.74881269789e-07,
            },
            {
                0.00032, 0.00044, 0, 0.04, 0.009, 0.0008, 7.996001333e-05, 0.0006,
                5.99550224916e-05, 0.0001, 9.99750041661e-06,
            },
            {
                0.00036, 0.000165, 0.0001425, 0.009, 0.0225, 0.0003, 2.99850049987e-05,
                0.001125, 0.000112415667172, 0.000225, 2.24943759374e-05,
            },
            {
                6.4e-05, 2.2e-05, 9.5e-06, 0.0008, 0.0003, 0.0001, 9.99500166625e-06,
                7.5e-05, 7.49437781145e-06, 2e-05, 1.99950008332e-06,
            },
            {
                6.3968010664e-06, 2.19890036657e-06, 9.49525158294e-07,
                7.996001333e-05, 2.99850049987e-05, 9.99500166625e-06,
                9.99000583083e-07, 7.49625124969e-06, 7.49063187129e-07,
                1.99900033325e-06, 1.99850066645e-07,
            },
            {
                7.2e-05, 1.65e-05, 2.85e-05, 0.0006, 0.001125, 7.5e-05,
                7.49625124969e-06, 0.000225, 2.24831334343e-05, 1.5e-05,
                1.49962506249e-06,
            },
            {
                7.19460269899e-06, 1.64876311852e-06, 2.84786356835e-06,
                5.99550224916e-05, 0.000112415667172, 7.49437781145e-06,
                7.49063187129e-07, 2.24831334343e-05, 2.24662795123e-06,
                1.49887556229e-06, 1.49850090584e-07,
            },
            {
                1.2e-05, 1.1e-05, 4.75e-06, 0.0001, 0.000225, 2e-05, 1.99900033325e-06,
                1.5e-05, 1.49887556229e-06, 2.5e-05, 2.49937510415e-06,
            },
            {
                1.19970004999e-06, 1.09972504583e-06, 4.74881269789e-07,
                9.99750041661e-06, 2.24943759374e-05, 1.99950008332e-06,
                1.99850066645e-07, 1.49962506249e-06, 1.49850090584e-07,
                2.49937510415e-06, 2.49875036451e-07,
            },
        };

        var m = new Matrix(11, 11);
        for (Size i = 0; i < 11; ++i)
        {
            for (Size j = 0; j < 11; ++j)
            {
                m[i, j] = tmp[i, j];
            }
        }

        Matrix c = CholeskyDecomposition(m, true);
        Matrix m2 = c * Transpose(c);

        Real tol = 1.0E-12;
        for (Size i = 0; i < 11; ++i)
        {
            for (Size j = 0; j < 11; ++j)
            {
                if (double.IsNaN(m2[i, j]))
                {
                    Assert.Fail($"Faield to verify Cholesky decomposition at (i,j)=({i},{j}), replicated value is nan");
                }

                // this does not detect nan values
                if (Math.Abs(m[i, j] - m2[i, j]) > tol)
                {
                    Assert.Fail(
                        $"Failed to verify Cholesky decomposition at (i,j)=({i},{j}), original value is {m[i, j]}, "
                        + $"replicated value is {m2[i, j]}");
                }
            }
        }
    }

    [Fact]
    public void TestPrincipalMatrixSqrt()
    {
        // Testing principal matrix pseudo sqrt...

        Size[] dims = [1, 4, 10, 40];
        foreach (Size n in dims)
        {
            Matrix rho = CreateTestCorrelationMatrix(n);
            Matrix sqrtRho = PseudoSqrt(rho, SalvagingAlgorithmType.Principal);

            // matrix is symmetric
            CheckCloseMatrixTol(sqrtRho, Transpose(sqrtRho), 1e3 * QL_EPSILON);

            // matrix is square root of original matrix
            CheckCloseMatrixTol(sqrtRho * sqrtRho, rho, 1e5 * QL_EPSILON);
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestCholeskySolverFor()
    {
        // Testing CholeskySolverFor...

        var rng = new MersenneTwisterUniformRng(1234);

        Size[] dims = [1, 4, 10, 25, 50];
        foreach (Size n in dims)
        {
            var b = new Vector(n);
            for (Size i = 0; i < n; ++i)
            {
                b[i] = rng.NextReal();
            }

            Matrix rho = CreateTestCorrelationMatrix(n);
            Vector x = CholeskySolveFor(CholeskyDecomposition(rho), b);

            Vector diff = ArrayFunctions.Abs((rho * x) - b);

            CheckSmall(Math.Sqrt(ArrayFunctions.DotProduct(diff, diff)), 20 * Math.Sqrt(n) * QL_EPSILON);
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestCholeskySolverForIncomplete()
    {
        // Testing CholeskySolverFor with incomplete matrix...

        const Size n = 4;

        var rho = new Matrix(n, n, 0.0);
        rho[0, 0] = rho[1, 1] = 1;
        rho[0, 1] = rho[1, 0] = 0.9;

        Matrix L = CholeskyDecomposition(rho, true);
        CheckCloseMatrix(L * Transpose(L), rho);

        Assert.Empty(errors);
    }

    [Fact]
    public void TestHouseholderTransformation()
    {
        // Testing Householder Transformation...

        var rng = new MersenneTwisterUniformRng(1234);

        static Matrix I(Size i)
        {
            var id = new Matrix(i, i, 0.0);
            for (Size j = 0; j < i; ++j)
            {
                id[j, j] = 1.0;
            }

            return id;
        }

        for (Size i = 1; i < 10; ++i)
        {
            Vector v = new(i), x = new(i);
            for (Size j = 0; j < i; ++j)
            {
                v[j] = rng.Next().Value - 0.5;
                x[j] = rng.Next().Value - 0.5;
            }

            Vector expected = (I(i) - (2.0 * OuterProduct(v, v))) * x;
            Vector calculated = new HouseholderTransformation(v).Evaluate(x);
            CheckCloseArrayTol(calculated, expected, 1e4 * QL_EPSILON);
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestHouseholderReflection()
    {
        // Testing Householder Reflection...

        const double tol = 1e4 * QL_EPSILON;

        static Vector E(Size n, Size m = 0)
        {
            var e = new Vector(n, 0.0);
            e[m] = 1.0;
            return e;
        }

        for (Size i = 0; i < 5; ++i)
        {
            CheckCloseArrayTol(
                new HouseholderReflection(E(5)).Evaluate(E(5, i)), E(5), tol);
            CheckCloseArrayTol(
                new HouseholderReflection(E(5)).Evaluate(M_PI * E(5, i)), M_PI * E(5), tol);
            CheckCloseArrayTol(
                new HouseholderReflection(E(5)).Evaluate(
                    E(5, i) + E(5)),
                ((i == 0) ? 2.0 : M_SQRT2) * E(5),
                tol);
        }

        // limits
        for (Real x = 10; x > 1e-50; x *= 0.1)
        {
            CheckCloseArrayTol(
                new HouseholderReflection(E(3)).Evaluate(
                    new Vector([10.0, x, 0])),
                Math.Sqrt((10.0 * 10.0) + (x * x)) * E(3),
                tol);

            CheckCloseArrayTol(
                new HouseholderReflection(E(3)).Evaluate(
                    new Vector([10.0, x, 1e-3])),
                Math.Sqrt((10.0 * 10.0) + (x * x) + (1e-3 * 1e-3)) * E(3),
                tol);
        }

        var rng = new MersenneTwisterUniformRng(1234);

        for (Size i = 0; i < 100; ++i)
        {
            Vector v = new Vector([rng.Next().Value, rng.Next().Value, rng.Next().Value]) - 0.5;
            Matrix u = new HouseholderTransformation(v / ArrayFunctions.Norm2(v)).GetMatrix();

            Vector eu = u * E(3, i % 3);
            Vector a = new Vector([rng.Next().Value, rng.Next().Value, rng.Next().Value]) - 0.5;

            Matrix h = new HouseholderTransformation(
                new HouseholderReflection(eu).ReflectionVector(a)).GetMatrix();

            CheckCloseArrayTol(u * h * a, ArrayFunctions.Norm2(a) * E(3, i % 3), tol);
        }

        Assert.Empty(errors);
    }

    private static Func<Vector, Vector> MatrixMult(Matrix m)
    {
        Matrix m_ = m.Clone();
        return x => m_ * x;
    }

    // The sparse matrix's row iterator: every row, each the range of its stored elements in the data arrays (none
    // beyond the last row written to), as uBLAS's begin1() to end1() visits them.
    private static IEnumerable<(int Begin, int End)> Rows(SparseMatrix m)
    {
        for (int i = 0; i < m.Size1; ++i)
        {
            yield return i + 1 < m.Filled1 ? (m.Index1Data[i], m.Index1Data[i + 1]) : (0, 0);
        }
    }

    // QuantLib's sparseMatrixToCoordinateTuple: the stored elements' rows, columns and values, row by row.
    private static CoordinateTuple SparseMatrixToCoordinateTuple(SparseMatrix m)
    {
        List<Size> row_idx = [], col_idx = [];
        List<Real> data = [];
        int i = 0;
        foreach ((int Begin, int End) row in Rows(m))
        {
            for (int k = row.Begin; k < row.End; ++k)
            {
                row_idx.Add(i);
                col_idx.Add(m.Index2Data[k]);
                data.Add(m.ValueData[k]);
            }

            ++i;
        }

        return new CoordinateTuple(row_idx, col_idx, data);
    }

    private sealed record CoordinateTuple(List<Size> RowIndices, List<Size> ColumnIndices, List<Real> Data);

    // QuantLib's norm(const Array&): the Euclidean norm.
    private static Real Norm(Vector v) => Math.Sqrt(ArrayFunctions.DotProduct(v, v));

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
    private void CheckCloseMatrix(Matrix actual, Matrix expected) => CheckCloseMatrixTol(actual, expected, 100 * QL_EPSILON);

    // QL_CHECK_CLOSE_MATRIX_TOL: as QL_CHECK_CLOSE_MATRIX, each element within tol percent.
    private void CheckCloseMatrixTol(Matrix actual, Matrix expected, double tol)
    {
        Assert.True(
            actual.Rows == expected.Rows && actual.Columns == expected.Columns,
            "critical check actual.rows() == expected.rows() && actual.columns() == expected.columns() has failed");
        double fraction = tol * 0.01;
        for (int i = 0; i < actual.Rows; i++)
        {
            for (int j = 0; j < actual.Columns; j++)
            {
                double a = actual[i, j], e = expected[i, j], diff = Math.Abs(a - e);
                if (!(SafeDivision(diff, Math.Abs(e)) <= fraction && SafeDivision(diff, Math.Abs(a)) <= fraction))
                {
                    errors.Add($"difference between {a:R} and {e:R} exceeds {tol:R}%");
                }
            }
        }
    }

    // matrices.cpp's QL_CHECK_CLOSE_ARRAY_TOL (lines 916-922): the sizes required equal, then each difference small.
    private void CheckCloseArrayTol(Vector actual, Vector expected, double tol)
    {
        Assert.True(actual.Size == expected.Size, "critical check actual.size() == expected.size() has failed");
        for (int i = 0; i < actual.Size; i++)
        {
            CheckSmall(actual[i] - expected[i], tol);
        }
    }

    // QL_CHECK_SMALL: BOOST_CHECK_SMALL, |value| <= tolerance.
    private void CheckSmall(double value, double tolerance)
    {
        if (!(Math.Abs(value) <= tolerance))
        {
            errors.Add($"absolute value of {value:R} exceeds {tolerance:R}");
        }
    }

    // MatrixTests::createTestCorrelationMatrix (lines 851-860).
    private static Matrix CreateTestCorrelationMatrix(Size n)
    {
        var rho = new Matrix(n, n);
        for (Size i = 0; i < n; ++i)
        {
            for (Size j = i; j < n; ++j)
            {
                rho[i, j] = rho[j, i] =
                    Math.Exp((-0.1 * Math.Abs((Real)i - (Real)j)) - ((i != j) ? 0.02 * (i + j) : 0.0));
            }
        }

        return rho;
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
