// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The composites' ends (the S-mmp plan's MMPR4 (vii), option (1), ruled by the user). QuantLib's composites never
/// record a part's end: <c>compositeproduct.cpp:43</c> resets the flag <c>multiproductcomposite.cpp:49</c> and
/// <c>singleproductcomposite.cpp:46</c> test, which nothing sets, so a finished part steps again at each of its later
/// times; and a composite's path ends when the parts it stepped at a step are finished, whatever the others' later
/// times. Forwards beside a 1% rebate at 1.0, 1.5 and 2.0, which finishes at each step: QuantLib's composite pays the
/// rebate three times, 0.0276525, where alone it pays once; forwards struck 50 bp low beside a 1% rebate at 0.75:
/// QuantLib's composite ends the path at 0.75, its later forwards 0 (this run's <c>probes\c1_facts.cpp</c>; the
/// planner's MPP21). nanoql's composites record each part's end, step only unfinished parts, end a path when every part
/// is finished or at their last step, and the single composite's count is 0 at a step where no part steps
/// (<c>singleproductcomposite.cpp:63</c> writes it only for a stepped part). Checks: QuantLib's own composite values,
/// bit for bit, on the parts it steps the same; each forward's exact value; and the single composite's cash flows,
/// those of the multi composite's products, bit for bit. A swaption (2, 5) beside a 1% rebate at 0.75 and a 2% one at
/// 2.25, in one product: QuantLib's single composite ends the path at 0.75, 0.0094334, the first rebate's; recording
/// each part's end but writing the count only for a stepped part, it pays the swaption's step again at 2.0, where no
/// part steps, 0.0354032 for the multi composite's 0.0314175 (the planner's MPP23 rerun at 1,023 paths,
/// <c>probes\r2\c2_spc1023.cpp</c>).
/// </summary>
public class MarketModelCompositeEndTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void AFinishedPartIsntSteppedAgain()
    {
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        double[] rebateTimes = [1.0, 1.5, 2.0];
        var rebate = new MarketModelCashRebate(
            new EvolutionDescription(Market.RateTimes, rebateTimes), rebateTimes, new Matrix(1, 3, 0.01), 1);
        var composite = new MultiProductComposite();
        composite.Add(forwards);
        composite.Add(rebate);
        composite.FinalizeComposite();

        (ImmutableArray<double> values, ImmutableArray<double> errors) = Market.TerminalPc(composite, 3, 8191);

        // the forwards, which QuantLib's composite steps as nanoql's: QuantLib's values, bit for bit
        double[] quantLibForwards =
        [
            1.9769124608881034e-05, 1.0091983265049231e-05, 2.4238917255176911e-05, 5.7998062536754938e-05,
            2.1243319911860881e-05, 6.7467671320471079e-05,
        ];
        Assert.Equal(quantLibForwards, values[..6]);

        // the rebate, paid once, at 1.0: its exact value within its Monte Carlo error
        double exact = 0.01 * Market.Discounts[1];
        Assert.True(Math.Abs(values[6] - exact) < 4 * errors[6], $"{values[6]} against {exact} ± {errors[6]}");

        // QuantLib's composite pays it at each of its times
        const double quantLibRebate = 0.027652471095442492;
        Assert.True(Math.Abs(quantLibRebate - (0.01 * (Market.Discounts[1] + Market.Discounts[2] + Market.Discounts[3]))) < 1e-4);
        Assert.True(quantLibRebate > 2.9 * values[6]);
    }

    [Fact]
    public void APathEndsOnlyWhenEveryPartHasEnded()
    {
        double[] strikes = [.. Market.Forwards.Select(f => f - 0.005)];
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, strikes);
        double[] rebateTimes = [0.75];
        var rebate = new MarketModelCashRebate(
            new EvolutionDescription(Market.RateTimes, rebateTimes), rebateTimes, new Matrix(1, 1, 0.01), 1);
        var composite = new MultiProductComposite();
        composite.Add(forwards);
        composite.Add(rebate);
        composite.FinalizeComposite();

        (ImmutableArray<double> values, ImmutableArray<double> errors) = Market.TerminalPc(composite, 3, 8191);

        // QuantLib's composite: the first forward as nanoql's, bit for bit, the path ended at 0.75, its later forwards
        // 0; its rebate 0.009433454016303161, through its discounter's bracket (MarketModelDiscounterBracketTests)
        Assert.Equal(0.0023419808519774621, values[0]);
        double[] quantLibLater = [0, 0, 0, 0, 0];

        for (int i = 0; i < 6; ++i)
        {
            double exact = 0.005 * Market.Accruals[i] * Market.Discounts[i + 1];
            Assert.True(Math.Abs(values[i] - exact) < 4 * errors[i], $"forward {i}: {values[i]} against {exact} ± {errors[i]}");
        }

        Assert.All(values[1..6], v => Assert.True(v > 0.002));
        Assert.NotEqual(quantLibLater, values[1..6]);
    }

    [Fact]
    public void AStepWhereNoPartStepsPaysNothing()
    {
        // forwards on the first rate only, finished at 0.5, beside rebates at 0.75 and 2.25: at 1.0 to 2.0 the forwards
        // are present and finished, so no part steps
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, [Market.Forwards[0]]);
        var early = new MarketModelCashRebate(
            new EvolutionDescription(Market.RateTimes, [0.75]), [0.75], new Matrix(1, 1, 0.01), 1);
        var late = new MarketModelCashRebate(
            new EvolutionDescription(Market.RateTimes, [2.25]), [2.25], new Matrix(1, 1, 0.02), 1);
        var single = new SingleProductComposite();
        var multi = new MultiProductComposite();
        foreach (MarketModelComposite c in new MarketModelComposite[] { single, multi })
        {
            c.Add(forwards);
            c.Add(early);
            c.Add(late, 0.5);
            c.FinalizeComposite();
        }

        // stepped on states of their own: the single composite's cash flows are the multi composite's, each step
        var state = new LMMCurveState(Market.RateTimes);
        state.SetOnForwardRates(Market.Forwards);
        int[] singleCounts = new int[1], multiCounts = new int[3];
        MarketModelMultiProductCashFlow[][] singleFlows = [new MarketModelMultiProductCashFlow[single.MaxNumberOfCashFlowsPerProductPerStep]];
        MarketModelMultiProductCashFlow[][] multiFlows =
            [.. Enumerable.Range(0, 3).Select(_ => new MarketModelMultiProductCashFlow[multi.MaxNumberOfCashFlowsPerProductPerStep])];
        single.Reset();
        multi.Reset();
        bool singleDone = false, multiDone = false;
        int steps = 0;
        while (!multiDone)
        {
            singleDone = single.NextTimeStep(state, singleCounts, singleFlows);
            multiDone = multi.NextTimeStep(state, multiCounts, multiFlows);
            var expected = new List<MarketModelMultiProductCashFlow>();
            for (int p = 0; p < 3; ++p)
            {
                expected.AddRange(multiFlows[p][..multiCounts[p]]);
            }

            Assert.Equal(expected.Count, singleCounts[0]);
            Assert.Equal(expected, singleFlows[0][..singleCounts[0]]);
            ++steps;
        }

        Assert.True(singleDone);

        // the merged times 0.5, 0.75, 1.0, 1.5, 2.0, 2.25, 2.5, 3.0: the path ends with the last rebate, at 2.25
        Assert.Equal(6, steps);

        // through the accounting engine: the single composite's value is the multi composite's products' sum
        (ImmutableArray<double> s, _) = Market.TerminalPc(single, 3, 2047);
        (ImmutableArray<double> m, _) = Market.TerminalPc(multi, 3, 2047);
        Assert.Equal(m[0] + m[1] + m[2], s[0], 1e-15);
    }

    [Fact]
    public void ASwaptionsIdleStepPaysNothingInOneProduct()
    {
        // a swaption (2, 5) struck at 3.3%, finished at its exercise at 1.5, beside a 1% rebate at 0.75 and a 2% one at
        // 2.25 (the plan's case, this run's R1-3): at 2.0 the swaption is present and finished, and no part steps
        var swaption = new MultiStepSwaption(Market.RateTimes, 2, 5, new PlainVanillaPayoff(OptionType.Call, 0.033));
        var early = new MarketModelCashRebate(
            new EvolutionDescription(Market.RateTimes, [0.75]), [0.75], new Matrix(1, 1, 0.01), 1);
        var late = new MarketModelCashRebate(
            new EvolutionDescription(Market.RateTimes, [2.25]), [2.25], new Matrix(1, 1, 0.02), 1);
        var single = new SingleProductComposite();
        var multi = new MultiProductComposite();
        foreach (MarketModelComposite c in new MarketModelComposite[] { single, multi })
        {
            c.Add(swaption);
            c.Add(early);
            c.Add(late);
            c.FinalizeComposite();
        }

        (ImmutableArray<double> s, _) = Market.TerminalPc(single, 3, 1023);
        (ImmutableArray<double> m, _) = Market.TerminalPc(multi, 3, 1023);

        // the swaption, which pays on a rate time: the value QuantLib's composite gives it under the ruled end, bit for
        // bit; the rebates, paid inside their periods, through the discounter's fix (MarketModelDiscounterBracketTests)
        Assert.Equal(0.0039844127552868336, m[0]);

        // the single composite pays the multi composite's products' sum
        Assert.Equal(m[0] + m[1] + m[2], s[0], 1e-15);

        // QuantLib's single composite, ended at 0.75; and with each end recorded but the count left at an idle step
        const double quantLib = 0.0094333652416315264, quantLibCountLeft = 0.035403213273878471;
        Assert.True(s[0] > quantLib + m[0]);
        Assert.True(Math.Abs(quantLibCountLeft - s[0] - m[0]) < 1e-4, $"{quantLibCountLeft} against {s[0]} + {m[0]}");
    }
}
