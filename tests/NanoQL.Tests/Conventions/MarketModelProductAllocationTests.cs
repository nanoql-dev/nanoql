// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market-model products' pin (the S-mmp plan's MMPR1 (v), SMD10): after a warm-up path, a path of the accounting
/// engine (the evolver's steps, the product's steps, the discounting into numeraire bonds, the roll into the next
/// numeraire and the accumulation into the statistics) allocates nothing, on the framework's products in a
/// multi-product composite and in a single-product composite, in the terminal and the money-market measures; and on
/// every product family: the multi-step swaps, options and structured products, the one-step products, and the
/// pathwise products through the wrapper.
/// </summary>
public class MarketModelProductAllocationTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Theory]
    [InlineData("multi", "terminal")]
    [InlineData("multi", "moneyMarket")]
    [InlineData("single", "terminal")]
    [InlineData("multiStep", "terminal")]
    [InlineData("oneStep", "terminal")]
    [InlineData("pathwise", "moneyMarket")]
    public void AnAccountingEnginePathAllocatesNothing(string composite, string measure) =>
        AllocationAssert.AllocatesNothing(
            () =>
            {
                (AccountingEngine engine, SequenceStatisticsInc stats) = Make(composite, measure);
                engine.MultiplePathValues(stats, 1);
                return (engine, stats);
            },
            s => s.engine.MultiplePathValues(s.stats, 1));

    private static (AccountingEngine Engine, SequenceStatisticsInc Stats) Make(string kind, string measure)
    {
        MarketModelComposite composite = kind == "single" ? new SingleProductComposite() : new MultiProductComposite();
        switch (kind)
        {
            case "multiStep":
                AddMultiStep(composite);
                break;
            case "oneStep":
                AddOneStep(composite);
                break;
            case "pathwise":
                AddPathwise(composite);
                break;
            default:
                AddFramework(composite);
                break;
        }

        composite.FinalizeComposite();
        EvolutionDescription evolution = composite.Evolution;
        int[] numeraires = measure == "terminal"
            ? [.. EvolutionDescriptionFunctions.TerminalMeasure(evolution)]
            : [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(evolution)];
        int factors = kind == "oneStep" ? 6 : 3;
        var evolver = new LogNormalFwdRatePc(Market.FlatVol(evolution, factors), new MTBrownianGeneratorFactory(42), numeraires);
        return (new AccountingEngine(evolver, composite, Market.Discounts[numeraires[0]]), new SequenceStatisticsInc());
    }

    private static void AddFramework(MarketModelComposite composite)
    {
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        var optionlets = new MultiStepOptionlets(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Calls());
        double[] rebateTimes = [0.75, 1.25];
        var rebate = new MarketModelCashRebate(
            new EvolutionDescription(Market.RateTimes, rebateTimes), rebateTimes, new Matrix(2, 2, 0.01), 2);
        composite.Add(forwards);
        composite.Add(optionlets, 2.0);
        composite.Add(new MultiStepNothing(forwards.Evolution, 1, 3));
        composite.Add(rebate);
    }

    private static void AddMultiStep(MarketModelComposite composite)
    {
        double[] rt = Market.RateTimes, a = Market.Accruals, pt = Market.PaymentTimes;
        double[] six = [.. Enumerable.Repeat(0.08, 6)];
        StrikedTypePayoff call = new PlainVanillaPayoff(OptionType.Call, 0.033);
        StrikedTypePayoff[] calls = [.. Enumerable.Repeat(call, 6)];
        composite.Add(new MultiStepSwap(rt, a, a, pt, 0.032));
        composite.Add(new MultiStepSwaption(rt, 1, 5, call));
        composite.Add(new MultiStepCoterminalSwaps(rt, a, a, pt, 0.032));
        composite.Add(new MultiStepCoterminalSwaptions(rt, rt[..^1], calls));
        composite.Add(new MultiStepCoinitialSwaps(rt, a, a, pt, 0.032));
        composite.Add(new MultiStepInverseFloater(rt, a, a, [.. Enumerable.Repeat(0.15, 6)], [.. Enumerable.Repeat(2.0, 6)], new double[6], pt));
        composite.Add(new MultiStepRatchet(rt, a, pt, 1.0, 1.0, 0.0, 0.0, 0.03));
        composite.Add(new MultiStepTarn(rt, a, a, pt, pt, 0.04, six, [.. Enumerable.Repeat(2.0, 6)], new double[6]));
        composite.Add(new MultiStepPeriodCapletSwaptions(rt, [1.5, 2.5, 3.5], [0.5, 1.5, 2.5], calls[..3], calls[..3], 2, 0));
    }

    private static void AddOneStep(MarketModelComposite composite)
    {
        double[] rt = Market.RateTimes, a = Market.Accruals, pt = Market.PaymentTimes;
        composite.Add(new OneStepForwards(rt, a, pt, Market.Forwards));
        composite.Add(new OneStepOptionlets(rt, a, pt, Calls()));
        composite.Add(new OneStepCoinitialSwaps(rt, a, a, pt, 0.032));
        composite.Add(new OneStepCoterminalSwaps(rt, a, a, pt, 0.032));
    }

    private static void AddPathwise(MarketModelComposite composite)
    {
        double[] rt = Market.RateTimes, a = Market.Accruals, pt = Market.PaymentTimes, k = Market.Forwards;
        double[] fifteen = [.. Enumerable.Repeat(0.15, 6)];
        MarketModelPathwiseMultiProduct[] products =
        [
            new MarketModelPathwiseMultiCaplet(rt, a, pt, k),
            new MarketModelPathwiseMultiDeflatedCaplet(rt, a, pt, k),
            new MarketModelPathwiseMultiDeflatedCap(rt, a, pt, 0.032, [(0, 6), (1, 4)]),
            new MarketModelPathwiseInverseFloater(rt, a, a, fifteen, [.. Enumerable.Repeat(2.0, 6)], new double[6], pt),
            new MarketModelPathwiseSwap(rt, a, [0.032]),
            new MarketModelPathwiseCashRebate(new EvolutionDescription(rt, rt[..^1]), pt, new Matrix(1, 6, 0.01), 1),
            new MarketModelPathwiseCoterminalSwaptionsDeflated(rt, k),
            new MarketModelPathwiseCoterminalSwaptionsNumericalDeflated(rt, k, 1e-6),
        ];
        foreach (MarketModelPathwiseMultiProduct p in products)
        {
            composite.Add(new MultiProductPathwiseWrapper(p));
        }
    }

    private static Payoff[] Calls() => [.. Market.Forwards.Select(f => (Payoff)new PlainVanillaPayoff(OptionType.Call, f))];
}
