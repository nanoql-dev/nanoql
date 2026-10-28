// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The discrepancy statistics' lazy dimension and pairing (the S-core plan's CR7 (viii) and (ix)): QuantLib computes
/// its constants for the dimension at construction, so a statistics of dimension 0, whose first sample sets it, keeps
/// dimension 0's constants; and it pairs the components' stored samples by position, which a percentile read, sorting
/// each component in place, breaks. nanoql fixes the constants with the dimension and keeps its own copy of the points.
/// On 200 three-dimensional Sobol points (seed 1) QuantLib gives 0.005923430933947052 with the dimension given, 0.666
/// with it lazy and 0.102 after a percentile read halfway (the plan's <c>misc1.cpp</c>, rerun in this run's
/// <c>probes\misc1.cpp</c>); nanoql gives the first in all three.
/// </summary>
public class DiscrepancyStatisticsTests
{
    private const double GivenDimension = 0.005923430933947052;

    [Fact]
    public void ALazyDimensionTakesItsConstantsFromTheFirstSample()
    {
        Assert.Equal(GivenDimension, Run(new DiscrepancyStatistics(3), percentileAt: -1)); // QuantLib's, its dimension given
        Assert.Equal(GivenDimension, Run(new DiscrepancyStatistics(0), percentileAt: -1)); // QuantLib 0.66633972877293524
    }

    [Fact]
    public void APercentileReadLeavesThePointsPaired()
    {
        Assert.Equal(GivenDimension, Run(new DiscrepancyStatistics(3), percentileAt: 100)); // QuantLib 0.10156000398318485
    }

    private static double Run(DiscrepancyStatistics statistics, int percentileAt)
    {
        var sobol = new SobolRsg(3, 1);
        for (int i = 0; i < 200; ++i)
        {
            statistics.Add(sobol.NextSequence().Value.Span);
            if (i == percentileAt)
            {
                _ = statistics.Percentile(0.5);
            }
        }

        return statistics.Discrepancy();
    }
}
