// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An empty matrix's pseudo square root is refused with QuantLib's master's text, "matrix must not be empty", before
/// the decomposition reads it (#2846, f3f789067), where QuantLib 1.44 refuses it inside the symmetric Schur
/// decomposition with "null matrix given" (symmetricschurdecomposition.cpp:29).
/// </summary>
public class PseudoSqrtEmptyMatrixTests
{
    [Fact]
    public void AnEmptyMatrixIsRefusedBeforeItsDecomposition()
    {
        // QuantLib 1.44, every algorithm: "null matrix given" (the wheel's pseudoSqrt(Matrix(), Spectral)).
        foreach (SalvagingAlgorithmType sa in Enum.GetValues<SalvagingAlgorithmType>())
        {
            ArgumentException e = Assert.Throws<ArgumentException>(() => PseudoSqrtFunctions.PseudoSqrt(new Matrix(), sa));
            Assert.Equal("matrix", e.ParamName);
            Assert.StartsWith("matrix must not be empty", e.Message);
        }

        foreach (SalvagingAlgorithmType sa in new[]
                 {
                     SalvagingAlgorithmType.None, SalvagingAlgorithmType.Spectral, SalvagingAlgorithmType.Higham,
                 })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => PseudoSqrtFunctions.RankReducedSqrt(new Matrix(), 1, 1.0, sa));
            Assert.Equal("matrix", e.ParamName);
            Assert.StartsWith("matrix must not be empty", e.Message);
        }
    }
}
