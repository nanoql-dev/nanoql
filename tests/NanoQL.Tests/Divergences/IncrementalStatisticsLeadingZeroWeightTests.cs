// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="IncrementalStatistics"/>' variance after samples of no weight: QuantLib's <c>variance()</c>
/// (<c>incrementalstatistics.cpp:47-54</c>) reads Boost's iterative weighted variance, σ² ← σ²(S − w)/S + (x − μ)²w/(S
/// − w) from the second sample on (<c>weighted_variance.hpp:89-97</c>), whose second term is 0/0 while every earlier
/// sample weighs 0, and x/0 while their weights vanish in the sum, below its rounding, so the variance is NaN or
/// infinite for good. The port resets the variance to 0 there, the first term's value and the second's limit (a skip
/// there would keep the earlier samples' variance as the set's). QuantLib's check: its <c>Statistics</c> on the same
/// samples, and its <c>IncrementalStatistics</c> with the samples of no weight moved after the first one that carries
/// weight, which Boost's recurrence reads without a 0/0.
/// </summary>
public class IncrementalStatisticsLeadingZeroWeightTests
{
    [Fact]
    public void LeadingSamplesOfNoWeightLeaveTheVarianceOfTheOthers()
    {
        // The wheel: QuantLib's IncrementalStatistics gives variance and error estimate NaN on each set; its
        // Statistics 2.734375 and 0.739509972887452, then 2.625 and 0.6614378277661477; its IncrementalStatistics
        // with the zero weight third 2.734375 and 0.739509972887452.
        foreach (((double X, double W)[] samples, double variance, double errorEstimate) in new[]
                 {
                     (new[] { (5.0, 0.0), (3.0, 1.0), (4.0, 1.0), (6.0, 1.0), (2.0, 1.0) }, 2.734375, 0.739509972887452),
                     (new[] { (5.0, 0.0), (7.0, 0.0), (3.0, 1.0), (4.0, 1.0), (6.0, 1.0), (2.0, 1.0) }, 2.625, 0.6614378277661477),
                 })
        {
            var s = new IncrementalStatistics();
            foreach ((double x, double w) in samples)
            {
                s.Add(x, w);
            }

            Assert.True(Tolerance.Within(variance, s.Variance()), $"{s.Variance()}");
            Assert.True(Tolerance.Within(errorEstimate, s.ErrorEstimate()), $"{s.ErrorEstimate()}");
        }

        var moved = new IncrementalStatistics();
        foreach ((double x, double w) in new[] { (3.0, 1.0), (4.0, 1.0), (5.0, 0.0), (6.0, 1.0), (2.0, 1.0) })
        {
            moved.Add(x, w);
        }

        Assert.Equal(2.734375, moved.Variance());
    }

    [Fact]
    public void LeadingWeightsTheSumAbsorbsLeaveTheVarianceOfTheOthers()
    {
        // The wheel: QuantLib's IncrementalStatistics gives NaN; its Statistics 0.3333333333333333, and its
        // IncrementalStatistics with (3, 1) moved first 0.3333333333333333. A skip of the update would keep the first
        // two samples' variance, 1.
        var s = new IncrementalStatistics();
        foreach ((double x, double w) in new[] { (5.0, 1e-300), (7.0, 1e-300), (3.0, 1.0), (4.0, 1.0) })
        {
            s.Add(x, w);
        }

        Assert.True(Tolerance.Within(0.3333333333333333, s.Variance()), $"{s.Variance()}");
    }
}
