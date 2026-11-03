// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The call-specified product's end. QuantLib's ends with its underlying (<c>callspecifiedmultiproduct.cpp:167</c>), so
/// its tested Bermudan, a call-specified product on <c>MultiStepNothing</c> done at its first step, ends at once alone
/// and is worth its value only inside a composite that steps a finished part again: on six semi-annual rates, 8,191
/// Sobol paths, <c>testCallableSwapNaif</c>'s Bermudan 0.0069062 in QuantLib's composite and 0.0053606 alone. nanoql
/// keeps QuantLib's end but on an underlying with no possible cash-flow times, which can't end it while an exercise
/// time remains, stops stepping a finished underlying, and, once ended with its underlying, stays ended, with no call
/// and no cash flow. Checks: QuantLib's composite values, bit for bit, alone and inside; QuantLib's callable identity.
/// </summary>
public class CallSpecifiedEndTests
{
    private const double K = 0.032;
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void ABermudanOnNothingIsWorthItsValueAloneAndInside()
    {
        double[] exerciseTimes = Market.RateTimes[..^1];
        var trigger = new SwapRateTrigger(Market.RateTimes, [.. Enumerable.Repeat(K, 6)], exerciseTimes);
        var nullValue = new NothingExerciseValue(Market.RateTimes);
        var receiver = new MultiStepSwap(Market.RateTimes, Market.Accruals, Market.Accruals, Market.PaymentTimes, K, false);
        var payer = new MultiStepSwap(Market.RateTimes, Market.Accruals, Market.Accruals, Market.PaymentTimes, K, true);
        EvolutionDescription evolution = new CallSpecifiedMultiProduct(receiver, trigger, new ExerciseAdapter(nullValue)).Evolution;
        var bermudan = new CallSpecifiedMultiProduct(new MultiStepNothing(evolution), trigger, payer);
        var callable = new CallSpecifiedMultiProduct(receiver, trigger, new ExerciseAdapter(nullValue));
        var all = new MultiProductComposite();
        all.Add(payer);
        all.Add(receiver);
        all.Add(bermudan);
        all.Add(callable);
        all.FinalizeComposite();

        ImmutableArray<double> inside = Simulate(all, evolution);
        ImmutableArray<double> alone = Simulate(bermudan, evolution);

        // QuantLib's composite, bit for bit, and the Bermudan alone worth the same
        double[] quantLibComposite =
            [0.0012122600992575947, -0.0012122600992575947, 0.0069061585800138043, 0.0056938984807562276];
        Assert.Equal(quantLibComposite, inside);
        Assert.Equal(quantLibComposite[2], alone[0]);
        Assert.True(Math.Abs(inside[1] + inside[2] - inside[3]) <= 1.1e-15);

        // QuantLib's Bermudan alone, ended at its first step
        const double quantLibAlone = 0.0053605551359631248;
        Assert.True(alone[0] - quantLibAlone > 1e-3);
    }

    [Fact]
    public void ACallableEndedWithItsUnderlyingStaysEnded()
    {
        // a TARN whose first coupon, (8% - 3%) x 0.5, passes its total of 2%: it ends at its first step
        double[] strikes = [.. Enumerable.Repeat(0.08, 6)];
        var tarn = new MultiStepTarn(
            Market.RateTimes, Market.Accruals, Market.Accruals, Market.PaymentTimes, Market.PaymentTimes, 0.02,
            strikes, [.. Enumerable.Repeat(1.0, 6)], new double[6]);
        // a strategy that calls at every exercise from the second rate time on, after the TARN's end
        var always = new SwapRateTrigger(Market.RateTimes, [.. Enumerable.Repeat(0.0, 5)], Market.RateTimes[1..^1]);
        var callable = new CallSpecifiedMultiProduct(tarn, always);
        var state = new LMMCurveState(Market.RateTimes);
        state.SetOnForwardRates(Market.Forwards);
        int[] counts = new int[1];
        MarketModelMultiProductCashFlow[][] flows = [new MarketModelMultiProductCashFlow[callable.MaxNumberOfCashFlowsPerProductPerStep]];

        callable.Reset();
        Assert.True(callable.NextTimeStep(state, counts, flows));
        Assert.Equal(2, counts[0]);

        // stepped again, as the upper-bound engine's replay steps it: no call and no cash flow, still ended; QuantLib's
        // steps its TARN past the TARN's end, and its strategy calls it into its rebate
        for (int k = 1; k < 4; ++k)
        {
            counts[0] = 7;
            Assert.True(callable.NextTimeStep(state, counts, flows));
            Assert.Equal(0, counts[0]);
        }
    }

    private static ImmutableArray<double> Simulate(MarketModelMultiProduct product, EvolutionDescription evolution)
    {
        int[] numeraires = [.. EvolutionDescriptionFunctions.MoneyMarketPlusMeasure(evolution, 1)];
        var evolver = new LogNormalFwdRatePc(
            Market.FlatVol(evolution, 3), new SobolBrownianGeneratorFactory(SobolBrownianGeneratorBaseOrdering.Diagonal, 42), numeraires);
        (ImmutableArray<double> means, _) = MarketModelProductMarket.Simulate(evolver, product, Market.Discounts[numeraires[0]], 8191);
        return means;
    }
}
