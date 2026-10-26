// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Stulz's engine at a negative strike. QuantLib's <c>log(forward1/strike)</c> (<c>stulzengine.cpp:62</c>) makes the
/// minimum NaN and Black's check refuses the maximum ("strike + displacement (-10 + 0) must be non-negative"), where
/// both payoffs are linear: the call is the call struck at zero less the discounted strike, the put worth nothing
/// (96.0373 and 117.9873 at K = −10 on the plan's market, the Monte Carlo engine 96.067 and 118.007). nanoql prices
/// those forms (the S-exo plan's ER5 (xxxiii)): QuantLib's own K = 0 values less K·D, and the Monte Carlo engine, are
/// the checks.
/// </summary>
public class StulzNegativeStrikeTests
{
    private static readonly GeneralizedBlackScholesProcess P1 = Process(100.0, 0.02, 0.05, 0.25);
    private static readonly GeneralizedBlackScholesProcess P2 = Process(95.0, 0.03, 0.05, 0.3);
    private static readonly double Df = Flat(0.05).Discount(Today + 365);

    [Fact]
    public void ACallIsTheZeroStrikeCallLessTheDiscountedStrike()
    {
        foreach (Func<Payoff, BasketPayoff> basket in new Func<Payoff, BasketPayoff>[] { p => new MinBasketPayoff(p), p => new MaxBasketPayoff(p) })
        {
            double atZero = Stulz(basket(new PlainVanillaPayoff(OptionType.Call, 0.0)));
            BasketPayoff payoff = basket(new PlainVanillaPayoff(OptionType.Call, -10.0));
            double calculated = Stulz(payoff);
            Assert.Equal(atZero + (10.0 * Df), calculated, 1e-12);

            // the Monte Carlo engine beside it, within four standard errors
            MultiAssetOptionResults mc = new MakeMCEuropeanBasketEngine<PseudoRandom, GeneralStatistics>(
                    new StochasticProcessArray([P1, P2], Rho(0.5)), 42)
                .WithSteps(1)
                .WithSamples(100000)
                .ToEngine()(new BasketOption(payoff, European()), Context);
            Assert.Equal(mc.NPV!.Value, calculated, 4 * mc.ErrorEstimate!.Value);
        }
    }

    [Fact]
    public void APutIsWorthNothing()
    {
        Assert.Equal(0.0, Stulz(new MinBasketPayoff(new PlainVanillaPayoff(OptionType.Put, -10.0))));
        Assert.Equal(0.0, Stulz(new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Put, -10.0))));
    }

    private static double Stulz(BasketPayoff payoff) =>
        StulzEngine.Calculate(new BasketOption(payoff, European()), Context, P1, P2, 0.5).NPV!.Value;
}
