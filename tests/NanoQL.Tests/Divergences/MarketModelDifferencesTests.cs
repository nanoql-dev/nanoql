// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The market-model differences (the S-mmp plan's MMPR4 (xiii) and (xiv), fixed under R8). QuantLib's
/// <c>rateVolDifferences</c> divides rate i's variance difference by the i-th evolution time
/// (<c>marketmodeldifferences.cpp:50</c>), not the rate's reset time: FlatVol at 25% against 20% on quarterly steps
/// over semi-annual rates gives 0.2121 for every rate where the volatilities differ by √(25%² − 20%²) = 15%, √2 too
/// high, and reads past the evolution times where the rates outnumber the steps. Its <c>rateInstVolDifferences</c>
/// checks the rate's index against the number of steps (<c>:68</c>): on three steps rate 4 of 6 is refused, "the index
/// given is greater than the number of steps", and on fine steps an index past the rates reads past the covariance
/// (this run's
/// <c>probes\c1_facts.cpp</c>; the planner's MPP10 (g)). nanoql divides by the rate's reset time, or by the evolution's
/// last time where that comes first, since the total covariance accrues nothing past it: on an evolution ending at 1.5
/// over resets to 3.0, the reset time alone gives 0.1299, 0.1162 and 0.1061 on the last three rates. It checks the
/// index against the number of rates. Checks: FlatVol's √(σ₁² − σ₂²); QuantLib's bits where the evolution times are
/// the rate times.
/// </summary>
public class MarketModelDifferencesTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void ARatesVolatilityIsOverTheTimeItsVarianceAccrues()
    {
        double[] quarterly = [.. Enumerable.Range(1, 12).Select(i => 0.25 * i)];
        var evolution = new EvolutionDescription(Market.RateTimes, quarterly);
        ImmutableArray<double> d = MarketModelDifferencesFunctions.RateVolDifferences(
            Market.FlatVol(evolution, 6, 0.25), Market.FlatVol(evolution, 6, 0.20));
        Assert.All(d, x => Assert.Equal(0.15, x, 1e-15));

        // QuantLib's, the variances over the evolution times
        const double quantLib = 0.21213203435596423;
        Assert.Equal(quantLib, 0.15 * Math.Sqrt(2.0), 1e-15);

        // an evolution ending at 1.5, before the last three rates reset: their variances accrue to 1.5 only, and over
        // their reset times they'd read 0.1299, 0.1162 and 0.1061; QuantLib reads past its three evolution times
        var early = new EvolutionDescription(Market.RateTimes, [0.5, 1.0, 1.5]);
        d = MarketModelDifferencesFunctions.RateVolDifferences(Market.FlatVol(early, 6, 0.25), Market.FlatVol(early, 6, 0.20));
        Assert.All(d, x => Assert.Equal(0.15, x, 1e-15));
    }

    [Fact]
    public void OnTheRateTimesTheDifferencesAreQuantLibs()
    {
        var evolution = new EvolutionDescription(Market.RateTimes);
        MarketModel m1 = Market.FlatVol(evolution, 6, 0.25), m2 = Market.FlatVol(evolution, 6, 0.20);
        double[] quantLib =
        [
            0.14999999999999994, 0.14999999999999997, 0.14999999999999997, 0.14999999999999999, 0.14999999999999997,
            0.14999999999999997,
        ];
        Assert.Equal(quantLib, MarketModelDifferencesFunctions.RateVolDifferences(m1, m2));
        double[] instantaneous = [0.14999999999999999, 0.14999999999999997, 0.14999999999999994, 0, 0, 0];
        Assert.Equal(instantaneous, MarketModelDifferencesFunctions.RateInstVolDifferences(m1, m2, 2));
    }

    [Fact]
    public void ARatesIndexIsCheckedAgainstTheRates()
    {
        var evolution = new EvolutionDescription(Market.RateTimes, [1.0, 2.0, 3.0]);
        MarketModel m1 = Market.FlatVol(evolution, 6, 0.25), m2 = Market.FlatVol(evolution, 6, 0.20);

        // QuantLib refuses rate 4 on these three steps: "the index given is greater than the number of steps"; it
        // resets at 2.5, half way through the last step
        ImmutableArray<double> d = MarketModelDifferencesFunctions.RateInstVolDifferences(m1, m2, 4);
        Assert.Equal(0.15, d[0], 1e-15);
        Assert.Equal(0.15, d[1], 1e-15);
        Assert.Equal(0.15 * Math.Sqrt(0.5), d[2], 1e-15);

        var e = Assert.Throws<NanoQLException>(() => MarketModelDifferencesFunctions.RateInstVolDifferences(m1, m2, 6));
        Assert.Equal("the index given is greater than the number of rates", e.Message);
    }
}
