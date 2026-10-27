// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The call-specified product's counts at an idle step (the S-mmp plan's MMPR4 (xvi), Revision 2's O1-2, fixed under
/// R8). At a step where neither its underlying nor its rebate steps, an exercise, strategy or rebate time off the
/// underlying's grid, QuantLib's product writes no counts (<c>callspecifiedmultiproduct.cpp:144-167</c>,
/// <c>pathwiseproductcallspecified.cpp:136-153</c>), and the accounting engine adds the previous step's flows again: a
/// 2% receiver swap on six semi-annual rates callable 0.1 before each rate time and never called, 8,191 paths, QuantLib
/// −0.061168 ± 0.00039 against the swap's exact −0.033604 (this run's <c>probes\c3_facts.cpp</c>; the planner's MPP21).
/// nanoql zeroes every count at each step's start, as QuantLib's composites zero the parts they don't step. Checks:
/// QuantLib's bits on schedules on the grid (QuantLib's cases, the vectors); off it, the swap's exact value.
/// </summary>
public class CallSpecifiedIdleStepTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void AnUncalledProductOffTheGridIsItsUnderlying()
    {
        var receiver = new MultiStepSwap(Market.RateTimes, Market.Accruals, Market.Accruals, Market.PaymentTimes, 0.02, false);
        double[] offTimes = [.. Market.RateTimes[1..^1].Select(t => t - 0.1)];
        var never = new SwapRateTrigger(Market.RateTimes, [.. Enumerable.Repeat(1.0, offTimes.Length)], offTimes);
        var callable = new CallSpecifiedMultiProduct(receiver, never);

        (ImmutableArray<double> values, ImmutableArray<double> errors) = Market.TerminalPc(callable, 3, 8191);

        double exact = 0.0;
        for (int i = 0; i < Market.N; ++i)
        {
            exact += (0.02 - Market.Forwards[i]) * Market.Accruals[i] * Market.Discounts[i + 1];
        }

        Assert.True(Math.Abs(values[0] - exact) < 4 * errors[0], $"{values[0]} ± {errors[0]} against {exact}");

        // QuantLib's, the idle steps' flows paid again
        const double quantLib = -0.061168265957096658;
        Assert.True(Math.Abs(quantLib - exact) > 50 * errors[0]);
    }
}
