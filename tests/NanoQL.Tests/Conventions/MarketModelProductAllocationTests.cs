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
/// multi-product composite and in a single-product composite, in the terminal and the money-market measures.
/// </summary>
public class MarketModelProductAllocationTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Theory]
    [InlineData("multi", "terminal")]
    [InlineData("multi", "moneyMarket")]
    [InlineData("single", "terminal")]
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
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        Payoff[] payoffs = [.. Market.Forwards.Select(f => (Payoff)new PlainVanillaPayoff(OptionType.Call, f))];
        var optionlets = new MultiStepOptionlets(Market.RateTimes, Market.Accruals, Market.PaymentTimes, payoffs);
        double[] rebateTimes = [0.75, 1.25];
        var rebate = new MarketModelCashRebate(
            new EvolutionDescription(Market.RateTimes, rebateTimes), rebateTimes, new Matrix(2, 2, 0.01), 2);
        MarketModelComposite composite = kind == "multi" ? new MultiProductComposite() : new SingleProductComposite();
        composite.Add(forwards);
        composite.Add(optionlets, 2.0);
        composite.Add(new MultiStepNothing(forwards.Evolution, 1, 3));
        composite.Add(rebate);
        composite.FinalizeComposite();
        EvolutionDescription evolution = composite.Evolution;
        int[] numeraires = measure == "terminal"
            ? [.. EvolutionDescriptionFunctions.TerminalMeasure(evolution)]
            : [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(evolution)];
        var evolver = new LogNormalFwdRatePc(Market.FlatVol(evolution, 3), new MTBrownianGeneratorFactory(42), numeraires);
        return (new AccountingEngine(evolver, composite, Market.Discounts[numeraires[0]]), new SequenceStatisticsInc());
    }
}
