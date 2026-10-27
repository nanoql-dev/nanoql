// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The market-model product families of the S-mmp plan's second commit: the refusals of inputs QuantLib reads past
/// (MMPR4's refusals) beside QuantLib's own checks; the one-step products against their multi-step twins and the
/// deflated caplets against the undeflated through the pathwise wrapper (two routes to one value); and QuantLib's
/// <c>testPeriodAdapter</c> on quarterly rates in periods of 4 and annual rates in periods of 2 (S-mm's MR3 (i)).
/// </summary>
public class MarketModelProductFamilyTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void TheMultiStepProductsInputsAreSizedAsTheyAreRead()
    {
        double[] rt = Market.RateTimes, a = Market.Accruals, pt = Market.PaymentTimes;
        StrikedTypePayoff call = new PlainVanillaPayoff(OptionType.Call, 0.035);
        StrikedTypePayoff[] calls = [.. Enumerable.Repeat(call, 6)];
        Assert.Equal(
            "Incorrect number of fixedAccruals given, should be at least 6 not 5 (Parameter 'fixedAccruals')",
            Assert.Throws<ArgumentException>(() => new MultiStepSwap(rt, a[..5], a, pt, 0.03)).Message);
        Assert.Equal(
            "Incorrect number of floatingAccruals given, should be at least 6 not 4 (Parameter 'floatingAccruals')",
            Assert.Throws<ArgumentException>(() => new MultiStepCoterminalSwaps(rt, a, a[..4], pt, 0.03)).Message);
        Assert.Equal(
            "Incorrect number of paymentTimes given, should be at least 6 not 5 (Parameter 'paymentTimes')",
            Assert.Throws<ArgumentException>(() => new MultiStepCoinitialSwaps(rt, a, a, pt[..5], 0.03)).Message);
        Assert.Equal(
            "Incorrect number of payoffs given, should be at least 6 not 5 (Parameter 'payoffs')",
            Assert.Throws<ArgumentException>(() => new MultiStepCoterminalSwaptions(rt, rt[..^1], calls[..5])).Message);
        Assert.Equal(
            " start index must be before end index (Parameter 'startIndex')",
            Assert.Throws<ArgumentException>(() => new MultiStepSwaption(rt, 3, 3, call)).Message);
        Assert.Equal(
            "end index be before the end of the rates. (Parameter 'endIndex')",
            Assert.Throws<ArgumentException>(() => new MultiStepSwaption(rt, 2, 7, call)).Message);
        double[] six = [.. Enumerable.Repeat(0.15, 6)];
        Assert.Equal(
            " Incorrect number of fixedStrikes given, should be 6 not 5 (Parameter 'fixedStrikes')",
            Assert.Throws<ArgumentException>(() => new MultiStepInverseFloater(rt, a, a, six[..5], six, six, pt)).Message);
        Assert.Equal(
            "Incorrect number of accruals given, should be at least 6 not 5 (Parameter 'accruals')",
            Assert.Throws<ArgumentException>(() => new MultiStepRatchet(rt, a[..5], pt, 1.0, 1.0, 0.0, 0.0, 0.03)).Message);
        Assert.Equal(
            "missized strikes in MultiStepTARN (Parameter 'strikes')",
            Assert.Throws<ArgumentException>(() => new MultiStepTarn(rt, a, a, pt, pt, 0.1, six[..5], six, new double[6])).Message);
        Assert.Equal(
            "Incorrect number of multipliers given, should be at least 6 not 4 (Parameter 'multipliers')",
            Assert.Throws<ArgumentException>(() => new MultiStepTarn(rt, a, a, pt, pt, 0.1, six, six[..4], new double[6])).Message);
        Assert.Equal(
            "the offset must be less then the period in MultiStepPeriodCapletSwaptions  (Parameter 'offset')",
            Assert.Throws<ArgumentException>(
                () => new MultiStepPeriodCapletSwaptions(rt, [1.5], [0.5], [call], [call], 2, 2)).Message);
        Assert.Throws<ArgumentNullException>(() => new MultiStepSwaption(rt, 1, 3, null!));
        Assert.Throws<ArgumentNullException>(() => new MultiProductPathwiseWrapper(null!));

        // more multipliers than rates: QuantLib reads the first ones
        Assert.Equal(12, new MultiStepTarn(rt, a, a, pt, pt, 0.1, six, [.. six, 0.15], new double[6]).PossibleCashFlowTimes().Length);
    }

    [Fact]
    public void TheOneStepAndPathwiseProductsInputsAreSizedAsTheyAreRead()
    {
        double[] rt = Market.RateTimes, a = Market.Accruals, pt = Market.PaymentTimes, k = Market.Forwards;
        Assert.Equal(
            "Rate times must contain at least two values (Parameter 'rateTimes')",
            Assert.Throws<ArgumentException>(() => new OneStepForwards([0.5], a, pt, k)).Message);
        Assert.Equal(
            "Incorrect number of strikes given, should be at most 6 not 7 (Parameter 'strikes')",
            Assert.Throws<ArgumentException>(() => new OneStepForwards(rt, [.. a, 0.5], [.. pt, 4.0], [.. k, 0.04])).Message);
        Assert.Equal(
            "Incorrect number of payoffs given, should be at most 6 not 7 (Parameter 'payoffs')",
            Assert.Throws<ArgumentException>(
                () => new OneStepOptionlets(rt, [.. a, 0.5], [.. pt, 4.0], [.. Enumerable.Repeat<Payoff>(new PlainVanillaPayoff(OptionType.Call, 0.03), 7)])).Message);

        // an empty one-step product, which QuantLib completes: no product, a step that pays nothing and ends it
        MarketModelMultiProduct[] empty = [new OneStepForwards(rt, a, pt, []), new OneStepOptionlets(rt, a, pt, [])];
        foreach (MarketModelMultiProduct p in empty)
        {
            Assert.Equal(0, p.NumberOfProducts);
            var state = new LMMCurveState(rt);
            state.SetOnForwardRates(k);
            p.Reset();
            Assert.True(p.NextTimeStep(state, [], []));
        }
        Payoff[] payoffs = [.. k.Select(f => (Payoff)new PlainVanillaPayoff(OptionType.Call, f))];
        Assert.Equal(
            "Incorrect number of accruals given, should be at least 6 not 2 (Parameter 'accruals')",
            Assert.Throws<ArgumentException>(() => new OneStepOptionlets(rt, a[..2], pt, payoffs)).Message);
        Assert.Equal(
            "Incorrect number of fixedAccruals given, should be at least 6 not 5 (Parameter 'fixedAccruals')",
            Assert.Throws<ArgumentException>(() => new OneStepCoinitialSwaps(rt, a[..5], a, pt, 0.03)).Message);
        Assert.Equal(
            "Incorrect number of paymentTimes given, should be at least 6 not 5 (Parameter 'paymentTimes')",
            Assert.Throws<ArgumentException>(() => new OneStepCoterminalSwaps(rt, a, a, pt[..5], 0.03)).Message);

        Assert.Equal(
            "strikes.size()<> numberOfRates (Parameter 'strikes')",
            Assert.Throws<ArgumentException>(() => new MarketModelPathwiseMultiCaplet(rt, a, pt, k[..5])).Message);
        Assert.Equal(
            "paymentTimes.size()<> numberOfRates (Parameter 'paymentTimes')",
            Assert.Throws<ArgumentException>(() => new MarketModelPathwiseMultiDeflatedCaplet(rt, a, pt[..5], 0.03)).Message);
        Assert.Equal(
            "a cap must start before it ends: 022 (Parameter 'startsAndEnds')",
            Assert.Throws<ArgumentException>(() => new MarketModelPathwiseMultiDeflatedCap(rt, a, pt, 0.03, [(2, 2)])).Message);
        Assert.Equal(
            "a cap must end when the underlying caplets: 107 (Parameter 'startsAndEnds')",
            Assert.Throws<ArgumentException>(() => new MarketModelPathwiseMultiDeflatedCap(rt, a, pt, 0.03, [(0, 6), (0, 7)])).Message);
        Assert.Equal(
            "strikes.size() does not equal numberOfRates or 1 (Parameter 'strikes')",
            Assert.Throws<ArgumentException>(() => new MarketModelPathwiseSwap(rt, a, k[..2])).Message);
        double[] six = [.. Enumerable.Repeat(0.15, 6)];
        Assert.Equal(
            " Incorrect number of floatingSpreads given, should be 6 not 5 (Parameter 'floatingSpreads')",
            Assert.Throws<ArgumentException>(() => new MarketModelPathwiseInverseFloater(rt, a, a, six, six, six[..5], pt)).Message);
        Assert.Equal(
            "strikes.size()<> numberOfRates (Parameter 'strikes')",
            Assert.Throws<ArgumentException>(() => new MarketModelPathwiseCoterminalSwaptionsDeflated(rt, k[..5])).Message);
        Assert.Equal(
            "strikes.size()<> numberOfRates (Parameter 'strikes')",
            Assert.Throws<ArgumentException>(() => new MarketModelPathwiseCoterminalSwaptionsNumericalDeflated(rt, k[..5], 1e-6)).Message);
        var evolution = new EvolutionDescription(rt, [1.0, 2.0]);
        Assert.Equal(
            "the number of rows in the matrix must equal the number of products (Parameter 'amounts')",
            Assert.Throws<ArgumentException>(() => new MarketModelPathwiseCashRebate(evolution, [1.0, 2.0], new Matrix(1, 2), 2)).Message);
    }

    [Fact]
    public void TheOneStepProductsAreTheirMultiStepTwins()
    {
        double[] rt = Market.RateTimes, a = Market.Accruals, pt = Market.PaymentTimes, k = Market.Forwards;
        Payoff[] payoffs = [.. k.Select(f => (Payoff)new PlainVanillaPayoff(OptionType.Call, f))];
        (MarketModelMultiProduct One, MarketModelMultiProduct Multi)[] twins =
        [
            (new OneStepForwards(rt, a, pt, [.. k.Select(f => f + 0.005)]), new MultiStepForwards(rt, a, pt, [.. k.Select(f => f + 0.005)])),
            (new OneStepOptionlets(rt, a, pt, payoffs), new MultiStepOptionlets(rt, a, pt, payoffs)),
            (new OneStepCoinitialSwaps(rt, a, a, pt, 0.032), new MultiStepCoinitialSwaps(rt, a, a, pt, 0.032)),
            (new OneStepCoterminalSwaps(rt, a, a, pt, 0.032), new MultiStepCoterminalSwaps(rt, a, a, pt, 0.032)),
        ];
        foreach ((MarketModelMultiProduct one, MarketModelMultiProduct multi) in twins)
        {
            Assert.Single(one.Evolution.EvolutionTimes);
            (ImmutableArray<double> v1, ImmutableArray<double> e1) = Market.TerminalPc(one, 6, 4095);
            (ImmutableArray<double> v2, ImmutableArray<double> e2) = Market.TerminalPc(multi, 6, 4095);
            for (int i = 0; i < v1.Length; ++i)
            {
                double error = Math.Sqrt((e1[i] * e1[i]) + (e2[i] * e2[i]));
                Assert.True(Math.Abs(v1[i] - v2[i]) < 4 * error, $"{one.GetType().Name} {i}: {v1[i]} against {v2[i]} ± {error}");
            }
        }
    }

    [Fact]
    public void TheDeflatedAndUndeflatedCapletsAgreeThroughTheWrapper()
    {
        var undeflated = new MarketModelPathwiseMultiCaplet(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        var deflated = new MarketModelPathwiseMultiDeflatedCaplet(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        (ImmutableArray<double> u, _) = Market.TerminalPc(new MultiProductPathwiseWrapper(undeflated), 3, 1023);
        (ImmutableArray<double> d, _) = Market.TerminalPc(new MultiProductPathwiseWrapper(deflated), 3, 1023);
        for (int i = 0; i < Market.N; ++i)
        {
            Assert.Equal(u[i], d[i], 1e-15 * u[i]);
        }
    }

    // QuantLib's testPeriodAdapter (marketmodel.cpp) on other grids: five years of quarterly rates in periods of 4 and
    // ten years of annual rates in periods of 2, an abcd model of flat 20% volatility on 5 factors, Sobol paths;
    // Black's approximations on the adapted models' variances within QuantLib's 4 standard errors.
    [Theory]
    [InlineData(0.25, 20, 4)]
    [InlineData(1.0, 10, 2)]
    public void ThePeriodAdapterHoldsOnOtherGrids(double tau, int n, int period)
    {
        var market = new MarketModelProductMarket(n, t0: tau, tau: tau);
        var cs = new LMMCurveState(market.RateTimes);
        cs.SetOnForwardRates(market.Forwards);
        const int offset = 0;
        LMMCurveState big = ForwardForwardMappings.RestrictCurveState(cs, period, offset);
        double[] paymentTimes = big.RateTimes.AsSpan()[..^1].ToArray();
        int m = big.NumberOfRates;
        var optionletPayoffs = new StrikedTypePayoff[m];
        var swaptionPayoffs = new StrikedTypePayoff[m];
        for (int i = 0; i < m; ++i)
        {
            optionletPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, big.ForwardRate(i));
            swaptionPayoffs[i] = new PlainVanillaPayoff(OptionType.Call, big.CoterminalSwapRate(i));
        }

        var product = new MultiStepPeriodCapletSwaptions(
            market.RateTimes, paymentTimes, paymentTimes, optionletPayoffs, swaptionPayoffs, period, offset);
        EvolutionDescription evolution = product.Evolution;
        Matrix correlations = ExpCorrelationsFunctions.ExponentialCorrelations(evolution.RateTimes.AsSpan(), 0.5, 0.2);
        var corr = new TimeHomogeneousForwardCorrelation(correlations, evolution.RateTimes.AsSpan());
        MarketModel original = new AbcdVol(
            0.0, 0.0, 1.0, 1.0, [.. Enumerable.Repeat(0.20, n)], corr, evolution, 5, market.Forwards, new double[n]);
        var adaptedForwards = new FwdPeriodAdapter(original, period, offset, []);
        var adaptedSwaps = new FwdToCotSwapAdapter(adaptedForwards);
        Matrix forwardCovariances = adaptedForwards.TotalCovariance(adaptedForwards.NumberOfSteps - 1);
        Matrix swapCovariances = adaptedSwaps.TotalCovariance(adaptedSwaps.NumberOfSteps - 1);

        int[] numeraires = [.. product.SuggestedNumeraires()];
        var evolver = new LogNormalFwdRatePc(
            original, new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 42), numeraires);
        (ImmutableArray<double> results, ImmutableArray<double> errors) =
            MarketModelProductMarket.Simulate(evolver, product, market.Discounts[numeraires[0]], 8191);
        Assert.Equal(2 * m, results.Length);

        for (int j = 0; j < m; ++j)
        {
            double capletAnnuity = market.Discounts[0] * big.DiscountRatio(j + 1, 0) * big.RateTaus[j];
            double caplet = new BlackCalculator(
                optionletPayoffs[j], big.ForwardRate(j), Math.Sqrt(forwardCovariances[j, j]), capletAnnuity).Value;
            double swaptionAnnuity = market.Discounts[0] * big.CoterminalSwapAnnuity(0, j);
            double swaption = new BlackCalculator(
                swaptionPayoffs[j], big.CoterminalSwapRate(j), Math.Sqrt(swapCovariances[j, j]), swaptionAnnuity).Value;
            Assert.True(Math.Abs(results[j] - caplet) < 4 * errors[j], $"caplet {j}: {results[j]} against {caplet} ± {errors[j]}");
            Assert.True(
                Math.Abs(results[j + m] - swaption) < 4 * errors[j + m],
                $"swaption {j}: {results[j + m]} against {swaption} ± {errors[j + m]}");
        }
    }
}
