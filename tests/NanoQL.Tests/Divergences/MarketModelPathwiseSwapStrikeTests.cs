// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The pathwise swap's one strike (fixed). QuantLib's <c>MarketModelPathwiseSwap</c> expands one strike to every rate,
/// then checks the strikes it was given, not the expanded ones (<c>pathwiseproductswap.cpp:59</c>), so it refuses one
/// strike with the message that offers it, "strikes.size() does not equal numberOfRates or 1". nanoql checks the
/// expanded strikes. Check: the same strike given for every rate, bit for bit.
/// </summary>
public class MarketModelPathwiseSwapStrikeTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void OneStrikeIsEveryRatesStrike()
    {
        var one = new MarketModelPathwiseSwap(Market.RateTimes, Market.Accruals, [0.035], -1.0);
        var every = new MarketModelPathwiseSwap(Market.RateTimes, Market.Accruals, [.. Enumerable.Repeat(0.035, Market.N)], -1.0);
        var state = new LMMCurveState(Market.RateTimes);
        state.SetOnForwardRates(Market.Forwards);
        int[] oneCounts = new int[1], everyCounts = new int[1];
        MarketModelPathwiseMultiProductCashFlow[][] oneFlows = [Flows(one)], everyFlows = [Flows(every)];
        one.Reset();
        every.Reset();
        bool done = false;
        while (!done)
        {
            done = one.NextTimeStep(state, oneCounts, oneFlows);
            Assert.Equal(done, every.NextTimeStep(state, everyCounts, everyFlows));
            Assert.Equal(everyCounts, oneCounts);
            for (int k = 0; k < oneCounts[0]; ++k)
            {
                Assert.Equal(everyFlows[0][k].TimeIndex, oneFlows[0][k].TimeIndex);
                Assert.Equal(everyFlows[0][k].Amount.ToArray(), oneFlows[0][k].Amount.ToArray());
            }
        }
    }

    private static MarketModelPathwiseMultiProductCashFlow[] Flows(MarketModelPathwiseSwap swap) =>
        [.. Enumerable.Range(0, swap.MaxNumberOfCashFlowsPerProductPerStep).Select(_ => new MarketModelPathwiseMultiProductCashFlow(Market.N + 1))];
}
