// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The vega bump collection's checks (the S-mmp plan's MMPR4 (xii), ruled by the user). QuantLib's <c>isFull</c> and
/// <c>isNonOverlapping</c> are true where an element fails (<c>vegabumpcluster.cpp:179</c>, <c>:218</c>): on six
/// semi-annual rates, FlatVol 20%, 3 factors, its own default collection, given as its clusters, reports not full,
/// overlapping and not sensible (this run's <c>probes\c4_facts.cpp</c>); half its clusters report full and one cluster
/// given twice non-overlapping (the planner's MPP10 (f)). nanoql's are true where no element fails. Checks: QuantLib's
/// default collection's flags, true.
/// </summary>
public class VegaBumpCollectionCheckTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void TheChecksAreTrueWhereNoElementFails()
    {
        MarketModel model = Market.FlatVol(new EvolutionDescription(Market.RateTimes, Market.RateTimes[..^1]), 3);
        var all = new VegaBumpCollection(model);
        Assert.True(all.IsFull() && all.IsNonOverlapping() && all.IsSensible());

        // QuantLib's default collection given as its clusters: full 0, non-overlapping 0, sensible 0
        var given = new VegaBumpCollection(all.AllBumps, model);
        Assert.True(given.IsFull());
        Assert.True(given.IsNonOverlapping());
        Assert.True(given.IsSensible());

        var half = new VegaBumpCollection(all.AllBumps.Take(all.NumberBumps / 2), model);
        Assert.False(half.IsFull());
        Assert.True(half.IsNonOverlapping());
        Assert.False(half.IsSensible());

        var twice = new VegaBumpCollection([.. all.AllBumps, all.AllBumps[0]], model);
        Assert.True(twice.IsFull());
        Assert.False(twice.IsNonOverlapping());
        Assert.False(twice.IsSensible());
    }
}
