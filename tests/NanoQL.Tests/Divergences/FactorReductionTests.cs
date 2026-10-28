// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The factor reduction's loadings and its last pass (the S-core plan's CR7 (iv)): QuantLib reads the loadings from the
/// matrix's row at the largest eigenvalue's index, the input's first row, so an exact one-factor matrix with loadings
/// (0.9, 0.7, 0.5, 0.3, 0.8) gives (0.546, 0.63, 0.45, 0.27, 0.72); nanoql's principal factor iteration gives the
/// loadings, to QuantLib's stopping tolerance of 1e-6 relative change. And QuantLib refuses a reduction that converges
/// on its last allowed pass: its own iteration on that matrix converges at its second pass, which maxIters 1 allows,
/// and refuses there, "convergence not reached after 1 iterations". QuantLib's values are its own (this run's
/// <c>probes\c4_facts.cpp</c>, <c>c4_facts.out</c>).
/// </summary>
public class FactorReductionTests
{
    private static Matrix OneFactor(double[] b)
    {
        var m = new Matrix(b.Length, b.Length);
        for (int i = 0; i < b.Length; ++i)
        {
            for (int j = 0; j < b.Length; ++j)
            {
                m[i, j] = i == j ? 1.0 : b[i] * b[j];
            }
        }

        return m;
    }

    [Fact]
    public void AnExactOneFactorMatrixGivesItsLoadings()
    {
        double[] b = [0.9, 0.7, 0.5, 0.3, 0.8];
        double[] loadings = [.. FactorReductionFunctions.FactorReduction(OneFactor(b))];

        // QuantLib's: 0.54559600438419642, 0.63, 0.45000000000000001, 0.27000000000000002, 0.72000000000000008
        for (int i = 0; i < b.Length; ++i)
        {
            Assert.True(Math.Abs(loadings[i] - b[i]) < 2e-6 * b[i], $"{i}: {loadings[i]:R}");
        }
    }

    [Fact]
    public void ConvergenceOnTheLastAllowedPassIsAccepted()
    {
        // nanoql's iteration on this matrix converges on its 26th pass, the last one QuantLib's default of 25 iterations
        // allows; QuantLib's rule refuses convergence there, as its own iteration shows at maxIters 1
        Matrix m = OneFactor([0.9, 0.7, 0.5, 0.3, 0.8]);
        _ = FactorReductionFunctions.FactorReduction(m, 25);
        NanoQLException e = Assert.Throws<NanoQLException>(() => FactorReductionFunctions.FactorReduction(m, 24));
        Assert.Equal("convergence not reached after 25 iterations", e.Message);
    }

    [Fact]
    public void AZeroFirstLoadingStillIterates()
    {
        // a variable uncorrelated with the rest: its relative change is 0/0, NaN, which no comparison stops on, so a
        // zero first stopped the iteration at its first pass, every loading up to 0.136 off at any maxIters (the code
        // review's C4-O3); measured by its successor's magnitude, the zero first converges as a zero elsewhere does,
        // and the default 25 iterations refuse both
        double[] b = [0.0, 0.9, 0.7, 0.5, 0.8];
        double[] loadings = [.. FactorReductionFunctions.FactorReduction(OneFactor(b), 200)];
        for (int i = 0; i < b.Length; ++i)
        {
            Assert.True(Math.Abs(loadings[i] - b[i]) < 2e-6, $"{i}: {loadings[i]:R}");
        }

        Assert.Throws<NanoQLException>(() => FactorReductionFunctions.FactorReduction(OneFactor(b)));
        Assert.Throws<NanoQLException>(() => FactorReductionFunctions.FactorReduction(OneFactor([0.9, 0.0, 0.7, 0.5, 0.8])));
    }
}
