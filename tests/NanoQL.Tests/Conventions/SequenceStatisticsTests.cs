// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The sequence and convergence statistics' shape: QuantLib's lazy dimension, set by the first sample of a statistics
/// built without one; its refusals; the lifted members one value per component; the empirical risk members extension
/// members constrained to statistics that keep their samples, and the incremental statistics' own downside members; and
/// the convergence table, which a sample added through the held statistics bypasses.
/// </summary>
public class SequenceStatisticsTests
{
    [Fact]
    public void TheFirstSampleSetsTheDimension()
    {
        var s = new SequenceStatistics();
        Assert.Equal(0, s.Size);
        Assert.Equal(0, s.Samples);
        Assert.Equal(0.0, s.WeightSum());
        Assert.Empty(s.Mean());
        s.Add([1.0, 2.0, 3.0], 2.0);
        Assert.Equal(3, s.Size);
        Assert.Equal(1, s.Samples);
        Assert.Equal(2.0, s.WeightSum());
        Assert.Equal("sample size mismatch: 3 required, 2 provided", Assert.Throws<NanoQLException>(() => s.Add([1.0, 2.0])).Message);
        Assert.Equal("sample error: end<=begin", Assert.Throws<NanoQLException>(() => new SequenceStatisticsInc().Add([])).Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequenceStatistics(-1));

        // a reset to the same dimension empties each component; to another replaces them
        s.Add([3.0, 4.0, 5.0]);
        s.Reset(3);
        Assert.Equal(0, s.Samples);
        s.Add([1.0, 1.0, 1.0]);
        Assert.Equal([1.0, 1.0, 1.0], s.Mean());
        s.Reset(2);
        Assert.Equal(2, s.Size);
        Assert.Equal(0, s.Samples);

        // a reset to no dimension leaves the components' statistics, so their samples still count, and the lifted
        // members return nothing until the next sample, where QuantLib's return their last call's values
        s.Add([1.0, 2.0]);
        s.Reset();
        Assert.Equal(0, s.Size);
        Assert.Equal(1, s.Samples);
        Assert.Empty(s.Mean());
    }

    [Fact]
    public void TheConstructorAndTheFirstSampleResetTheBaseOnly()
    {
        // QuantLib's constructor and add call the base's own reset (sequencestatistics.hpp:161, :127): a derived
        // Reset reading a field its constructor sets isn't run before that constructor, nor by the first sample
        var s = new CountingStatistics(2);
        Assert.Equal(0, s.Resets);
        s.Reset(2);
        Assert.Equal(1, s.Resets);

        var lazy = new CountingStatistics(0);
        lazy.Add([1.0, 2.0]);
        Assert.Equal(2, lazy.Size);
        Assert.Equal(0, lazy.Resets);
    }

    [Fact]
    public void TheCovarianceNeedsTwoSamplesOfWeight()
    {
        var s = new SequenceStatisticsInc(2);
        Assert.Equal("sampleWeight=0, unsufficient", Assert.Throws<NanoQLException>(s.Covariance).Message);
        s.Add([1.0, 2.0]);
        Assert.Equal("sample number <=1, unsufficient", Assert.Throws<NanoQLException>(s.Correlation).Message);
        s.Add([2.0, 2.0]);
        Matrix c = s.Correlation();

        // a component of no variance: 1 on its diagonal, 0 against another
        Assert.Equal(1.0, c[0, 0]);
        Assert.Equal(1.0, c[1, 1]);
        Assert.Equal(0.0, c[0, 1]);
        var flat = new SequenceStatisticsInc(2);
        flat.Add([1.0, 1.0]);
        flat.Add([1.0, 1.0]);
        Assert.Equal(1.0, flat.Correlation()[0, 1]);
    }

    [Fact]
    public void EachMemberIsLiftedOverTheComponents()
    {
        var s = new SequenceStatistics(2);
        var g0 = new GeneralStatistics();
        var g1 = new GeneralStatistics();
        double[] x = [0.5, -1.0, 2.0, -0.3, 1.2, -2.2, 0.9, 0.1];
        for (int i = 0; i < x.Length; ++i)
        {
            s.Add([x[i], -x[i] * 2.0], 1.0 + (0.1 * i));
            g0.Add(x[i], 1.0 + (0.1 * i));
            g1.Add(-x[i] * 2.0, 1.0 + (0.1 * i));
        }

        Assert.Equal([g0.Variance(), g1.Variance()], s.Variance());
        Assert.Equal([g0.StandardDeviation(), g1.StandardDeviation()], s.StandardDeviation());
        Assert.Equal([g0.ErrorEstimate(), g1.ErrorEstimate()], s.ErrorEstimate());
        Assert.Equal([g0.Skewness(), g1.Skewness()], s.Skewness());
        Assert.Equal([g0.Kurtosis(), g1.Kurtosis()], s.Kurtosis());
        Assert.Equal([g0.GaussianPercentile(0.95), g1.GaussianPercentile(0.95)], s.GaussianPercentile(0.95));
        Assert.Equal([g0.GaussianPotentialUpside(0.95), g1.GaussianPotentialUpside(0.95)], s.GaussianPotentialUpside(0.95));
        Assert.Equal([g0.GaussianValueAtRisk(0.95), g1.GaussianValueAtRisk(0.95)], s.GaussianValueAtRisk(0.95));
        Assert.Equal([g0.GaussianExpectedShortfall(0.95), g1.GaussianExpectedShortfall(0.95)], s.GaussianExpectedShortfall(0.95));
        Assert.Equal([g0.GaussianShortfall(0.1), g1.GaussianShortfall(0.1)], s.GaussianShortfall(0.1));
        Assert.Equal([g0.GaussianAverageShortfall(0.1), g1.GaussianAverageShortfall(0.1)], s.GaussianAverageShortfall(0.1));
        Assert.Equal([g0.Percentile(0.5), g1.Percentile(0.5)], s.Percentile(0.5));
        Assert.Equal([g0.PotentialUpside(0.9), g1.PotentialUpside(0.9)], s.PotentialUpside(0.9));
        Assert.Equal([g0.ValueAtRisk(0.9), g1.ValueAtRisk(0.9)], s.ValueAtRisk(0.9));
        Assert.Equal([g0.ExpectedShortfall(0.9), g1.ExpectedShortfall(0.9)], s.ExpectedShortfall(0.9));
        Assert.Equal([g0.Regret(0.2), g1.Regret(0.2)], s.Regret(0.2));
        Assert.Equal([g0.Shortfall(0.2), g1.Shortfall(0.2)], s.Shortfall(0.2));
        Assert.Equal([g0.AverageShortfall(0.2), g1.AverageShortfall(0.2)], s.AverageShortfall(0.2));
        Assert.Equal([g0.SemiVariance(), g1.SemiVariance()], s.SemiVariance());
        Assert.Equal([g0.SemiDeviation(), g1.SemiDeviation()], s.SemiDeviation());
        Assert.Equal([g0.DownsideVariance(), g1.DownsideVariance()], s.DownsideVariance());
        Assert.Equal([g0.DownsideDeviation(), g1.DownsideDeviation()], s.DownsideDeviation());

        var inc = new SequenceStatisticsInc(1);
        var i0 = new IncrementalStatistics();
        foreach (double v in x)
        {
            inc.Add([v]);
            i0.Add(v);
        }

        Assert.Equal([i0.DownsideVariance()], inc.DownsideVariance());
        Assert.Equal([i0.DownsideDeviation()], inc.DownsideDeviation());
    }

    [Fact]
    public void TheEmpiricalMembersNeedStatisticsThatKeepTheirSamples()
    {
        MethodInfo percentile = typeof(GenericSequenceStatisticsExtensions).GetMethods()
            .Single(m => m.Name == "Percentile" && m.IsGenericMethodDefinition);
        Type parameter = percentile.GetGenericArguments()[0];
        Assert.Contains(typeof(IEmpiricalStatistics), parameter.GetGenericParameterConstraints());

        // the incremental statistics' downside members are their own
        MethodInfo downside = typeof(GenericSequenceStatisticsExtensions).GetMethods()
            .Single(m => m.Name == "DownsideVariance" && !m.IsGenericMethodDefinition);
        Assert.Equal(typeof(GenericSequenceStatistics<IncrementalStatistics>), downside.GetParameters()[0].ParameterType);
    }

    [Fact]
    public void TheConvergenceTableRecordsTheMeanAtTheRulesSizes()
    {
        var c = new ConvergenceStatistics<IncrementalStatistics, DoublingConvergenceSteps>();
        c.AddSequence([1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0, 13.0, 14.0, 15.0]);
        Assert.Equal([(1, 1.0), (3, 2.0), (7, 4.0), (15, 8.0)], c.ConvergenceTable);
        Assert.Equal(15, c.Samples);
        Assert.Equal(c.Statistics.Mean(), c.Mean());
        Assert.Equal(c.Statistics.Variance(), c.Variance());

        // a sample added through the held statistics bypasses the table
        c.Statistics.Add(16.0);
        c.Add(17.0);
        Assert.Equal(4, c.ConvergenceTable.Length);
        Assert.Equal(17, c.Samples);

        // weighted, and on statistics given, which it resets
        var held = new GeneralStatistics();
        held.Add(100.0);
        var w = new ConvergenceStatistics<GeneralStatistics, DoublingConvergenceSteps>(held);
        Assert.Equal(0, held.Samples);
        w.AddSequence([1.0, 3.0, 5.0], [1.0, 1.0, 2.0]);
        Assert.Equal([(1, 1.0), (3, 3.5)], w.ConvergenceTable);
        Assert.Throws<ArgumentException>(() => w.AddSequence([1.0, 2.0], [1.0]));
        w.Reset();
        Assert.Empty(w.ConvergenceTable);
        Assert.Equal(0, w.Samples);
        Assert.Equal(1, default(DoublingConvergenceSteps).InitialSamples);
        Assert.Equal(7, default(DoublingConvergenceSteps).NextSamples(3));
    }

    [Fact]
    public void AStatisticsDerivedReadsQuantLibsProtectedMembers()
    {
        var d = new Derived();
        d.Add([1.0, 2.0]);
        d.Add([3.0, 5.0], 0.5);
        Assert.Equal(2, d.DimensionRead);
        Assert.Equal(2, d.ComponentsRead.Length);
        Assert.Equal(2, d.ComponentsRead[0].Samples);

        // Σ w x xᵀ
        Assert.Equal([1.0 + (0.5 * 9.0), 2.0 + (0.5 * 15.0), 2.0 + (0.5 * 15.0), 4.0 + (0.5 * 25.0)], d.QuadraticSumRead);
    }

    private sealed class Derived : SequenceStatistics
    {
        public int DimensionRead => Dimension;

        public GeneralStatistics[] ComponentsRead => ComponentStatistics.ToArray();

        public double[] QuadraticSumRead => QuadraticSum.ToArray();
    }

    // A derived statistics whose Reset reads a field its constructor sets, and counts its calls.
    private sealed class CountingStatistics : SequenceStatistics
    {
        private readonly int[] resets;

        public CountingStatistics(int dimension)
            : base(dimension)
        {
            resets = new int[1];
        }

        public int Resets => resets[0];

        public override void Reset(int dimension = 0)
        {
            base.Reset(dimension);
            resets[0]++;
        }
    }
}
