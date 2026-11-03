// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The basis systems' function counts (fixed). QuantLib adjusts only the last exercise's count
/// (<c>swapbasissystem.cpp:46</c>, <c>swapforwardbasissystem.cpp:53</c>, <c>:56</c>), so an earlier exercise in the
/// last periods reports more functions than its own values give: on six semi-annual rates, a swap basis system
/// exercised at 2.6 and 2.8 counts 3 and 2 where its values give 2 and 2, and a swap-forward basis system at every rate
/// time but the last counts 10 at its fifth exercise where its values give 6. nanoql counts each exercise's functions
/// from its own rate, as its values decide, which the node-data collection and the Longstaff-Schwartz strategy size
/// their buffers by. Check: QuantLib's values' lengths.
/// </summary>
public class BasisSystemFunctionCountTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void EachExercisesCountIsItsValues()
    {
        var swap = new SwapBasisSystem(Market.RateTimes, [2.6, 2.8]);
        Assert.Equal([2, 2], swap.NumberOfFunctions());
        Assert.Equal([2, 2], ValuesLengths(swap, 2));

        var swapForward = new SwapForwardBasisSystem(Market.RateTimes, Market.RateTimes[..^1]);
        Assert.Equal([10, 10, 10, 10, 6, 3], swapForward.NumberOfFunctions());

        // QuantLib's counts and its values' lengths
        int[] quantLibSwap = [3, 2], quantLibSwapForward = [10, 10, 10, 10, 10, 3];
        int[] quantLibValues = [10, 10, 10, 10, 6, 3];
        Assert.NotEqual(quantLibSwap, swap.NumberOfFunctions());
        Assert.NotEqual(quantLibSwapForward, swapForward.NumberOfFunctions());
        Assert.Equal(quantLibValues, swapForward.NumberOfFunctions());
    }

    // The number of values each exercise writes, its span one longer than its count and the extra entry unwritten.
    private static int[] ValuesLengths(MarketModelBasisSystem basis, int exercises)
    {
        var state = new LMMCurveState(Market.RateTimes);
        state.SetOnForwardRates(Market.Forwards);
        ImmutableArray<int> counts = basis.NumberOfFunctions();
        int[] lengths = new int[exercises];
        basis.Reset();
        for (int k = 0; k < exercises; ++k)
        {
            basis.NextStep(state);
            double[] v = [.. Enumerable.Repeat(double.NaN, counts[k] + 1)];
            basis.Values(state, v);
            lengths[k] = v.Count(x => !double.IsNaN(x));
        }

        return lengths;
    }
}
