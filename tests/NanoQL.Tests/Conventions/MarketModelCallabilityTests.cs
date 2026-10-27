// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market-model callability of the S-mmp plan's third commit: the refusals of inputs QuantLib reads past or
/// misreads (MMPR4's refusals: the node-data collection's times, the strategies' coefficients and parameters, the
/// call-specified product's rebate, the upper-bound engine's inner evolvers), with QuantLib's default nothing control
/// accepted beside a rebate at a subset of the times (MPP24); and two routes to one value: the Longstaff-Schwartz
/// regression against least squares, and the lower bound against the upper bound (SMD13).
/// </summary>
public class MarketModelCallabilityTests
{
    private const double K = 0.032;
    private const string SubsetsTodo =
        "evolution times for product, basis system, rebate and control must be subsets of the passed evolution times";

    private const string ExerciseTimesTodo =
        "rebate, control and basis system must have the same exercise---not evolution---times";

    private static readonly MarketModelProductMarket Market = new(6);
    private static readonly double[] ExerciseTimes = Market.RateTimes[..^1];

    [Fact]
    public void TheStrategiesAndProductsInputsAreSizedAsTheyAreRead()
    {
        double[] rt = Market.RateTimes;
        var basis = new SwapBasisSystem(rt, ExerciseTimes);
        var nullValue = new NothingExerciseValue(rt);
        var evolution = new EvolutionDescription(rt, ExerciseTimes);
        int[] numeraires = [6, 6, 6, 6, 6, 6];
        ImmutableArray<ImmutableArray<double>> coefficients = [.. basis.NumberOfFunctions().Select(n => ImmutableArray.Create(new double[n]))];
        Assert.Equal(
            "the basis system has 3 exercises, fewer than the exercise value's 6 (Parameter 'basisSystem')",
            Assert.Throws<ArgumentException>(
                () => new LongstaffSchwartzExerciseStrategy(new SwapBasisSystem(rt, ExerciseTimes[..3]), coefficients, evolution, numeraires, nullValue, nullValue)).Message);
        Assert.Equal(
            "5 exercises' coefficients given, fewer than the 6 exercises (Parameter 'basisCoefficients')",
            Assert.Throws<ArgumentException>(
                () => new LongstaffSchwartzExerciseStrategy(basis, coefficients[..5], evolution, numeraires, nullValue, nullValue)).Message);
        Assert.Equal(
            "exercise 0's 4 coefficients are more than its 3 basis functions (Parameter 'basisCoefficients')",
            Assert.Throws<ArgumentException>(
                () => new LongstaffSchwartzExerciseStrategy(basis, coefficients.SetItem(0, [1.0, 2.0, 3.0, 4.0]), evolution, numeraires, nullValue, nullValue)).Message);

        var form = new TriggeredSwapExercise(rt, ExerciseTimes, [.. Enumerable.Repeat(K, 6)]);
        ImmutableArray<double>[] parameters = [.. Enumerable.Repeat(ImmutableArray.Create(K), 6)];
        Assert.Equal(
            "5 exercises' parameters given, fewer than the 6 exercises (Parameter 'parameters')",
            Assert.Throws<ArgumentException>(() => new ParametricExerciseAdapter(form, parameters.AsSpan(0, 5))).Message);
        parameters[2] = [];
        Assert.Equal(
            "exercise 2's 0 parameters are fewer than the rule's 1 (Parameter 'parameters')",
            Assert.Throws<ArgumentException>(() => new ParametricExerciseAdapter(form, parameters)).Message);

        var trigger = new SwapRateTrigger(rt, [.. Enumerable.Repeat(K, 6)], ExerciseTimes);
        var receiver = new MultiStepSwap(rt, Market.Accruals, Market.Accruals, Market.PaymentTimes, K, false);
        Assert.Equal(
            "the rebate's 2 products are more than the underlying's 1 (Parameter 'rebate')",
            Assert.Throws<ArgumentException>(() => new CallSpecifiedMultiProduct(receiver, trigger, new ExerciseAdapter(nullValue, 2))).Message);

        var model = Market.FlatVol(evolution, 3);
        MarketModelEvolver[] inner = [.. Enumerable.Range(0, 4).Select(s => (MarketModelEvolver)new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(42 + s), numeraires, s))];
        Assert.Equal(
            "4 inner evolvers given, fewer than the 5 exercise dates before the last step (Parameter 'innerEvolvers')",
            Assert.Throws<ArgumentException>(
                () => new UpperBoundEngine(
                    new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(184), numeraires), inner, receiver, nullValue, receiver, nullValue, trigger, 0.9)).Message);

        Assert.Equal(
            "Incorrect number of payoffs given, should be at least 6 not 5 (Parameter 'payoffs')",
            Assert.Throws<ArgumentException>(
                () => new BermudanSwaptionExerciseValue(rt, [.. Enumerable.Repeat<Payoff>(new PlainVanillaPayoff(OptionType.Call, K), 5)])).Message);
    }

    [Fact]
    public void TheNodeDataCollectionRefusesTimesItWouldMisread()
    {
        double[] rt = Market.RateTimes;
        var receiver = new MultiStepSwap(rt, Market.Accruals, Market.Accruals, Market.PaymentTimes, K, false);
        var basis = new SwapBasisSystem(rt, ExerciseTimes);
        var nullValue = new NothingExerciseValue(rt);

        // an evolver stepping at the call-specified product's times, off the swap's
        double[] offTimes = [.. ExerciseTimes[..^1].Select(t => t + 0.1)];
        var callable = new CallSpecifiedMultiProduct(receiver, new SwapRateTrigger(rt, [.. Enumerable.Repeat(K, 5)], offTimes));
        Assert.StartsWith(
            $"{SubsetsTodo}: the evolver steps 11 times, the product 6;",
            Assert.Throws<ArgumentException>(() => CollectNodeDataFunctions.CollectNodeData(Evolver(callable.Evolution), receiver, basis, nullValue, nullValue, 4)).Message,
            StringComparison.Ordinal);

        // a provider off the product's times
        Assert.StartsWith(
            $"{SubsetsTodo}: the data provider's time 0.6 isn't the product's;",
            Assert.Throws<ArgumentException>(
                () => CollectNodeDataFunctions.CollectNodeData(Evolver(receiver.Evolution), receiver, new SwapBasisSystem(rt, offTimes), nullValue, nullValue, 4)).Message,
            StringComparison.Ordinal);

        // a provider whose exercise times aren't the rebate's
        bool[] everyOther = [true, false, true, false, true, false];
        Assert.StartsWith(
            $"{ExerciseTimesTodo}: the data provider's exercise times aren't the rebate's;",
            Assert.Throws<ArgumentException>(
                () => CollectNodeDataFunctions.CollectNodeData(Evolver(receiver.Evolution), receiver, basis, new NothingExerciseValue(rt, everyOther), nullValue, 4)).Message,
            StringComparison.Ordinal);

        // a control whose evolution misses one of the rebate's exercise times
        var lateControl = new MarketModelCashRebate(new EvolutionDescription(rt, ExerciseTimes[1..]), ExerciseTimes[1..], new Matrix(1, 5), 1);
        Assert.StartsWith(
            $"{ExerciseTimesTodo}: the control's evolution misses the rebate's exercise time 0.5;",
            Assert.Throws<ArgumentException>(
                () => CollectNodeDataFunctions.CollectNodeData(Evolver(receiver.Evolution), receiver, basis, nullValue, new ControlOf(lateControl), 4)).Message,
            StringComparison.Ordinal);

        var two = new MultiProductComposite();
        two.Add(receiver);
        two.Add(receiver);
        two.FinalizeComposite();
        Assert.Equal(
            "a single product is required (Parameter 'product')",
            Assert.Throws<ArgumentException>(() => CollectNodeDataFunctions.CollectNodeData(Evolver(two.Evolution), two, basis, nullValue, nullValue, 4)).Message);
    }

    [Fact]
    public void QuantLibsDefaultControlBesideARebateAtASubsetIsTheMatchedOne()
    {
        // MPP24: a rebate and basis system at 0.5, 1.5 and 2.5; QuantLib's default control flagged at every rate time
        double[] rt = Market.RateTimes;
        var receiver = new MultiStepSwap(rt, Market.Accruals, Market.Accruals, Market.PaymentTimes, K, false);
        bool[] flags = [true, false, true, false, true, false];
        var rebate = new NothingExerciseValue(rt, flags);
        var basis = new SwapBasisSystem(rt, [0.5, 1.5, 2.5]);
        NodeData[][] matched = CollectNodeDataFunctions.CollectNodeData(Evolver(receiver.Evolution), receiver, basis, rebate, new NothingExerciseValue(rt, flags), 1023);
        NodeData[][] byDefault = CollectNodeDataFunctions.CollectNodeData(Evolver(receiver.Evolution), receiver, basis, rebate, new NothingExerciseValue(rt), 1023);
        Assert.Equal(4, byDefault.Length);
        for (int i = 0; i < matched.Length; ++i)
        {
            for (int j = 0; j < matched[i].Length; ++j)
            {
                Assert.Equal(matched[i][j].ExerciseValue, byDefault[i][j].ExerciseValue);
                Assert.Equal(matched[i][j].CumulatedCashFlows, byDefault[i][j].CumulatedCashFlows);
                Assert.Equal(matched[i][j].Values, byDefault[i][j].Values);
                Assert.Equal(matched[i][j].ControlValue, byDefault[i][j].ControlValue);
                Assert.Equal(matched[i][j].IsValid, byDefault[i][j].IsValid);
            }
        }
    }

    [Fact]
    public void TheRegressionIsLeastSquares()
    {
        double[] rt = Market.RateTimes;
        var receiver = new MultiStepSwap(rt, Market.Accruals, Market.Accruals, Market.PaymentTimes, K, false);
        var nullValue = new NothingExerciseValue(rt);
        NodeData[][] data = CollectNodeDataFunctions.CollectNodeData(
            Evolver(receiver.Evolution), receiver, new SwapBasisSystem(rt, ExerciseTimes), nullValue, new NothingExerciseValue(rt), 1023);

        // the last exercise, whose cash flows the regression doesn't change before regressing on them
        NodeData[] last = [.. data[^1].Where(d => d.IsValid)];
        int n = last[0].Values.Length;
        GeneralLinearLeastSquares ls = GeneralLinearLeastSquares.Create(
            [.. last.Select(d => d.Values)],
            [.. last.Select(d => d.CumulatedCashFlows - d.ControlValue)],
            [.. Enumerable.Range(0, n).Select(k => (Func<double[], double>)(x => x[k]))]);

        GenericLSRegressionResults r = GenericLSRegressionFunctions.GenericLongstaffSchwartzRegression(data);
        Vector expected = ls.Coefficients();
        for (int k = 0; k < n; ++k)
        {
            Assert.Equal(expected[k], r.BasisCoefficients[^1][k], 1e-10 * Math.Max(1.0, Math.Abs(expected[k])));
        }
    }

    [Fact]
    public void TheRegressionAndTheOptimisationRefuseTheRowsQuantLibReadsPast()
    {
        // the regression: a valid path of fewer values than the first valid path's, whose tail QuantLib reads from the
        // path before (genericlsregression.cpp:46-49)
        static NodeData Node(double[] values, double cash) =>
            new() { Values = values, CumulatedCashFlows = cash, ExerciseValue = values[0], IsValid = true };
        NodeData[][] regression =
        [
            [new() { IsValid = true }, new() { IsValid = true }, new() { IsValid = true }],
            [Node([1.0, 0.03, 0.0009], 0.01), Node([1.0, 0.04], 0.02), Node([1.0, 0.035, 0.001225], 0.015)],
        ];
        Assert.Equal(
            "path 1 holds 2 values, not the first valid path's 3 (Parameter 'simulationData')",
            Assert.Throws<ArgumentException>(() => GenericLSRegressionFunctions.GenericLongstaffSchwartzRegression(regression)).Message);

        // the optimisation updates the row before over its own paths, reading the exercise row's
        // (parametricexercise.cpp:113-118): an exercise row of more paths completes, one of fewer is refused
        var rule = new TriggeredSwapExercise(Market.RateTimes, ExerciseTimes.AsSpan(0, 1), [K]);
        NodeData[] Exercise(int paths) => [.. Enumerable.Range(0, paths).Select(j => Node([0.03 + (0.001 * j)], 0.01))];
        NodeData[] Before(int paths) => [.. Enumerable.Range(0, paths).Select(_ => new NodeData { IsValid = true })];
        var criteria = new EndCriteria(1000, 100, 1e-8, 1e-8, 1e-8);
        GenericEarlyExerciseOptimizationResults longer =
            ParametricExerciseFunctions.GenericEarlyExerciseOptimization([Before(3), Exercise(4)], rule, criteria, new Simplex(0.01));
        Assert.True(Assert.Single(longer.Parameters).Length == 1 && double.IsFinite(longer.BiasedEstimate));
        Assert.Equal(
            "row 1 holds 3 paths, fewer than row 0's 4 (Parameter 'simulationData')",
            Assert.Throws<ArgumentException>(
                () => ParametricExerciseFunctions.GenericEarlyExerciseOptimization([Before(4), Exercise(3)], rule, criteria, new Simplex(0.01))).Message);
    }

    [Fact]
    public void TheUpperBoundsGapIsAtLeastZeroBesideTheLowerBound()
    {
        // testCallableSwapLS' route on six semi-annual rates: the lower bound, the Bermudan into the payer swap, and the
        // upper bound's gap over the callable receiver swap
        double[] rt = Market.RateTimes;
        var receiver = new MultiStepSwap(rt, Market.Accruals, Market.Accruals, Market.PaymentTimes, K, false);
        var payer = new MultiStepSwap(rt, Market.Accruals, Market.Accruals, Market.PaymentTimes, K, true);
        var nullValue = new NothingExerciseValue(rt);
        var basis = new SwapBasisSystem(rt, ExerciseTimes);
        EvolutionDescription evolution = receiver.Evolution;
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(evolution)];
        MarketModel model = Market.FlatVol(evolution, 3);
        NodeData[][] data = CollectNodeDataFunctions.CollectNodeData(
            new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(42), numeraires), receiver, basis, nullValue, new NothingExerciseValue(rt), 2047);
        GenericLSRegressionResults r = GenericLSRegressionFunctions.GenericLongstaffSchwartzRegression(data);
        var strategy = new LongstaffSchwartzExerciseStrategy(basis, r.BasisCoefficients, evolution, numeraires, nullValue, nullValue);

        var bermudan = new CallSpecifiedMultiProduct(new MultiStepNothing(evolution), strategy, payer);
        (ImmutableArray<double> lower, _) = MarketModelProductMarket.Simulate(
            new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(43), numeraires), bermudan, Market.Discounts[numeraires[0]], 4095);

        var engine = new UpperBoundEngine(
            new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(184), numeraires),
            [.. Enumerable.Range(0, 6).Select(s => (MarketModelEvolver)new LogNormalFwdRatePc(model, new MTBrownianGeneratorFactory(42 + s), numeraires, s))],
            receiver, nullValue, receiver, nullValue, strategy, Market.Discounts[numeraires[0]]);
        var gap = new GeneralStatistics();
        engine.MultiplePathValues(gap, 127, 64);

        Assert.Equal(0.0, gap.Min());
        Assert.True(lower[0] > 0.0);
        Assert.True(gap.Mean() < lower[0], $"gap {gap.Mean()} ± {gap.ErrorEstimate()} against the lower bound {lower[0]}");
    }

    [Fact]
    public void TheCallableProductsHoldClonesOfTheirParts()
    {
        double[] rt = Market.RateTimes;
        var receiver = new MultiStepSwap(rt, Market.Accruals, Market.Accruals, Market.PaymentTimes, K, false);
        var trigger = new SwapRateTrigger(rt, [.. Enumerable.Repeat(0.0, 6)], ExerciseTimes);
        var nullValue = new NothingExerciseValue(rt);
        var adapter = new ExerciseAdapter(nullValue);
        Assert.IsType<NothingExerciseValue>(adapter.ExerciseValue);
        Assert.NotSame(nullValue, adapter.ExerciseValue);

        var callable = new CallSpecifiedMultiProduct(receiver, trigger, adapter);
        Assert.IsType<MultiStepSwap>(callable.Underlying);
        Assert.NotSame(receiver, callable.Underlying);
        Assert.IsType<ExerciseAdapter>(callable.Rebate);
        Assert.NotSame(adapter, callable.Rebate);
        Assert.NotSame(trigger, callable.Strategy);

        // the pathwise twin: its parts, a clone going on as the original, and its callability switched off and on
        var swap = new MarketModelPathwiseSwap(rt, Market.Accruals, [K], -1.0);
        var pathwise = new CallSpecifiedPathwiseMultiProduct(swap, trigger);
        Assert.IsType<MarketModelPathwiseSwap>(pathwise.Underlying);
        Assert.IsType<MarketModelPathwiseCashRebate>(pathwise.Rebate);
        Assert.IsType<SwapRateTrigger>(pathwise.Strategy);
        var state = new LMMCurveState(rt);
        state.SetOnForwardRates(Market.Forwards);
        int[] counts = new int[1];
        MarketModelPathwiseMultiProductCashFlow[][] flows =
            [[.. Enumerable.Range(0, pathwise.MaxNumberOfCashFlowsPerProductPerStep).Select(_ => new MarketModelPathwiseMultiProductCashFlow(7))]];
        pathwise.Reset();
        pathwise.DisableCallability();
        pathwise.NextTimeStep(state, counts, flows);
        Assert.Equal(1, counts[0]);
        Assert.Equal(1, flows[0][0].TimeIndex);
        MarketModelPathwiseMultiProduct copy = pathwise.Clone();
        pathwise.EnableCallability();
        pathwise.NextTimeStep(state, counts, flows);

        // called at the second exercise: the rebate's flow, its index past the swap's cash-flow times
        Assert.Equal(1, counts[0]);
        Assert.Equal(swap.PossibleCashFlowTimes().Length + 1, flows[0][0].TimeIndex);
        ((CallSpecifiedPathwiseMultiProduct)copy).DisableCallability();
        copy.NextTimeStep(state, counts, flows);
        Assert.Equal(2, flows[0][0].TimeIndex);

        // a Longstaff-Schwartz strategy on a swap-forward basis system steps its own clone of it
        var basis = new SwapForwardBasisSystem(rt, ExerciseTimes);
        var evolution = new EvolutionDescription(rt, ExerciseTimes);
        var strategy = new LongstaffSchwartzExerciseStrategy(
            basis, [.. basis.NumberOfFunctions().Select(f => ImmutableArray.Create(new double[f]))], evolution, [6, 6, 6, 6, 6, 6],
            nullValue, new NothingExerciseValue(rt));
        strategy.Reset();
        strategy.NextStep(state);
        Assert.True(strategy.Exercise(state));
    }

    private static LogNormalFwdRatePc Evolver(EvolutionDescription evolution)
    {
        int[] numeraires = [.. EvolutionDescriptionFunctions.TerminalMeasure(evolution)];
        return new LogNormalFwdRatePc(Market.FlatVol(evolution, Math.Min(3, 6)), new MTBrownianGeneratorFactory(42), numeraires);
    }

    // A control of nothing on a given evolution: the cash rebate's times as its own, its value 0.
    private sealed class ControlOf : MarketModelExerciseValue
    {
        private readonly MarketModelCashRebate rebate;

        public ControlOf(MarketModelCashRebate rebate) => this.rebate = rebate;

        public override int NumberOfExercises => rebate.Evolution.EvolutionTimes.Length;

        public override EvolutionDescription Evolution => rebate.Evolution;

        public override ImmutableArray<double> PossibleCashFlowTimes() => rebate.PossibleCashFlowTimes();

        public override void NextStep(CurveState currentState)
        {
        }

        public override void Reset()
        {
        }

        public override ImmutableArray<bool> IsExerciseTime() => [.. Enumerable.Repeat(true, NumberOfExercises)];

        public override MarketModelMultiProductCashFlow Value(CurveState currentState) => default;

        public override MarketModelExerciseValue Clone() => this;
    }
}
