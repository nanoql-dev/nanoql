// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The statistics' shape: QuantLib's Gaussian and empirical risk layers, which derive from their type parameter, are
/// extension members over the accumulator they read, so each applies to every accumulator with what it reads (<see
/// cref="GaussianStatisticsExtensions"/> to <see cref="GeneralStatistics"/>, <see cref="IncrementalStatistics"/> and
/// <see cref="StatsHolder"/>, from their mean and standard deviation alone; <see cref="RiskStatisticsExtensions"/> to
/// the sample-keeping <see cref="IEmpiricalStatistics"/>); an accumulator is a working type that <c>Reset</c> empties;
/// and <see cref="IEmpiricalStatistics.Data"/> is a view of the samples.
/// </summary>
public class StatisticsExtensionTests
{
    private static readonly double[] Samples = [0.3, -1.2, 2.5, 0.7, -0.4, 1.9, -2.2, 0.05, 1.1, -0.6];

    [Fact]
    public void EachLayersMembersReadOnlyWhatTheirTypeParameterGives()
    {
        var general = new GeneralStatistics();
        var incremental = new IncrementalStatistics();
        foreach (double x in Samples)
        {
            general.Add(x);
            incremental.Add(x);
        }

        // The Gaussian members read the mean and the standard deviation: on a holder of the accumulator's two, the
        // same values bit for bit.
        foreach (ISummaryStatistics s in new ISummaryStatistics[] { general, incremental })
        {
            var holder = new StatsHolder(s.Mean(), s.StandardDeviation());
            Assert.Equal(holder.GaussianDownsideVariance(), Gaussian(s, x => x.GaussianDownsideVariance()));
            Assert.Equal(holder.GaussianRegret(0.5), Gaussian(s, x => x.GaussianRegret(0.5)));
            Assert.Equal(holder.GaussianPercentile(0.2), Gaussian(s, x => x.GaussianPercentile(0.2)));
            Assert.Equal(holder.GaussianTopPercentile(0.2), Gaussian(s, x => x.GaussianTopPercentile(0.2)));
            Assert.Equal(holder.GaussianValueAtRisk(0.95), Gaussian(s, x => x.GaussianValueAtRisk(0.95)));
            Assert.Equal(holder.GaussianExpectedShortfall(0.95), Gaussian(s, x => x.GaussianExpectedShortfall(0.95)));
            Assert.Equal(holder.GaussianShortfall(0.1), Gaussian(s, x => x.GaussianShortfall(0.1)));
            Assert.Equal(holder.GaussianAverageShortfall(0.1), Gaussian(s, x => x.GaussianAverageShortfall(0.1)));
        }

        // The empirical members compose as QuantLib's: the semi-variance the regret at the mean, the downside variance
        // the regret at 0, the deviations their square roots.
        Assert.Equal(general.Regret(general.Mean()), general.SemiVariance());
        Assert.Equal(general.Regret(0.0), general.DownsideVariance());
        Assert.Equal(Math.Sqrt(general.SemiVariance()), general.SemiDeviation());
        Assert.Equal(Math.Sqrt(general.DownsideVariance()), general.DownsideDeviation());
    }

    [Fact]
    public void ResetEmptiesAnAccumulator()
    {
        var general = new GeneralStatistics();
        var incremental = new IncrementalStatistics();
        foreach (double x in Samples)
        {
            general.Add(x, 2.0);
            incremental.Add(x, 2.0);
        }

        general.Reset();
        incremental.Reset();
        Assert.Equal(0, general.Samples);
        Assert.Equal(0, incremental.Samples);
        Assert.Equal(0, incremental.DownsideSamples);
        Assert.Equal(0.0, general.WeightSum());
        Assert.Equal(0.0, incremental.WeightSum());
        Assert.Equal(0.0, incremental.DownsideWeightSum());
        Assert.True(general.Data.IsEmpty);
        Assert.Throws<NanoQLException>(() => general.Mean());
        Assert.Throws<NanoQLException>(() => incremental.Min());

        // added to again, each reads as new: the minimum not the one before the reset
        general.Add(5.0);
        general.Add(7.0);
        incremental.Add(5.0);
        incremental.Add(7.0);
        Assert.Equal(5.0, general.Min());
        Assert.Equal(5.0, incremental.Min());
        Assert.Equal(6.0, general.Mean());
        Assert.Equal(6.0, incremental.Mean());
    }

    [Fact]
    public void DataIsAViewOfTheSamplesInTheirSetsOrder()
    {
        var s = new GeneralStatistics();
        s.Add(3.0, 1.0);
        s.Add(1.0, 2.0);
        s.Add(2.0, 0.5);
        Assert.Equal([(3.0, 1.0), (1.0, 2.0), (2.0, 0.5)], s.Data.ToArray());

        // a percentile sorts the samples, which the view then reads in order
        Assert.Equal(1.0, s.Percentile(0.5));
        Assert.Equal([(1.0, 2.0), (2.0, 0.5), (3.0, 1.0)], s.Data.ToArray());
        s.Add(0.5);
        Assert.Equal(4, s.Data.Length);
        Assert.Equal((0.5, 1.0), s.Data[3]);
    }

    private static double Gaussian<TStat>(TStat s, Func<TStat, double> member)
        where TStat : ISummaryStatistics => member(s);
}
