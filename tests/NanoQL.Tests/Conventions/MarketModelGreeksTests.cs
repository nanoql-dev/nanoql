// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market-model Greeks' refusals (the S-mmp plan's MMPR5, ruled by the user, and MMPR4's refusals), each beside its
/// valid twin: the pathwise engines on an evolver not in the money-market measure, which QuantLib requires and doesn't
/// check (<c>pathwiseaccountingengine.cpp:99</c>), and the money-market evolver; the swaption pseudo-derivative on steps
/// straddling its expiry or on unequal displacements, refused as <c>swaptionImpliedVolatility</c> refuses
/// (<c>swaptionpseudojacobian.cpp:44</c>, <c>:53</c>), and on a grid holding the expiry; and the inputs QuantLib reads
/// past: a product off the model's evolution, the vega bumps, the proxy engine's weights and constraints, the
/// constrained evolver's reference model.
/// </summary>
public class MarketModelGreeksTests
{
    private static readonly MarketModelProductMarket Market = new(6);
    private static readonly EvolutionDescription Evolution = new(Market.RateTimes, Market.RateTimes[..^1]);

    [Fact]
    public void ThePathwiseEnginesRefuseAnEvolverOutsideTheMoneyMarketMeasure()
    {
        MarketModel model = Market.FlatVol(Evolution, 3);
        var caplets = new MarketModelPathwiseMultiCaplet(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        int[] terminal = [.. EvolutionDescriptionFunctions.TerminalMeasure(Evolution)];
        int[] moneyMarket = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(Evolution)];
        Matrix[][] bumps = [.. Enumerable.Range(0, 6).Select(_ => new[] { new Matrix(6, 3, 0.0) })];
        LogNormalFwdRateEuler Evolver(int[] numeraires) => new(model, new MTBrownianGeneratorFactory(42), numeraires);

        const string text =
            "the pathwise engines need an evolver in the money-market measure (the discretely compounding money-market account,"
            + " each step's numeraire its first alive rate) (Parameter 'numeraires')";
        Assert.Equal(text, Assert.Throws<ArgumentException>(() => new PathwiseAccountingEngine(Evolver(terminal), caplets, model, 0.95)).Message);
        Assert.Equal(text, Assert.Throws<ArgumentException>(() => new PathwiseVegasAccountingEngine(Evolver(terminal), caplets, model, bumps, 0.95)).Message);
        Assert.Equal(text, Assert.Throws<ArgumentException>(() => new PathwiseVegasOuterAccountingEngine(Evolver(terminal), caplets, model, bumps, 0.95)).Message);

        // the twins, in the money-market measure
        var engine = new PathwiseAccountingEngine(Evolver(moneyMarket), caplets, model, 0.95);
        var stats = new SequenceStatisticsInc(6 * 7);
        engine.MultiplePathValues(stats, 7);
        Assert.Equal(7, stats.Samples);
        _ = new PathwiseVegasAccountingEngine(Evolver(moneyMarket), caplets, model, bumps, 0.95);
        _ = new PathwiseVegasOuterAccountingEngine(Evolver(moneyMarket), caplets, model, bumps, 0.95);

        // a product off the model's evolution, whose steps QuantLib reads by the model's
        var coarse = new EvolutionDescription(Market.RateTimes, [0.5, 1.5, 2.5]);
        MarketModel coarseModel = Market.FlatVol(coarse, 3);
        Assert.Equal(
            "the product's rate and evolution times must be the model's (Parameter 'productEvolution')",
            Assert.Throws<ArgumentException>(
                () => new PathwiseAccountingEngine(
                    new LogNormalFwdRateEuler(coarseModel, new MTBrownianGeneratorFactory(42), [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(coarse)]),
                    caplets,
                    coarseModel,
                    0.95)).Message);

        // the bumps: a list per step, of one size
        Assert.Equal(
            "we need one vector of vega bumps for each step. (Parameter 'vegaBumps')",
            Assert.Throws<ArgumentException>(() => new PathwiseVegasAccountingEngine(Evolver(moneyMarket), caplets, model, bumps[..5], 0.95)).Message);
        Matrix[][] uneven = [.. bumps];
        uneven[3] = [new Matrix(6, 3, 0.0), new Matrix(6, 3, 0.0)];
        Assert.Equal(
            "We must have precisely the same number of bumps for each step. (Parameter 'vegaBumps')",
            Assert.Throws<ArgumentException>(() => new PathwiseVegasOuterAccountingEngine(Evolver(moneyMarket), caplets, model, uneven, 0.95)).Message);
    }

    [Fact]
    public void ThePathwiseAndProxyDeltasAgree()
    {
        // the two routes on plain caplets at the forwards, 8,191 Sobol paths (seed 42): the pathwise deltas summed over
        // the rates, a parallel shift, against the proxy engine's forwards shifted by ±1e-6, within 4 of its errors
        int n = Market.N;
        MarketModel model = Market.FlatVol(Evolution, 3);
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(Evolution)];
        var factory = new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 42);
        var caplets = new MarketModelPathwiseMultiCaplet(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        var pathwise = new SequenceStatisticsInc(n * (n + 1));
        new PathwiseAccountingEngine(new LogNormalFwdRateEuler(model, factory, numeraires), caplets, model, Market.Discounts[0])
            .MultiplePathValues(pathwise, 8191);

        const double h = 1e-6;
        int[] start = [.. Enumerable.Range(0, n)], end = [.. Enumerable.Range(1, n)];
        ConstrainedEvolver Shifted(double shift)
        {
            Matrix corr = ExpCorrelationsFunctions.ExponentialCorrelations(Market.RateTimes, 0.5, 0.2);
            var bumped = new FlatVol(
                [.. Enumerable.Repeat(0.20, n)], new TimeHomogeneousForwardCorrelation(corr, Market.RateTimes), Evolution, 3,
                [.. Market.Forwards.Select(f => f + shift)], new double[n]);
            var e = new LogNormalFwdRateEulerConstrained(bumped, model, factory, numeraires);
            e.SetConstraintType(start, end);
            return e;
        }

        var optionlets = new MultiStepOptionlets(
            Market.RateTimes, Market.Accruals, Market.PaymentTimes, [.. Market.Forwards.Select(f => (Payoff)new PlainVanillaPayoff(OptionType.Call, f))]);
        var values = new SequenceStatisticsInc(n);
        SequenceStatisticsInc[][] proxy = [[new(n)]];
        new ProxyGreekEngine(
            new LogNormalFwdRateEuler(model, factory, numeraires), [[Shifted(-h), Shifted(h)]], [[[0.0, -1.0 / (2 * h), 1.0 / (2 * h)]]],
            start, end, optionlets, Market.Discounts[0]).MultiplePathValues(values, proxy, 8191);

        System.Collections.Immutable.ImmutableArray<double> means = pathwise.Mean();
        System.Collections.Immutable.ImmutableArray<double> delta = proxy[0][0].Mean(), errors = proxy[0][0].ErrorEstimate();
        for (int i = 0; i < n; ++i)
        {
            double sum = 0.0;
            for (int j = 0; j < n; ++j)
            {
                sum += means[n + (i * n) + j];
            }

            Assert.True(Math.Abs(sum - delta[i]) <= 4 * errors[i], $"caplet {i}: pathwise {sum} against proxy {delta[i]} ± {errors[i]}");
        }
    }

    [Fact]
    public void TheOuterEnginesElementaryVegasCombineIntoItsBumpsVegas()
    {
        // QuantLib's multiplePathValues: the elementary vegas' means, a step, rate and factor each, combined with the bumps
        MarketModel model = Market.FlatVol(Evolution, 3);
        var caplets = new MarketModelPathwiseMultiCaplet(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(Evolution)];
        Matrix[][] bumps = [.. Enumerable.Range(0, 6).Select(l => new[] { new Matrix(6, 3, 0.01 * (l + 1)), new Matrix(6, 3, -0.02) })];
        PathwiseVegasOuterAccountingEngine Engine() =>
            new(new LogNormalFwdRateEuler(model, new MTBrownianGeneratorFactory(42), numeraires), caplets, model, bumps, 0.95);

        PathwiseVegasOuterAccountingEngine combined = Engine(), elementary = Engine();
        Assert.Equal(6 * (1 + 6 + 2), combined.NumberOfResults);
        Assert.Equal(6 * (1 + 6 + (6 * 6 * 3)), elementary.NumberOfElementaryResults);
        double[] means = new double[combined.NumberOfResults], errors = new double[combined.NumberOfResults];
        double[] all = new double[elementary.NumberOfElementaryResults], allErrors = new double[elementary.NumberOfElementaryResults];
        combined.MultiplePathValues(means, errors, 63);
        elementary.MultiplePathValuesElementary(all, allErrors, 63);
        for (int p = 0; p < 6; ++p)
        {
            for (int b = 0; b < 2; ++b)
            {
                double vega = 0.0;
                for (int t = 0; t < 6; ++t)
                {
                    for (int r = 0; r < 6; ++r)
                    {
                        for (int f = 0; f < 3; ++f)
                        {
                            vega += bumps[t][b][r, f] * all[(p * 115) + 1 + 6 + (t * 18) + (r * 3) + f];
                        }
                    }
                }

                Assert.Equal(vega, means[(p * 9) + 7 + b]);
                Assert.Equal(0.0, errors[(p * 9) + 7 + b]);
            }
        }
    }

    [Fact]
    public void TheSwaptionPseudoDerivativeRefusesWhatSwaptionImpliedVolatilityRefuses()
    {
        // steps straddling the expiry: the evolution doesn't stop at rate time 3, 2.0
        var straddling = new EvolutionDescription(Market.RateTimes, [0.5, 1.25, 2.25, 2.75]);
        MarketModel model = Market.FlatVol(straddling, 3);
        Assert.Equal(
            "skipping 4th rate time (Parameter 'volStructure')",
            Assert.Throws<ArgumentException>(() => new SwaptionPseudoDerivative(model, 3, 6)).Message);

        // unequal displacements
        Matrix corr = ExpCorrelationsFunctions.ExponentialCorrelations(Market.RateTimes, 0.5, 0.2);
        var unequal = new FlatVol(
            [.. Enumerable.Repeat(0.20, 6)], new TimeHomogeneousForwardCorrelation(corr, Market.RateTimes), Evolution, 3, Market.Forwards,
            [0.0, 0.0, 0.01, 0.0, 0.0, 0.0]);
        Assert.Equal(
            "3rd displacement (0.01) not equal to the previous ones (0) (Parameter 'volStructure')",
            Assert.Throws<ArgumentException>(() => new SwaptionPseudoDerivative(unequal, 1, 6)).Message);

        // the twin: a grid holding the expiry, QuantLib's value, swaptionImpliedVolatility's
        MarketModel onGrid = Market.FlatVol(Evolution, 3);
        var derivative = new SwaptionPseudoDerivative(onGrid, 3, 6);
        Assert.Equal(SwapForwardMappings.SwaptionImpliedVolatility(onGrid, 3, 6), derivative.ImpliedVolatility, 1e-15);
        Assert.Throws<ArgumentOutOfRangeException>(() => derivative.VolatilityDerivative(6));
    }

    [Fact]
    public void TheBumpsAndThePseudoDerivativesAreSizedAsTheyAreRead()
    {
        MarketModel model = Market.FlatVol(Evolution, 3);
        Assert.Throws<ArgumentException>(() => new VegaBumpCluster(0, 0, 0, 1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VegaBumpCluster(-1, 1, 0, 1, 0, 1));
        Assert.Equal(
            "incompatible bumps passed to VegaBumpCollection (Parameter 'allBumps')",
            Assert.Throws<ArgumentException>(() => new VegaBumpCollection([new VegaBumpCluster(0, 4, 0, 1, 0, 1)], model)).Message);
        Assert.Equal(
            "for a cap pseudo derivative the start of the cap must be before the end (Parameter 'startIndex')",
            Assert.Throws<ArgumentException>(() => new CapPseudoDerivative(model, 0.03, 3, 3, 0.95)).Message);
        Assert.Equal(
            "for a cap pseudo derivative the end of the cap must before the end of the rates (Parameter 'endIndex')",
            Assert.Throws<ArgumentException>(() => new CapPseudoDerivative(model, 0.03, 3, 7, 0.95)).Message);
        var jacobian = new VolatilityBumpInstrumentJacobian(new VegaBumpCollection(model), [new VolatilityBumpInstrumentJacobianSwaption(1, 6)], []);
        Assert.Throws<ArgumentOutOfRangeException>(() => jacobian.DerivativesVolatility(1));
        Assert.Equal(jacobian.DerivativesVolatility(0), jacobian.DerivativesVolatility(0));
    }

    [Fact]
    public void TheProxyEngineAndTheConstrainedEvolverRefuseWhatTheyReadPast()
    {
        MarketModel model = Market.FlatVol(Evolution, 3);
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketMeasure(Evolution)];
        var factory = new MTBrownianGeneratorFactory(42);
        var product = new MultiStepOptionlets(
            Market.RateTimes, Market.Accruals, Market.PaymentTimes, [.. Market.Forwards.Select(f => (Payoff)new PlainVanillaPayoff(OptionType.Call, f))]);
        var e = new LogNormalFwdRateEulerConstrained(model, model, factory, numeraires);
        int[] start = [0, 1, 2, 3, 4, 5], end = [1, 2, 3, 4, 5, 6];

        var original = new LogNormalFwdRateEuler(model, factory, numeraires);
        Assert.Equal(
            "2 groups of weights given, more than the 1 groups of evolvers (Parameter 'diffWeights')",
            Assert.Throws<ArgumentException>(() => new ProxyGreekEngine(original, [[e]], [[[0.0, 1.0]], [[0.0]]], start, end, product, 0.95)).Message);
        Assert.Equal(
            "group 0 has a list of 3 weights, not 1 to its 1 evolvers plus 1 (Parameter 'diffWeights')",
            Assert.Throws<ArgumentException>(() => new ProxyGreekEngine(original, [[e]], [[[0.0, 1.0, 1.0]]], start, end, product, 0.95)).Message);
        Assert.Equal(
            "the constraints' 5 starts and 6 ends are fewer than the product's 6 steps (Parameter 'startIndexOfConstraint')",
            Assert.Throws<ArgumentException>(() => new ProxyGreekEngine(original, [[e]], [[[0.0, 1.0]]], start[..5], end, product, 0.95)).Message);
        var engine = new ProxyGreekEngine(original, [[e]], [[[0.0, 1.0]]], start, end, product, 0.95);
        Assert.Equal(
            "the statistics are fewer than the weights (Parameter 'modifiedStats')",
            Assert.Throws<ArgumentException>(() => engine.MultiplePathValues(new SequenceStatisticsInc(6), [[]], 1)).Message);

        // the reference model: the model's rates and steps
        var coarse = new EvolutionDescription(Market.RateTimes, [0.5, 1.5, 2.5]);
        Assert.Equal(
            "the reference model's 6 rates and 3 steps aren't the model's 6 and 6 (Parameter 'referenceModel')",
            Assert.Throws<ArgumentException>(() => new LogNormalFwdRateEulerConstrained(model, Market.FlatVol(coarse, 3), factory, numeraires)).Message);
    }
}
