// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Inputs for which QuantLib's statistics are undefined are refused (playbook §2's undefined-path row): a NaN sample
/// where the percentiles sort the samples with <c>std::sort</c>, whose ordering a NaN breaks
/// (generalstatistics.hpp:247-252), with a <see cref="NanoQLException"/>; and fewer weights than samples given to
/// <c>AddSequence</c>, whose loop reads past the weights' end (generalstatistics.hpp:174-179,
/// incrementalstatistics.hpp:142-147), with an <see cref="ArgumentException"/> naming them. Where nothing sorts, a NaN
/// sample is read as QuantLib reads it.
/// </summary>
public class StatisticsUndefinedInputsTests
{
    [Fact]
    public void ANaNSampleCantBeSorted()
    {
        var s = new GeneralStatistics();
        foreach (double x in new[] { 1.0, double.NaN, 2.0, 3.0 })
        {
            s.Add(x);
        }

        foreach (Action sorting in new Action[]
                 {
                     () => s.Sort(), () => s.Percentile(0.5), () => s.TopPercentile(0.5), () => s.ValueAtRisk(0.95),
                     () => s.PotentialUpside(0.95), () => s.ExpectedShortfall(0.95),
                 })
        {
            Assert.Equal("NaN sample not allowed in a sort", Assert.Throws<NanoQLException>(sorting).Message);
        }

        // Nothing sorts here: QuantLib's mean is NaN, and its minimum and maximum read the pairs' operator<.
        Assert.True(double.IsNaN(s.Mean()));
        Assert.Equal(1.0, s.Min());
        Assert.Equal(3.0, s.Max());
    }

    [Fact]
    public void FewerWeightsThanSamplesAreRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new GeneralStatistics().AddSequence([1.0, 2.0, 3.0], [1.0, 1.0]));
        Assert.Equal("weights", e.ParamName);
        Assert.StartsWith("2 weights given for 3 samples", e.Message);
        e = Assert.Throws<ArgumentException>(() => new IncrementalStatistics().AddSequence([1.0, 2.0], [1.0]));
        Assert.Equal("weights", e.ParamName);

        // More weights than samples read only the samples' own, as QuantLib's loop does.
        var s = new GeneralStatistics();
        s.AddSequence([1.0, 2.0], [1.0, 3.0, 5.0]);
        Assert.Equal(4.0, s.WeightSum());
    }
}
