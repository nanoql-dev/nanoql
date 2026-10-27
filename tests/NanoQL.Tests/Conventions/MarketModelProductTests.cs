// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market-model products' shape (the S-mmp plan's MMPR1): a product writes each step's counts and cash flows into
/// the caller's buffers; a holder (a composite, the accounting engine) steps a clone of the product it is given, never
/// the caller's; <c>Clone</c> copies a product's state, so a clone taken mid-path goes on as the original; QuantLib's
/// texts for a composite stepped before it is finalized or added to after; and the refusals of inputs QuantLib reads
/// past (MMPR4's refusals).
/// </summary>
public class MarketModelProductTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void AStepWritesTheCallersBuffers()
    {
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        int[] counts = [7, 7, 7, 7, 7, 7];
        MarketModelMultiProductCashFlow[][] flows = [.. Enumerable.Range(0, 6).Select(_ => new MarketModelMultiProductCashFlow[1])];
        LMMCurveState state = State(Market.Forwards.Select(f => f + 0.002).ToArray());
        forwards.Reset();
        for (int s = 0; s < 6; ++s)
        {
            bool done = forwards.NextTimeStep(state, counts, flows);
            Assert.Equal(s == 5, done);
            for (int i = 0; i < 6; ++i)
            {
                Assert.Equal(i == s ? 1 : 0, counts[i]);
            }

            Assert.Equal(s, flows[s][0].TimeIndex);
            Assert.Equal((state.ForwardRate(s) - Market.Forwards[s]) * Market.Accruals[s], flows[s][0].Amount);
        }
    }

    [Fact]
    public void AHolderStepsItsOwnClone()
    {
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        var composite = new MultiProductComposite();
        composite.Add(forwards);
        composite.FinalizeComposite();
        Assert.NotSame(forwards, composite.Item(0));

        // the engine clones the composite: a caller's composite stepped twice, valued, then stepped again pays the
        // third rate's forward
        LMMCurveState state = State(Market.Forwards);
        int[] counts = new int[6];
        MarketModelMultiProductCashFlow[][] flows = [.. Enumerable.Range(0, 6).Select(_ => new MarketModelMultiProductCashFlow[1])];
        composite.Reset();
        composite.NextTimeStep(state, counts, flows);
        composite.NextTimeStep(state, counts, flows);
        _ = Market.TerminalPc(composite, 3, 16);
        composite.NextTimeStep(state, counts, flows);
        Assert.Equal([0, 0, 1, 0, 0, 0], counts);
        Assert.Equal(2, flows[2][0].TimeIndex);
    }

    [Fact]
    public void ACloneTakenMidPathGoesOnAsTheOriginal()
    {
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        double[] rebateTimes = [1.0, 2.0];
        var rebate = new MarketModelCashRebate(
            new EvolutionDescription(Market.RateTimes, rebateTimes), rebateTimes, new Matrix(1, 2, 0.01), 1);
        var composite = new SingleProductComposite();
        composite.Add(forwards);
        composite.Add(rebate);
        composite.FinalizeComposite();
        LMMCurveState state = State(Market.Forwards);
        int[] counts = new int[1], cloneCounts = new int[1];
        var flows = new MarketModelMultiProductCashFlow[1][] { new MarketModelMultiProductCashFlow[composite.MaxNumberOfCashFlowsPerProductPerStep] };
        var cloneFlows = new MarketModelMultiProductCashFlow[1][] { new MarketModelMultiProductCashFlow[composite.MaxNumberOfCashFlowsPerProductPerStep] };
        composite.Reset();
        composite.NextTimeStep(state, counts, flows);
        composite.NextTimeStep(state, counts, flows);
        MarketModelMultiProduct clone = composite.Clone();
        bool done = false;
        while (!done)
        {
            done = composite.NextTimeStep(state, counts, flows);
            Assert.Equal(done, clone.NextTimeStep(state, cloneCounts, cloneFlows));
            Assert.Equal(counts[0], cloneCounts[0]);
            Assert.Equal(flows[0][..counts[0]], cloneFlows[0][..cloneCounts[0]]);
        }
    }

    [Fact]
    public void ACompositeStepsOnlyOnceFinalized()
    {
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        var composite = new MultiProductComposite();
        Assert.Equal("composite not finalized", Assert.Throws<NanoQLException>(() => composite.Evolution).Message);
        Assert.Equal("composite not finalized", Assert.Throws<NanoQLException>(() => composite.SuggestedNumeraires()).Message);
        Assert.Equal("composite not finalized", Assert.Throws<NanoQLException>(() => composite.PossibleCashFlowTimes()).Message);
        Assert.Equal("no sub-product provided", Assert.Throws<NanoQLException>(composite.FinalizeComposite).Message);
        composite.Add(forwards);
        Assert.Equal(
            "composite not finalized",
            Assert.Throws<NanoQLException>(() => composite.NextTimeStep(State(Market.Forwards), new int[6], [])).Message);
        var other = new MarketModelProductMarket(5);
        Assert.Equal(
            "incompatible rate times",
            Assert.Throws<NanoQLException>(
                () => composite.Add(new MultiStepForwards(other.RateTimes, other.Accruals, other.PaymentTimes, other.Forwards))).Message);
        composite.FinalizeComposite();
        Assert.Equal("product already finalized", Assert.Throws<NanoQLException>(() => composite.Add(forwards)).Message);
        Assert.Equal("product already finalized", Assert.Throws<NanoQLException>(composite.FinalizeComposite).Message);
        Assert.Equal(EvolutionDescriptionFunctions.TerminalMeasure(composite.Evolution), composite.SuggestedNumeraires());
        Assert.Equal(1.0, composite.Multiplier(0));
        Assert.Equal(1, composite.Size);
        Assert.Throws<ArgumentOutOfRangeException>(() => composite.Item(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => composite.Multiplier(-1));
    }

    [Fact]
    public void ASubtractedPartIsNegated()
    {
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        var composite = new MultiProductComposite();
        composite.Add(forwards, 2.0);
        composite.Subtract(forwards, 0.5);
        composite.FinalizeComposite();
        Assert.Equal(2.0, composite.Multiplier(0));
        Assert.Equal(-0.5, composite.Multiplier(1));
        Assert.Equal(12, composite.NumberOfProducts);
        Assert.Equal(1, composite.MaxNumberOfCashFlowsPerProductPerStep);
        Assert.Equal(Market.PaymentTimes, composite.PossibleCashFlowTimes());
    }

    [Fact]
    public void TheMultiStepBasesSuggestQuantLibsMeasures()
    {
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        Assert.Equal([1, 2, 3, 4, 5, 6], forwards.SuggestedNumeraires());
        Assert.Equal(Market.RateTimes[..^1], forwards.Evolution.EvolutionTimes);
        Assert.Equal([(0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6)], forwards.Evolution.RelevanceRates);
        var nothing = new MultiStepNothing(forwards.Evolution, 2, 3);
        Assert.Equal(2, nothing.NumberOfProducts);
        Assert.Equal(0, nothing.MaxNumberOfCashFlowsPerProductPerStep);
        Assert.Empty(nothing.PossibleCashFlowTimes());
        int[] counts = [5, 5];
        nothing.Reset();
        Assert.False(nothing.NextTimeStep(State(Market.Forwards), counts, []));
        Assert.Equal([0, 0], counts);
        Assert.False(nothing.NextTimeStep(State(Market.Forwards), counts, []));
        Assert.True(nothing.NextTimeStep(State(Market.Forwards), counts, []));
        MarketModelMultiProduct copy = nothing.Clone();
        Assert.True(copy.NextTimeStep(State(Market.Forwards), counts, []));
    }

    [Fact]
    public void TheOptionletsPayTheirPayoffs()
    {
        Payoff[] payoffs = [.. Market.Forwards.Select(f => (Payoff)new PlainVanillaPayoff(OptionType.Call, f))];
        var optionlets = new MultiStepOptionlets(Market.RateTimes, Market.Accruals, Market.PaymentTimes, payoffs);
        Assert.Equal(6, optionlets.NumberOfProducts);
        Assert.Equal(1, optionlets.MaxNumberOfCashFlowsPerProductPerStep);
        Assert.Equal(Market.PaymentTimes, optionlets.PossibleCashFlowTimes());
        int[] counts = new int[6];
        MarketModelMultiProductCashFlow[][] flows = [.. Enumerable.Range(0, 6).Select(_ => new MarketModelMultiProductCashFlow[1])];
        LMMCurveState state = State(Market.Forwards.Select(f => f + 0.004).ToArray());
        optionlets.Reset();
        optionlets.NextTimeStep(state, counts, flows);
        MarketModelMultiProduct copy = optionlets.Clone();
        int[] copyCounts = new int[6];
        copy.NextTimeStep(state, copyCounts, flows);
        Assert.Equal(1, copyCounts[1]);
        Assert.Equal(Math.Max(state.ForwardRate(1) - Market.Forwards[1], 0.0) * Market.Accruals[1], flows[1][0].Amount);
    }

    [Fact]
    public void TheRebatePaysEachProductAndRefusesASecondStep()
    {
        double[] times = [1.0];
        var amounts = new Matrix(2, 1);
        amounts[0, 0] = 0.01;
        amounts[1, 0] = 0.02;
        var rebate = new MarketModelCashRebate(new EvolutionDescription(Market.RateTimes, times), times, amounts, 2);
        Assert.Equal("not implemented (yet?)", Assert.Throws<NanoQLException>(() => rebate.SuggestedNumeraires()).Message);
        Assert.Equal(times, rebate.PossibleCashFlowTimes());
        Assert.Equal(2, rebate.NumberOfProducts);
        Assert.Equal(1, rebate.MaxNumberOfCashFlowsPerProductPerStep);
        int[] counts = new int[2];
        MarketModelMultiProductCashFlow[][] flows = [new MarketModelMultiProductCashFlow[1], new MarketModelMultiProductCashFlow[1]];
        rebate.Reset();
        MarketModelMultiProduct copy = rebate.Clone();
        Assert.True(rebate.NextTimeStep(State(Market.Forwards), counts, flows));
        Assert.Equal([1, 1], counts);
        Assert.Equal(0.02, flows[1][0].Amount);
        Assert.Equal(
            "the rebate is stepped past its payment times",
            Assert.Throws<NanoQLException>(() => rebate.NextTimeStep(State(Market.Forwards), counts, flows)).Message);
        Assert.True(copy.NextTimeStep(State(Market.Forwards), counts, flows));
    }

    [Fact]
    public void TheProductsInputsAreSizedAsTheyAreRead()
    {
        double[] rt = Market.RateTimes, a = Market.Accruals, pt = Market.PaymentTimes, k = Market.Forwards;
        Assert.Equal(
            "Rate times must contain at least two values (Parameter 'rateTimes')",
            Assert.Throws<ArgumentException>(() => new MultiStepForwards([0.5], a, pt, k)).Message);
        Assert.Equal(
            "Incorrect number of strikes given, should be 1 to 6 not 0 (Parameter 'strikes')",
            Assert.Throws<ArgumentException>(() => new MultiStepForwards(rt, a, pt, [])).Message);
        Assert.Equal(
            "Incorrect number of strikes given, should be 1 to 6 not 7 (Parameter 'strikes')",
            Assert.Throws<ArgumentException>(() => new MultiStepForwards(rt, [.. a, 0.5], [.. pt, 4.0], [.. k, 0.04])).Message);
        Assert.Equal(
            "Incorrect number of accruals given, should be at least 6 not 5 (Parameter 'accruals')",
            Assert.Throws<ArgumentException>(() => new MultiStepForwards(rt, a[..5], pt, k)).Message);
        Assert.Equal(
            "Incorrect number of paymentTimes given, should be at least 6 not 5 (Parameter 'paymentTimes')",
            Assert.Throws<ArgumentException>(() => new MultiStepForwards(rt, a, pt[..5], k)).Message);
        Payoff[] payoffs = [.. k.Select(f => (Payoff)new PlainVanillaPayoff(OptionType.Call, f))];
        Assert.Equal(
            "Incorrect number of payoffs given, should be 1 to 6 not 0 (Parameter 'payoffs')",
            Assert.Throws<ArgumentException>(() => new MultiStepOptionlets(rt, a, pt, [])).Message);
        Assert.Equal(
            "Incorrect number of accruals given, should be at least 6 not 2 (Parameter 'accruals')",
            Assert.Throws<ArgumentException>(() => new MultiStepOptionlets(rt, a[..2], pt, payoffs)).Message);
        Assert.Equal(
            "Incorrect number of paymentTimes given, should be at least 6 not 3 (Parameter 'paymentTimes')",
            Assert.Throws<ArgumentException>(() => new MultiStepOptionlets(rt, a, pt[..3], payoffs)).Message);
        Assert.Throws<ArgumentNullException>(() => new MultiStepOptionlets(rt, a, pt, [payoffs[0], null!]));

        // fewer products than rates: the path ends with the last
        var two = new MultiStepForwards(rt, a, pt, k[..2]);
        Assert.Equal(2, two.NumberOfProducts);

        var evolution = new EvolutionDescription(rt, [1.0, 2.0]);
        Assert.Equal(
            "the number of rows in the matrix must equal the number of products (Parameter 'amounts')",
            Assert.Throws<ArgumentException>(() => new MarketModelCashRebate(evolution, [1.0, 2.0], new Matrix(1, 2), 2)).Message);
        Assert.Equal(
            "the number of columns in the matrix must equal the number of payment times (Parameter 'amounts')",
            Assert.Throws<ArgumentException>(() => new MarketModelCashRebate(evolution, [1.0, 2.0], new Matrix(1, 3), 1)).Message);
        Assert.Equal(
            "the number of evolution times must equal the number of payment times (Parameter 'evolution')",
            Assert.Throws<ArgumentException>(() => new MarketModelCashRebate(evolution, [1.0], new Matrix(1, 1), 1)).Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiStepNothing(evolution, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiStepNothing(evolution, 1, -1));
    }

    [Fact]
    public void AProductOutlastingTheEvolverIsRefused()
    {
        var forwards = new MultiStepForwards(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        var nothing = new MultiStepNothing(forwards.Evolution, 1, 100);
        var evolver = new LogNormalFwdRatePc(
            Market.FlatVol(nothing.Evolution, 3), new MTBrownianGeneratorFactory(42), [6, 6, 6, 6, 6, 6]);
        var engine = new AccountingEngine(evolver, nothing, 1.0);
        Assert.Equal(
            "the product isn't finished at the evolver's last step",
            Assert.Throws<NanoQLException>(() => engine.MultiplePathValues(new SequenceStatisticsInc(), 1)).Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.MultiplePathValues(new SequenceStatisticsInc(), -1));

        // the composite, at its last step
        var composite = new MultiProductComposite();
        composite.Add(nothing);
        composite.FinalizeComposite();
        int[] counts = new int[1];
        composite.Reset();
        for (int s = 0; s < 6; ++s)
        {
            Assert.Equal(s == 5, composite.NextTimeStep(State(Market.Forwards), counts, [[]]));
        }

        Assert.Equal(
            "the composite is stepped past its last step",
            Assert.Throws<NanoQLException>(() => composite.NextTimeStep(State(Market.Forwards), counts, [[]])).Message);
    }

    [Fact]
    public void TheDiscounterNeedsTwoRateTimes()
    {
        Assert.Equal(
            "at least two rate times are required (Parameter 'rateTimes')",
            Assert.Throws<ArgumentException>(() => new MarketModelDiscounter(0.5, [1.0])).Message);
    }

    [Fact]
    public void TheDifferencesNeedMatchingModels()
    {
        var evolution = new EvolutionDescription(Market.RateTimes);
        MarketModel m1 = Market.FlatVol(evolution, 3);
        var other = new MarketModelProductMarket(6, 0.5, 0.5, 0.04);
        MarketModel m2 = other.FlatVol(evolution, 3);
        MarketModel m3 = Market.FlatVol(new EvolutionDescription(Market.RateTimes, [1.0, 2.0]), 3);
        Assert.Equal("initialRates do not match", Assert.Throws<NanoQLException>(() => MarketModelDifferencesFunctions.RateVolDifferences(m1, m2)).Message);
        Assert.Equal("Evolution times do not match", Assert.Throws<NanoQLException>(() => MarketModelDifferencesFunctions.RateVolDifferences(m1, m3)).Message);
        Assert.Equal("initialRates do not match", Assert.Throws<NanoQLException>(() => MarketModelDifferencesFunctions.RateInstVolDifferences(m1, m2, 0)).Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => MarketModelDifferencesFunctions.RateInstVolDifferences(m1, m1, -1));

        // coterminalSwapPseudoRoots, which QuantLib declares and never defines, isn't ported
        Assert.Null(typeof(MarketModelDifferencesFunctions).GetMethod("CoterminalSwapPseudoRoots"));
    }

    private static LMMCurveState State(double[] forwards)
    {
        var state = new LMMCurveState(Market.RateTimes);
        state.SetOnForwardRates(forwards);
        return state;
    }
}
