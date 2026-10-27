// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The single composite's buffer (the S-mmp plan's MMPR4 (viii), fixed under R8). QuantLib's
/// <c>SingleProductComposite::maxNumberOfCashFlowsPerProductPerStep</c> sums its parts' maxima per product, ignoring
/// their numbers of products (<c>singleproductcomposite.cpp:32</c>), so a part of several products paying at one step
/// writes past the buffer the accounting engine sizes by it: a rebate of three products, each paying at every step,
/// gives 1 for 3 cash flows a step (this run's <c>probes\c1_facts.cpp</c>), and the planner's coterminal swaps corrupt
/// the heap (MPP11). nanoql counts each part's maximum times its products. Checks: the multi composite's products,
/// summed.
/// </summary>
public class SingleProductCompositeBufferTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void APartOfSeveralProductsFitsTheBuffer()
    {
        double[] times = [1.0, 1.5, 2.0];
        var amounts = new Matrix(3, 3);
        for (int i = 0; i < 3; ++i)
        {
            for (int j = 0; j < 3; ++j)
            {
                amounts[i, j] = 0.01 * (i + 1);
            }
        }

        var rebate = new MarketModelCashRebate(new EvolutionDescription(Market.RateTimes, times), times, amounts, 3);
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        var single = new SingleProductComposite();
        single.Add(forwards);
        single.Add(rebate, 2.0);
        single.FinalizeComposite();
        var multi = new MultiProductComposite();
        multi.Add(forwards);
        multi.Add(rebate, 2.0);
        multi.FinalizeComposite();

        // each part's maximum times its products, 1 * 6 + 1 * 3, where QuantLib's sums the maxima, 1 + 1, short of the
        // rebate's three a step
        Assert.Equal(9, single.MaxNumberOfCashFlowsPerProductPerStep);

        (ImmutableArray<double> s, _) = Market.TerminalPc(single, 3, 2047);
        (ImmutableArray<double> m, _) = Market.TerminalPc(multi, 3, 2047);
        Assert.Equal(m.Sum(), s[0], 1e-15);
    }
}
