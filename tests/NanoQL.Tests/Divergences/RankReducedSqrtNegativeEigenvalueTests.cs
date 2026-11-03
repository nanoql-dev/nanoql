// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <c>rankReducedSqrt</c> with a retained negative eigenvalue: QuantLib floors negative eigenvalues under
/// <c>Spectral</c> only (<c>pseudosqrt.cpp:496</c>), so the eigenvalues <c>None</c> accepts down to −1e-16
/// (<c>:486-491</c>) and those of Higham's adjusted matrix, positive semi-definite only to its projections' tolerance
/// (<c>:498-505</c>), reach <c>std::sqrt(eigenValues[i])</c> (<c>:531</c>): a NaN column, and
/// <c>normalizePseudoRoot</c> skips every row whose norm is NaN (<c>:64</c>), leaving the others unnormalized. The port
/// floors the root at 0. QuantLib's check: its own root at the rank that keeps the non-negative eigenvalues, with zero
/// columns appended, bit for bit (the harness's).
/// </summary>
public class RankReducedSqrtNegativeEigenvalueTests
{
    [Fact]
    public void HighamsNegativeAdjustedEigenvalueGivesAZeroColumn()
    {
        // Higham's adjusted matrix has the eigenvalues 1.7637, 1.2363 and -2.49e-7. QuantLib's root at full rank and
        // 1.0: the third column NaN, the first two unnormalized (0.99500513879934 for its first element).
        var m = new Matrix(new[,] { { 1.0, 0.9, 0.7 }, { 0.9, 1.0, -0.4 }, { 0.7, -0.4, 1.0 } });
        double[,] quantLibRank2 =
        {
            { 0.9950050853868101, 0.09982424582428256 },
            { 0.7626008403216061, -0.6468693518329496 },
            { 0.43833043116021425, 0.8988139034966586 },
        };
        foreach (int rank in new[] { 3, 100 })
        {
            AssertPadded(quantLibRank2, PseudoSqrtFunctions.RankReducedSqrt(m, rank, 1.0, SalvagingAlgorithmType.Higham));
        }
    }

    [Fact]
    public void NonesRoundOffNegativeEigenvalueGivesAZeroColumn()
    {
        // A rank-1 covariance matrix whose third eigenvalue Schur leaves at a round-off negative within None's -1e-16:
        // QuantLib's root at full rank and 1.0 has a NaN third column (the harness).
        var m = new Matrix(new[,]
        {
            { 3.04929456813921e-05, -0.0004660394908315418, -0.0004888022776101121 },
            { -0.0004660394908315418, 0.007122723048270855, 0.007470618514685746 },
            { -0.0004888022776101121, 0.007470618514685746, 0.007835506254242777 },
        });
        double[,] quantLibRank2 =
        {
            { 0.005522041803662129, 0.0 },
            { -0.08439622650492648, 0.0 },
            { -0.08851839500489588, 0.0 },
        };
        AssertPadded(quantLibRank2, PseudoSqrtFunctions.RankReducedSqrt(m, 100, 1.0, SalvagingAlgorithmType.None));
    }

    // The root equals QuantLib's lower-rank root bit for bit, with zero columns after it.
    private static void AssertPadded(double[,] expected, Matrix root)
    {
        int rows = expected.GetLength(0), columns = expected.GetLength(1);
        Assert.Equal(rows, root.Rows);
        Assert.Equal(rows, root.Columns);
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < rows; j++)
            {
                Assert.Equal(j < columns ? expected[i, j] : 0.0, root[i, j]);
            }
        }
    }
}
