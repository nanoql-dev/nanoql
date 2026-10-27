// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="MarketModel"/>'s covariance cache (the S-mm plan's SD3, the rule on QuantLib's speed caches): computed once
/// from the model's own pseudo-roots, whichever is read first, under a lock, each read a copy, so every answer is a fresh
/// model's on whichever thread: QuantLib's arithmetic, each step's pseudo-root times its transpose and their running
/// sums.
/// </summary>
public class MarketModelCovarianceCacheTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0, 2.5];

    [Fact]
    public void TheCacheIsAFreshModelsAnswerOnEveryThread()
    {
        FlatVol fresh = Model(), shared = Model();
        Matrix[] expected = [.. Enumerable.Range(0, 4).Select(fresh.TotalCovariance)];
        var seen = new Matrix[16];
        Parallel.For(0, seen.Length, i => seen[i] = i % 2 == 0 ? shared.TotalCovariance(i % 4) : shared.Covariance(i % 4));
        for (int i = 0; i < seen.Length; ++i)
        {
            Assert.Equal(i % 2 == 0 ? expected[i % 4] : fresh.Covariance(i % 4), seen[i]);
        }
    }

    [Fact]
    public void TheCacheIsQuantLibsSums()
    {
        FlatVol m = Model();
        Matrix total = m.Covariance(0);
        for (int j = 0; j < m.NumberOfSteps; ++j)
        {
            Matrix a = m.PseudoRoot(j);
            Assert.Equal(a * MatrixFunctions.Transpose(a), m.Covariance(j));
            if (j > 0)
            {
                total = total + m.Covariance(j);
            }

            Assert.Equal(total, m.TotalCovariance(j));
        }

        double[] vols = [.. m.TimeDependentVolatility(2)];
        Assert.Equal(Math.Sqrt(m.Covariance(0)[2, 2] / 0.5), vols[0]);
    }

    private static FlatVol Model() =>
        new([0.2, 0.22, 0.24, 0.26], new ExponentialForwardCorrelation(RateTimes, 0.4, 0.3), new EvolutionDescription(RateTimes), 3, [0.03, 0.035, 0.04, 0.045], [0.0, 0.0, 0.0, 0.0]);
}
