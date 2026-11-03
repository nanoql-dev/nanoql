// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using static NanoQL.Numerics.MatrixFunctions;
using static NanoQL.Numerics.PseudoSqrtFunctions;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's master after the pin (#2846) adds coverage cases for the pseudo square root (test-suite/matrices.cpp at
/// a23190af1: testPseudoSqrtSalvagingAlgorithms, testPseudoSqrtValidationAndPrincipalTolerance,
/// testRankReducedPseudoSqrtBranches, testPseudoSqrtDegenerateInputs). Their lines that pass on 1.44 are here; the one
/// that doesn't, an empty matrix's text, is <c>Divergences/PseudoSqrtEmptyMatrixTests.cs</c>.
/// </summary>
public class PseudoSqrtCoverageTests
{
    private static readonly Matrix M1 = new(new[,] { { 1.0, 0.9, 0.7 }, { 0.9, 1.0, 0.4 }, { 0.7, 0.4, 1.0 } });
    private static readonly Matrix M2 = new(new[,] { { 1.0, 0.9, 0.7 }, { 0.9, 1.0, 0.3 }, { 0.7, 0.3, 1.0 } });
    private static readonly Matrix M5 = new(new[,]
    {
        { 2.0, -1.0, 0.0, 0.0 }, { -1.0, 2.0, -1.0, 0.0 }, { 0.0, -1.0, 2.0, -1.0 }, { 0.0, 0.0, -1.0, 2.0 },
    });

    [Fact]
    public void DegenerateAndNonPositiveMatricesGiveValidRoots()
    {
        // testPseudoSqrtDegenerateInputs
        var zero = new Matrix(3, 3, 0.0);
        foreach (Matrix root in new[]
                 {
                     PseudoSqrt(zero, SalvagingAlgorithmType.Spectral), PseudoSqrt(zero, SalvagingAlgorithmType.Principal),
                     RankReducedSqrt(zero, 10, 1.0, SalvagingAlgorithmType.Spectral),
                 })
        {
            foreach (double x in root)
            {
                Assert.True(double.IsFinite(x));
            }

            Assert.True(Norm(root * Transpose(root)) <= 1.0e-12);
        }

        var singular = new Matrix(2, 2, 1.0);
        foreach (Matrix root in new[]
                 {
                     PseudoSqrt(singular, SalvagingAlgorithmType.Spectral),
                     PseudoSqrt(singular, SalvagingAlgorithmType.Principal),
                     RankReducedSqrt(singular, 10, 1.0, SalvagingAlgorithmType.None),
                 })
        {
            Assert.True(Norm((root * Transpose(root)) - singular) <= 1.0e-12);
        }

        // testPseudoSqrtSalvagingAlgorithms
        foreach (SalvagingAlgorithmType sa in new[]
                 {
                     SalvagingAlgorithmType.Spectral, SalvagingAlgorithmType.Hypersphere, SalvagingAlgorithmType.LowerDiagonal,
                 })
        {
            Matrix root = PseudoSqrt(M2, sa);
            Matrix covariance = root * Transpose(root);
            Vector eigenvalues = new SymmetricSchurDecomposition(covariance).Eigenvalues();
            for (int i = 0; i < M2.Rows; ++i)
            {
                Assert.True(Math.Abs(covariance[i, i] - M2[i, i]) <= 1.0e-12, $"{sa}: diagonal {i}");
                Assert.True(eigenvalues[i] >= -1.0e-12, $"{sa}: eigenvalue {i}");
            }
        }

        Matrix positiveHypersphere = PseudoSqrt(M1, SalvagingAlgorithmType.Hypersphere);
        Matrix positiveLowerDiagonal = PseudoSqrt(M1, SalvagingAlgorithmType.LowerDiagonal);
        Assert.True(Norm((positiveHypersphere * Transpose(positiveHypersphere)) - M1) <= 1.0e-12);
        Assert.True(Norm((positiveLowerDiagonal * Transpose(positiveLowerDiagonal)) - M1) <= 1.0e-12);
    }

    [Fact]
    public void RefusalsAndTolerancesAreQuantLibs()
    {
        // testPseudoSqrtValidationAndPrincipalTolerance
        ArgumentException e = Assert.Throws<ArgumentException>(() => PseudoSqrt(M2, SalvagingAlgorithmType.None));
        Assert.StartsWith("negative eigenvalue(s)", e.Message);
        Assert.Equal("matrix", e.ParamName);
        e = Assert.Throws<ArgumentException>(() => PseudoSqrt(M2, SalvagingAlgorithmType.Principal));
        Assert.StartsWith("negative eigenvalue(s)", e.Message);
        e = Assert.Throws<ArgumentException>(() => PseudoSqrt(new Matrix(2, 3, 1.0), SalvagingAlgorithmType.None));
        Assert.StartsWith("non square matrix", e.Message);
        ArgumentOutOfRangeException o = Assert.Throws<ArgumentOutOfRangeException>(
            () => PseudoSqrt(M1, (SalvagingAlgorithmType)99));
        Assert.StartsWith("unknown salvaging algorithm", o.Message);

        Matrix nearlyPositive = M1.Clone();
        nearlyPositive[0, 0] += 1.0e-14;
        nearlyPositive[1, 1] += 1.0e-14;
        Matrix principal = PseudoSqrt(nearlyPositive, SalvagingAlgorithmType.Principal);
        Assert.True(Norm((principal * Transpose(principal)) - nearlyPositive) <= 1.0e-12);

        var almostSemidefinite = new Matrix(1, 1, -2.220446049250313e-16);
        Assert.True(Math.Abs(PseudoSqrt(almostSemidefinite, SalvagingAlgorithmType.Principal)[0, 0]) <= 1.0e-12);

        // testRankReducedPseudoSqrtBranches
        Matrix fullRank = RankReducedSqrt(M1, 3, 1.0, SalvagingAlgorithmType.None);
        Matrix rankOne = RankReducedSqrt(M1, 1, 1.0, SalvagingAlgorithmType.None);
        Matrix halfSpectrum = RankReducedSqrt(M1, 3, 0.5, SalvagingAlgorithmType.None);
        Assert.Equal(3, fullRank.Columns);
        Assert.Equal(1, rankOne.Columns);
        Assert.True(halfSpectrum.Columns <= 3);
        foreach (Matrix root in new[] { fullRank, rankOne, halfSpectrum })
        {
            Matrix covariance = root * Transpose(root);
            for (int i = 0; i < M1.Rows; ++i)
            {
                Assert.True(Math.Abs(covariance[i, i] - M1[i, i]) <= 1.0e-12);
            }
        }

        Assert.Equal(3, RankReducedSqrt(M2, 3, 1.0, SalvagingAlgorithmType.Spectral).Columns);
        Assert.Equal(4, RankReducedSqrt(M5, 4, 1.0, SalvagingAlgorithmType.Higham).Columns);
        o = Assert.Throws<ArgumentOutOfRangeException>(() => RankReducedSqrt(M1, 3, 0.0, SalvagingAlgorithmType.None));
        Assert.StartsWith("no eigenvalues retained", o.Message);
        o = Assert.Throws<ArgumentOutOfRangeException>(() => RankReducedSqrt(M1, 3, 1.1, SalvagingAlgorithmType.None));
        Assert.StartsWith("percentage to be retained > 100%", o.Message);
        o = Assert.Throws<ArgumentOutOfRangeException>(() => RankReducedSqrt(M1, 0, 1.0, SalvagingAlgorithmType.None));
        Assert.StartsWith("max rank required < 1", o.Message);
        o = Assert.Throws<ArgumentOutOfRangeException>(
            () => RankReducedSqrt(M1, 3, 1.0, SalvagingAlgorithmType.Hypersphere));
        Assert.StartsWith("unknown or invalid salvaging algorithm", o.Message);
    }

    // matrices.cpp's norm(const Matrix&): the Frobenius norm.
    private static double Norm(Matrix m)
    {
        double sum = 0.0;
        foreach (double x in m)
        {
            sum += x * x;
        }

        return Math.Sqrt(sum);
    }
}
