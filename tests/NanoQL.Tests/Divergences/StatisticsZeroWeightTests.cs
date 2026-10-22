// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Where the samples a member averages weigh 0 in all, the statistics refuse (the S-sr plan's SSR6 (ix)), where
/// QuantLib divides 0 by 0 and returns NaN: <see cref="GeneralStatistics.ExpectationValue(Func{double, double}, Func{double, bool})"/>,
/// which every empirical member reads, refuses with the text QuantLib's percentile gives a set of zero weight, "empty
/// sample set" (generalstatistics.hpp:115-133, generalstatistics.cpp:94-95); and <see cref="IncrementalStatistics"/>'s
/// skewness and kurtosis refuse as its mean does, "sampleWeight_= 0, unsufficient" (incrementalstatistics.cpp:42).
/// QuantLib's check: its own refusals.
/// </summary>
public class StatisticsZeroWeightTests
{
    private static readonly double[] Data = [1.0, -2.0, 3.0, -0.5];

    [Fact]
    public void EveryEmpiricalMemberRefusesSamplesOfNoWeight()
    {
        var s = new GeneralStatistics();
        foreach (double x in Data)
        {
            s.Add(x, 0.0);
        }

        // QuantLib (the wheel's RiskStatistics): mean, variance, standardDeviation, errorEstimate, skewness, kurtosis,
        // downsideVariance, regret(0), shortfall(0) and averageShortfall(0) NaN; semiVariance refuses "samples under
        // target <= 1, unsufficient" (nothing lies below a NaN mean); valueAtRisk and expectedShortfall refuse "empty
        // sample set", through the percentile.
        Func<double>[] members =
        [
            () => s.Mean(), () => s.Variance(), () => s.StandardDeviation(), () => s.ErrorEstimate(),
            () => s.Skewness(), () => s.Kurtosis(), () => s.DownsideVariance(), () => s.Regret(0.0),
            () => s.Shortfall(0.0), () => s.AverageShortfall(0.0), () => s.SemiVariance(), () => s.ValueAtRisk(0.95),
            () => s.ExpectedShortfall(0.95), () => s.GaussianValueAtRisk(0.95), () => s.Percentile(0.5),
        ];
        foreach (Func<double> member in members)
        {
            Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => member()).Message);
        }

        // Weighted samples elsewhere: the members over the range below 0, whose samples weigh 0, refuse alike, where
        // QuantLib's regret(0), downsideVariance and averageShortfall(0) are NaN; the whole set's mean is defined.
        var partly = new GeneralStatistics();
        partly.Add(1.0, 1.0);
        partly.Add(3.0, 2.0);
        partly.Add(-2.0, 0.0);
        partly.Add(-0.5, 0.0);
        Assert.Equal(7.0 / 3.0, partly.Mean());
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => partly.Regret(0.0)).Message);
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => partly.DownsideVariance()).Message);
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => partly.AverageShortfall(0.0)).Message);
    }

    [Fact]
    public void IncrementalSkewnessAndKurtosisRefuseSamplesOfNoWeight()
    {
        var s = new IncrementalStatistics();
        foreach (double x in Data)
        {
            s.Add(x, 0.0);
        }

        // QuantLib (the wheel's IncrementalStatistics): skewness and kurtosis NaN; mean and variance refuse
        // "sampleWeight_= 0, unsufficient"; min -2 and max 3.
        Assert.Equal("sampleWeight_= 0, unsufficient", Assert.Throws<NanoQLException>(() => s.Skewness()).Message);
        Assert.Equal("sampleWeight_= 0, unsufficient", Assert.Throws<NanoQLException>(() => s.Kurtosis()).Message);
        Assert.Equal("sampleWeight_= 0, unsufficient", Assert.Throws<NanoQLException>(() => s.Mean()).Message);
        Assert.Equal(-2.0, s.Min());
        Assert.Equal(3.0, s.Max());
    }
}
