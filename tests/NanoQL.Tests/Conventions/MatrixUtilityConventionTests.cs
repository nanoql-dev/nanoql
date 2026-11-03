// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The matrix utilities' conventions: the factor reduction below 2 × 2 and TAP's rank 0, size 0 and unchecked angle
/// reads refused; the sparse ILU's refusals of a row whose elimination never ends and of a vector not of its size;
/// Pascal's rows beyond 67 refused; QuantLib's checks and texts. And §2.6's two routes: the ILU's factors against
/// ILU(k)'s defining identity, (LU)ᵢⱼ = Aᵢⱼ on the kept pattern; <c>Expm</c> against the eigendecomposition. QuantLib's
/// values beside the refusals are its own.
/// </summary>
public class MatrixUtilityConventionTests
{
    private static readonly double[,] Cancelling =
    {
        { 4, 0, 2, 0, 2, 1, 0, 0 },
        { 0, 4, 0, 0, 1, 1, 0, 0 },
        { 0, 0, 4, 0, 0, 0, 1, 0 },
        { 2, 0, 0, 4, 1, 0, 0, 1 },
        { 0, 0, 0, 2, 4, 1, 0, 0 },
        { 2, 2, 2, 0, 0, 4, 0, 0 },
        { 0, 0, 0, 0, 0, 2, 4, 0 },
        { 0, 2, 0, 0, 2, 0, 0, 4 },
    };

    private static double[][] MatrixVectorRows(Matrix m) => [.. Enumerable.Range(0, m.Rows).Select(i => m.Row(i).ToArray())];

    private static SparseMatrix Sparse(double[,] m)
    {
        var s = new SparseMatrix(m.GetLength(0), m.GetLength(1));
        for (int i = 0; i < m.GetLength(0); ++i)
        {
            for (int j = 0; j < m.GetLength(1); ++j)
            {
                if (m[i, j] != 0.0)
                {
                    s[i, j] = m[i, j];
                }
            }
        }

        return s;
    }

    [Fact]
    public void TheFactorReductionRefusesBelowTwoByTwo()
    {
        // QuantLib's initial guess divides by 0 there: NaN loadings
        Assert.Equal("mtrx", Assert.Throws<ArgumentException>(
            () => FactorReductionFunctions.FactorReduction(new Matrix(1, 1, 1.0))).ParamName);
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => FactorReductionFunctions.FactorReduction(new Matrix(2, 3, 0.5)));
        Assert.StartsWith("Input matrix is not square", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTriangularAnglesRefuseWhatQuantLibReadsUnchecked()
    {
        // rank 0 (QuantLib's own count check refuses it too), a rank above the size + 1, size 0, and the LMM form's
        // angles
        Assert.Throws<ArgumentOutOfRangeException>(() => TapCorrelationsFunctions.TriangularAnglesParametrization(new Vector(0), 3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TapCorrelationsFunctions.TriangularAnglesParametrization(new Vector(2), 3, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => TapCorrelationsFunctions.TriangularAnglesParametrization(new Vector(5), 4, 6));

        // rank size + 1, whose count and loops are rank size's: QuantLib's rank-size matrix
        var angles = new Vector([0.4, 1.1, 2.3]);
        Assert.Equal(
            MatrixVectorRows(TapCorrelationsFunctions.TriangularAnglesParametrization(angles, 3, 3)),
            MatrixVectorRows(TapCorrelationsFunctions.TriangularAnglesParametrization(angles, 3, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TapCorrelationsFunctions.TriangularAnglesParametrization(new Vector(0), 0, 1));
        ArgumentException count = Assert.Throws<ArgumentException>(
            () => TapCorrelationsFunctions.TriangularAnglesParametrization(new Vector(2), 3, 3));
        Assert.StartsWith("rank-1) * (matrixSize - rank/2) == angles.size()", count.Message, StringComparison.Ordinal);
        Assert.Equal("angles", Assert.Throws<ArgumentException>(
            () => TapCorrelationsFunctions.LmmTriangularAnglesParametrization(new Vector(1), 3, 3)).ParamName);

        // rank 1: every row the first unit vector, QuantLib's
        Matrix one = TapCorrelationsFunctions.TriangularAnglesParametrization(new Vector(0), 3, 1);
        for (int i = 0; i < 3; ++i)
        {
            Assert.Equal(1.0, one[i, 0]);
        }
    }

    [Fact]
    public void TheIluRefusesARowItsEliminationNeverLeaves()
    {
        // row 1's only entry is left of its diagonal: QuantLib's loop runs on column 0 forever
        var a = new SparseMatrix(2, 2);
        a[0, 0] = 1.0;
        a[1, 0] = 1.0;
        Assert.Equal("a", Assert.Throws<ArgumentException>(() => new SparseILUPreconditioner(a, 1)).ParamName);

        var ilu = new SparseILUPreconditioner(Sparse(Cancelling), 1);
        Assert.Equal("b", Assert.Throws<ArgumentException>(() => ilu.Apply(new Vector(7))).ParamName);
    }

    [Fact]
    public void TheIluFactorsMeetTheMatrixOnTheirPattern()
    {
        for (int lfil = 0; lfil <= 2; ++lfil)
        {
            var ilu = new SparseILUPreconditioner(Sparse(Cancelling), lfil);
            SparseMatrix l = ilu.L(), u = ilu.U();
            for (int i = 0; i < 8; ++i)
            {
                for (int j = 0; j < 8; ++j)
                {
                    if ((j < i && l[i, j] == 0.0) || (j >= i && u[i, j] == 0.0))
                    {
                        continue;
                    }

                    double lu = 0.0;
                    for (int k = 0; k < 8; ++k)
                    {
                        lu += l[i, k] * u[k, j];
                    }

                    Assert.True(Math.Abs(lu - Cancelling[i, j]) < 1e-14, $"lfil {lfil}, ({i}, {j}): {lu:R}");
                }
            }
        }
    }

    [Fact]
    public void ExpmIsTheEigendecompositionsExponential()
    {
        var m = new Matrix(3, 3);
        m[0, 0] = -1.0; m[0, 1] = 0.4; m[0, 2] = 0.1;
        m[1, 0] = 0.4; m[1, 1] = -2.0; m[1, 2] = 0.3;
        m[2, 0] = 0.1; m[2, 1] = 0.3; m[2, 2] = -0.5;
        foreach (double t in new[] { 0.1, 1.0, 3.0 })
        {
            var schur = new SymmetricSchurDecomposition(m);
            Matrix v = schur.Eigenvectors();
            Vector lambda = schur.Eigenvalues();
            var d = new Matrix(3, 3, 0.0);
            for (int i = 0; i < 3; ++i)
            {
                d[i, i] = Math.Exp(t * lambda[i]);
            }

            Matrix expected = v * d * MatrixFunctions.Transpose(v);
            Matrix calculated = ExpmFunctions.Expm(m, t, 1e-15);
            for (int i = 0; i < 3; ++i)
            {
                for (int j = 0; j < 3; ++j)
                {
                    Assert.True(Math.Abs(calculated[i, j] - expected[i, j]) < 1e-13, $"t {t}, ({i}, {j})");
                }
            }
        }

        ArgumentException e = Assert.Throws<ArgumentException>(() => ExpmFunctions.Expm(new Matrix(2, 3, 0.0)));
        Assert.StartsWith("Expm expects a square matrix", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PascalsRowsEndAtSixtySeven()
    {
        Assert.Equal(68, PascalTriangle.Get(PascalTriangle.MaxOrder).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => PascalTriangle.Get(68));
        Assert.Throws<ArgumentOutOfRangeException>(() => PascalTriangle.Get(-1));

        // the polynomial's definite coefficients read the rows to its order
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PolynomialFunction(new double[68]).DefiniteIntegralCoefficients(0.0, 1.0));
        Assert.Equal("coeff", Assert.Throws<ArgumentException>(() => new PolynomialFunction([])).ParamName);
    }

    [Fact]
    public void QrSolveChecksItsSizes()
    {
        var a = new Matrix(3, 2, 1.0);
        Assert.StartsWith(
            "dimensions of A and b don't match",
            Assert.Throws<ArgumentException>(() => QRDecompositionFunctions.QRSolve(a, new Vector(2))).Message,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "dimensions of A and d don't match",
            Assert.Throws<ArgumentException>(() => QRDecompositionFunctions.QRSolve(a, new Vector(3), true, new Vector(3))).Message,
            StringComparison.Ordinal);
    }
}
