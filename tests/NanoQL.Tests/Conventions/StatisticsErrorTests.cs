// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types and messages of the statistics' refusals (playbook §2), which a vector's
/// <c>{"throws": true}</c> can't tell apart: an argument a member refuses throws an
/// <see cref="ArgumentOutOfRangeException"/> naming it, with QuantLib's text; a set too small or of no weight, a
/// <see cref="NanoQLException"/> with QuantLib's text.
/// </summary>
public class StatisticsErrorTests
{
    [Fact]
    public void GeneralStatisticsRefusals()
    {
        var s = new GeneralStatistics();
        ArgumentOutOfRangeException o = Assert.Throws<ArgumentOutOfRangeException>(() => s.Add(1.0, -1.0));
        Assert.Equal("weight", o.ParamName);
        Assert.StartsWith("negative weight not allowed", o.Message);
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => s.Mean()).Message);
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => s.Min()).Message);
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => s.Max()).Message);
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => s.Percentile(0.5)).Message);
        s.Add(1.0);
        Assert.Equal("sample number <=1, unsufficient", Assert.Throws<NanoQLException>(() => s.Variance()).Message);
        s.Add(2.0);
        Assert.Equal("sample number <=2, unsufficient", Assert.Throws<NanoQLException>(() => s.Skewness()).Message);
        s.Add(3.0);
        Assert.Equal("sample number <=3, unsufficient", Assert.Throws<NanoQLException>(() => s.Kurtosis()).Message);
        foreach (double y in new[] { 0.0, 1.5, double.NaN })
        {
            o = Assert.Throws<ArgumentOutOfRangeException>(() => s.Percentile(y));
            Assert.Equal("y", o.ParamName);
            Assert.StartsWith("percentile (", o.Message);
            o = Assert.Throws<ArgumentOutOfRangeException>(() => s.TopPercentile(y));
            Assert.Equal("y", o.ParamName);
        }

        Assert.StartsWith(
            "percentile (1.5) must be in (0.0, 1.0]", Assert.Throws<ArgumentOutOfRangeException>(() => s.Percentile(1.5)).Message);
        o = Assert.Throws<ArgumentOutOfRangeException>(() => s.Reserve(-1));
        Assert.Equal("n", o.ParamName);
    }

    [Fact]
    public void IncrementalStatisticsRefusals()
    {
        var s = new IncrementalStatistics();
        ArgumentOutOfRangeException o = Assert.Throws<ArgumentOutOfRangeException>(() => s.Add(1.0, -0.5));
        Assert.Equal("weight", o.ParamName);
        Assert.StartsWith("negative weight (-0.5) not allowed", o.Message);
        Assert.Equal("sampleWeight_= 0, unsufficient", Assert.Throws<NanoQLException>(() => s.Mean()).Message);
        Assert.Equal("sampleWeight_= 0, unsufficient", Assert.Throws<NanoQLException>(() => s.Variance()).Message);
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => s.Min()).Message);
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => s.Max()).Message);
        Assert.Equal("sampleWeight_= 0, unsufficient", Assert.Throws<NanoQLException>(() => s.DownsideVariance()).Message);
        s.Add(-1.0);
        Assert.Equal("sample number <= 1, unsufficient", Assert.Throws<NanoQLException>(() => s.Variance()).Message);
        Assert.Equal(
            "sample number <= 1, unsufficient", Assert.Throws<NanoQLException>(() => s.DownsideVariance()).Message);
        s.Add(2.0);
        Assert.Equal("sample number <= 2, unsufficient", Assert.Throws<NanoQLException>(() => s.Skewness()).Message);
        s.Add(3.0);
        Assert.Equal("sample number <= 3, unsufficient", Assert.Throws<NanoQLException>(() => s.Kurtosis()).Message);
    }

    [Fact]
    public void RiskAndGaussianMeasureRefusals()
    {
        var s = new GeneralStatistics();
        foreach (double x in new[] { 1.0, -2.0, 0.5, 3.0, -0.25 })
        {
            s.Add(x);
        }

        foreach (double p in new[] { 0.5, 1.0 })
        {
            ArgumentOutOfRangeException o = Assert.Throws<ArgumentOutOfRangeException>(() => s.ValueAtRisk(p));
            Assert.Equal("percentile", o.ParamName);
            Assert.StartsWith($"percentile ({(p == 1.0 ? "1" : "0.5")}) out of range [0.9, 1.0)", o.Message);
            Assert.Equal("percentile", Assert.Throws<ArgumentOutOfRangeException>(() => s.PotentialUpside(p)).ParamName);
            Assert.Equal("percentile", Assert.Throws<ArgumentOutOfRangeException>(() => s.ExpectedShortfall(p)).ParamName);
            o = Assert.Throws<ArgumentOutOfRangeException>(() => s.GaussianValueAtRisk(p));
            Assert.StartsWith($"percentile ({(p == 1.0 ? "1" : "0.5")}) out of range [0.9, 1)", o.Message);
            Assert.Equal(
                "percentile", Assert.Throws<ArgumentOutOfRangeException>(() => s.GaussianPotentialUpside(p)).ParamName);
            Assert.Equal(
                "percentile", Assert.Throws<ArgumentOutOfRangeException>(() => s.GaussianExpectedShortfall(p)).ParamName);
        }

        Assert.StartsWith(
            "percentile (0) must be > 0.0", Assert.Throws<ArgumentOutOfRangeException>(() => s.GaussianPercentile(0.0)).Message);
        Assert.StartsWith(
            "percentile (1) must be < 1.0", Assert.Throws<ArgumentOutOfRangeException>(() => s.GaussianPercentile(1.0)).Message);
        Assert.Equal(
            "samples under target <= 1, unsufficient", Assert.Throws<NanoQLException>(() => s.Regret(-1.0)).Message);
        Assert.Equal("no data below the target", Assert.Throws<NanoQLException>(() => s.AverageShortfall(-5.0)).Message);
        var empty = new GeneralStatistics();
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => empty.Shortfall(0.0)).Message);
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => empty.ExpectedShortfall(0.95)).Message);

        // A set too small for both the mean and the standard deviation reports the standard deviation's, which MSVC
        // reads first where QuantLib passes both to a distribution's constructor.
        Assert.Equal(
            "sample number <=1, unsufficient", Assert.Throws<NanoQLException>(() => empty.GaussianShortfall(0.0)).Message);
        Assert.Equal(
            "sample number <=1, unsufficient", Assert.Throws<NanoQLException>(() => empty.GaussianPercentile(0.5)).Message);
        Assert.Equal("empty sample set", Assert.Throws<NanoQLException>(() => empty.GaussianRegret(0.0)).Message);
    }
}
