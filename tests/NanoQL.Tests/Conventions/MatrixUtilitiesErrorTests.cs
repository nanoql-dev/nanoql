// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types and messages of the matrix utilities' refusals (playbook §2), which a vector's
/// <c>{"throws": true}</c> can't tell apart: a matrix or vector argument that doesn't fit throws an
/// <see cref="ArgumentException"/> naming it, a value outside its range an <see cref="ArgumentOutOfRangeException"/>,
/// each with QuantLib's text, or nanoql's where QuantLib reads past an empty matrix; a decomposition that doesn't
/// converge, a <see cref="NanoQLException"/>.
/// </summary>
public class MatrixUtilitiesErrorTests
{
    [Fact]
    public void SymmetricSchurDecompositionRefusals()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new SymmetricSchurDecomposition(new Matrix()));
        Assert.Equal("s", e.ParamName);
        Assert.StartsWith("null matrix given", e.Message);
        e = Assert.Throws<ArgumentException>(() => new SymmetricSchurDecomposition(new Matrix(2, 3, 1.0)));
        Assert.Equal("s", e.ParamName);
        Assert.StartsWith("input matrix must be square", e.Message);

        // A NaN off the diagonal stops every rotation (its comparisons are false), so the loop runs out.
        var nan = new Matrix(new[,] { { 1.0, double.NaN }, { double.NaN, 1.0 } });
        Assert.Equal("Too many iterations (100) reached", Assert.Throws<NanoQLException>(() => new SymmetricSchurDecomposition(nan)).Message);
    }

    [Fact]
    public void AnEmptyMatrixHasNoSingularValueDecomposition()
    {
        // QuantLib's decomposition reads its arrays' first elements on a matrix of no row or no column, as does the
        // Moore-Penrose inverse through it; a one-by-one matrix decomposes.
        foreach (Matrix empty in new[] { new Matrix(0, 3), new Matrix(3, 0), new Matrix() })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => new SVD(empty));
            Assert.Equal("matrix", e.ParamName);
            Assert.StartsWith("an empty matrix has no decomposition", e.Message, StringComparison.Ordinal);
            e = Assert.Throws<ArgumentException>(() => MoorePenroseInverseFunctions.MoorePenroseInverse(empty));
            Assert.Equal("A", e.ParamName);
            Assert.StartsWith("an empty matrix has no Moore-Penrose inverse", e.Message, StringComparison.Ordinal);
        }

        Assert.Equal([2.0], new SVD(new Matrix(new[,] { { 2.0 } })).SingularValues().ToArray());
        Assert.Equal(0.5, MoorePenroseInverseFunctions.MoorePenroseInverse(new Matrix(new[,] { { 2.0 } }))[0, 0], 15);
    }

    [Fact]
    public void CholeskyRefusals()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => CholeskyDecompositionFunctions.CholeskyDecomposition(new Matrix(2, 3, 1.0)));
        Assert.Equal("m", e.ParamName);
        Assert.StartsWith("input matrix is not a square matrix", e.Message);
        e = Assert.Throws<ArgumentException>(
            () => CholeskyDecompositionFunctions.CholeskyDecomposition(new Matrix(new[,] { { 1.0, 2.0 }, { 2.0, 1.0 } })));
        Assert.Equal("m", e.ParamName);
        Assert.StartsWith("input matrix is not positive definite", e.Message);
        e = Assert.Throws<ArgumentException>(
            () => CholeskyDecompositionFunctions.CholeskySolveFor(new Matrix(2, 2, 1.0), new Vector(3)));
        Assert.Equal("L", e.ParamName);
        Assert.StartsWith("Size of input matrix and vector does not match.", e.Message);
    }

    [Fact]
    public void PseudoSqrtRefusals()
    {
        var indefinite = new Matrix(new[,] { { 1.0, 0.9, 0.7 }, { 0.9, 1.0, -0.4 }, { 0.7, -0.4, 1.0 } });
        ArgumentException e = Assert.Throws<ArgumentException>(() => PseudoSqrtFunctions.PseudoSqrt(new Matrix(2, 3, 1.0)));
        Assert.Equal("matrix", e.ParamName);
        Assert.StartsWith("non square matrix: 2 rows, 3 columns", e.Message);
        e = Assert.Throws<ArgumentException>(() => PseudoSqrtFunctions.PseudoSqrt(indefinite));
        Assert.Equal("matrix", e.ParamName);
        Assert.Matches(@"^negative eigenvalue\(s\) \(-\d\.\d{6}e-\d\d\)", e.Message);
        e = Assert.Throws<ArgumentException>(
            () => PseudoSqrtFunctions.RankReducedSqrt(new Matrix(3, 2, 1.0), 2, 0.9, SalvagingAlgorithmType.Spectral));
        Assert.Equal("matrix", e.ParamName);
        Assert.StartsWith("non square matrix: 3 rows, 2 columns", e.Message);
        e = Assert.Throws<ArgumentException>(
            () => PseudoSqrtFunctions.RankReducedSqrt(indefinite, 2, 0.9, SalvagingAlgorithmType.None));
        Assert.Equal("matrix", e.ParamName);
        Assert.StartsWith("negative eigenvalue(s) (", e.Message);
        ArgumentOutOfRangeException o = Assert.Throws<ArgumentOutOfRangeException>(
            () => PseudoSqrtFunctions.RankReducedSqrt(indefinite, -1, 0.9, SalvagingAlgorithmType.Spectral));
        Assert.Equal("maxRank", o.ParamName);
        o = Assert.Throws<ArgumentOutOfRangeException>(
            () => PseudoSqrtFunctions.RankReducedSqrt(indefinite, 2, double.NaN, SalvagingAlgorithmType.Spectral));
        Assert.Equal("componentRetainedPercentage", o.ParamName);
        Assert.StartsWith("no eigenvalues retained", o.Message);
    }
}
