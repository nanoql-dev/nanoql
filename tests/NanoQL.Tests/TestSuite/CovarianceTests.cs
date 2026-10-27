// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/covariance.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;
using static NanoQL.Numerics.MatrixFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CovarianceTests
{
    // BOOST_ERROR records a failure and goes on; the case fails at its end.
    private readonly List<string> errors = [];

    [Fact]
    public void TestRankReduction()
    {
        // Testing matrix rank reduction salvaging algorithms...

        Real expected, calculated;

        Size n = 3;

        var badCorr = new Matrix(n, n);
        badCorr[0, 0] = 1.0; badCorr[0, 1] = 0.9; badCorr[0, 2] = 0.7;
        badCorr[1, 0] = 0.9; badCorr[1, 1] = 1.0; badCorr[1, 2] = 0.3;
        badCorr[2, 0] = 0.7; badCorr[2, 1] = 0.3; badCorr[2, 2] = 1.0;

        Matrix goodCorr = GoodCorrelation(n);

        Matrix b = PseudoSqrtFunctions.RankReducedSqrt(badCorr, 3, 1.0, SalvagingAlgorithmType.Spectral);
        Matrix calcCorr = b * Transpose(b);

        for (Size i = 0; i < n; i++)
        {
            for (Size j = 0; j < n; j++)
            {
                expected = goodCorr[i, j];
                calculated = calcCorr[i, j];
                if (Math.Abs(calculated - expected) > 1.0e-10)
                {
                    errors.Add(
                        $"Salvaging correlation with spectral alg through rankReducedSqrt cor[{i}][{j}]:\n"
                        + $"    calculated: {G10(calculated)}\n    expected:   {G10(expected)}");
                }
            }
        }

        Matrix badCov = BadCovariance(n);

        b = PseudoSqrtFunctions.PseudoSqrt(badCov, SalvagingAlgorithmType.Spectral);
        b = PseudoSqrtFunctions.RankReducedSqrt(badCov, 3, 1.0, SalvagingAlgorithmType.Spectral);
        Matrix goodCov = b * Transpose(b);

        Real error = Norm(goodCov - badCov);
        if (error > 4.0e-4)
        {
            errors.Add(
                $"{error.ToString("E6", CultureInfo.InvariantCulture)} error while salvaging covariance matrix with "
                + $"spectral alg through rankReducedSqrt\ninput matrix:\n{badCov}salvaged matrix:\n{goodCov}");
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestSalvagingMatrix()
    {
        // Testing positive semi-definiteness salvaging algorithms...

        Real expected, calculated;

        Size n = 3;

        var badCorr = new Matrix(n, n);
        badCorr[0, 0] = 1.0; badCorr[0, 1] = 0.9; badCorr[0, 2] = 0.7;
        badCorr[1, 0] = 0.9; badCorr[1, 1] = 1.0; badCorr[1, 2] = 0.3;
        badCorr[2, 0] = 0.7; badCorr[2, 1] = 0.3; badCorr[2, 2] = 1.0;

        Matrix goodCorr = GoodCorrelation(n);

        Matrix b = PseudoSqrtFunctions.PseudoSqrt(badCorr, SalvagingAlgorithmType.Spectral);
        Matrix calcCorr = b * Transpose(b);

        for (Size i = 0; i < n; i++)
        {
            for (Size j = 0; j < n; j++)
            {
                expected = goodCorr[i, j];
                calculated = calcCorr[i, j];
                if (Math.Abs(calculated - expected) > 1.0e-10)
                {
                    errors.Add(
                        $"SalvagingCorrelation with spectral alg cor[{i}][{j}]:\n"
                        + $"    calculated: {G10(calculated)}\n    expected:   {G10(expected)}");
                }
            }
        }

        Matrix badCov = BadCovariance(n);

        b = PseudoSqrtFunctions.PseudoSqrt(badCov, SalvagingAlgorithmType.Spectral);
        Matrix goodCov = b * Transpose(b);

        Real error = Norm(goodCov - badCov);
        if (error > 4.0e-4)
        {
            errors.Add(
                $"{error.ToString("E6", CultureInfo.InvariantCulture)} error while salvaging covariance matrix with "
                + $"spectral alg\ninput matrix:\n{badCov}salvaged matrix:\n{goodCov}");
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestCovariance()
    {
        // Testing covariance and correlation calculations...

        Real[][] data =
        [
            [3.0, 9.0],
            [2.0, 7.0],
            [4.0, 12.0],
            [5.0, 15.0],
            [6.0, 17.0],
        ];
        Real[] weights = [.. Enumerable.Repeat(1.0, data.Length)];

        Size i, j, n = data[0].Length;

        var expCor = new Matrix(n, n);
        expCor[0, 0] = 1.0000000000000000; expCor[0, 1] = 0.9970544855015813;
        expCor[1, 0] = 0.9970544855015813; expCor[1, 1] = 1.0000000000000000;

        var s = new SequenceStatistics(n);
        Real[] temp = new Real[n];

        for (i = 0; i < data.Length; i++)
        {
            for (j = 0; j < n; j++)
            {
                temp[j] = data[i][j];
            }

            s.Add(temp, weights[i]);
        }

        Real[] std = [.. s.StandardDeviation()];
        Matrix calcCov = s.Covariance();
        Matrix calcCor = s.Correlation();

        var expCov = new Matrix(n, n);
        for (i = 0; i < n; i++)
        {
            expCov[i, i] = std[i] * std[i];
            for (j = 0; j < i; j++)
            {
                expCov[i, j] = expCov[j, i] = expCor[i, j] * std[i] * std[j];
            }
        }

        Real expected, calculated;
        for (i = 0; i < n; i++)
        {
            for (j = 0; j < n; j++)
            {
                expected = expCor[i, j];
                calculated = calcCor[i, j];
                if (Math.Abs(calculated - expected) > 1.0e-10)
                {
                    errors.Add(
                        $"SequenceStatistics cor[{i}][{j}]:\n    calculated: {G10(calculated)}\n    expected:   "
                        + $"{G10(expected)}");
                }

                expected = expCov[i, j];
                calculated = calcCov[i, j];
                if (Math.Abs(calculated - expected) > 1.0e-10)
                {
                    errors.Add(
                        $"SequenceStatistics cov[{i}][{j}]:\n    calculated: {G10(calculated)}\n    expected:   "
                        + $"{G10(expected)}");
                }
            }
        }

        calcCov = GetCovarianceFunctions.GetCovariance(std, expCor);

        for (i = 0; i < n; i++)
        {
            for (j = 0; j < n; j++)
            {
                calculated = calcCov[i, j];
                expected = expCov[i, j];
                if (Math.Abs(calculated - expected) > 1.0e-10)
                {
                    errors.Add(
                        $"getCovariance cov[{i}][{j}]:\n    calculated: {G10(calculated)}\n    expected:   "
                        + $"{G10(expected)}");
                }
            }
        }

        var covDecomposition = new CovarianceDecomposition(expCov);
        calcCor = covDecomposition.CorrelationMatrix();
        Vector calcStd = covDecomposition.StandardDeviations();

        for (i = 0; i < n; i++)
        {
            calculated = calcStd[i];
            expected = std[i];
            if (Math.Abs(calculated - expected) > 1.0e-16)
            {
                errors.Add(
                    $"CovarianceDecomposition standardDev[{i}]:\n    calculated: {E16(calculated)}\n    expected:   "
                    + $"{E16(expected)}");
            }

            for (j = 0; j < n; j++)
            {
                calculated = calcCor[i, j];
                expected = expCor[i, j];
                if (Math.Abs(calculated - expected) > 1.0e-14)
                {
                    errors.Add(
                        $"\nCovarianceDecomposition corr[{i}][{j}]:\n    calculated: "
                        + $"{calculated.ToString("E14", CultureInfo.InvariantCulture)}\n    expected:   "
                        + $"{expected.ToString("E14", CultureInfo.InvariantCulture)}");
                }
            }
        }

        Assert.Empty(errors);
    }

    private static Matrix GoodCorrelation(Size n)
    {
        var goodCorr = new Matrix(n, n);
        goodCorr[0, 0] = goodCorr[1, 1] = goodCorr[2, 2] = 1.00000000000;
        goodCorr[0, 1] = goodCorr[1, 0] = 0.894024408508599;
        goodCorr[0, 2] = goodCorr[2, 0] = 0.696319066114392;
        goodCorr[1, 2] = goodCorr[2, 1] = 0.300969036104592;
        return goodCorr;
    }

    private static Matrix BadCovariance(Size n)
    {
        var badCov = new Matrix(n, n);
        badCov[0, 0] = 0.04000; badCov[0, 1] = 0.03240; badCov[0, 2] = 0.02240;
        badCov[1, 0] = 0.03240; badCov[1, 1] = 0.03240; badCov[1, 2] = 0.00864;
        badCov[2, 0] = 0.02240; badCov[2, 1] = 0.00864; badCov[2, 2] = 0.02560;
        return badCov;
    }

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

    private static string G10(Real x) => x.ToString("G10", CultureInfo.InvariantCulture);

    private static string E16(Real x) => x.ToString("E16", CultureInfo.InvariantCulture);
}
