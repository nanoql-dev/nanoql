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
/// Stulz's, both Margrabe engines and the two-asset correlation engine on a second risk-free curve. QuantLib reads the
/// second process's risk-free curve three ways: Kirk's family and the Monte Carlo basket engines forward the second
/// asset on its own curves and discount on the first's (<c>spreadblackscholesvanillaengine.cpp:54-68</c>,
/// <c>mceuropeanbasketengine.hpp:196</c>); Stulz and the European Margrabe engine build the second forward on the first
/// process's discount (<c>stulzengine.cpp:140-152</c>, <c>analyticeuropeanmargrabeengine.cpp:61-72</c>), the two-asset
/// correlation engine takes its rate from the first process alone (<c>:64-66</c>), and the American Margrabe engine
/// reads no risk-free rate (<c>analyticamericanmargrabeengine.cpp:69-79</c>); at r₁ 5%, r₂ 10% on the test's market
/// Stulz's minimum call is −94 standard errors from the Monte Carlo engine and Margrabe's exchange +134. nanoql reads
/// Kirk's way in each, the American Margrabe engine's transformed rate the second asset's yield on the first curve (q₂
/// + r₁ − r₂), and the European Margrabe theta the second asset's carry there: each engine on the market restated onto
/// the first process's curve, the second asset's yield q₂ + r₁ − r₂ for the same forward, is the check, the European
/// Margrabe theta's among them, with the Monte Carlo basket engines beside.
/// </summary>
public class TwoAssetTwoRiskFreeCurvesTests
{
    private const double Tol = 1e-10;

    // r1 5%, r2 10%; restated, the second asset's yield 0 + 5% - 10% on the first curve
    private static readonly GeneralizedBlackScholesProcess P1 = Process(100.0, 0.0, 0.05, 0.3);
    private static readonly GeneralizedBlackScholesProcess P2 = Process(100.0, 0.0, 0.10, 0.2);
    private static readonly GeneralizedBlackScholesProcess P2Restated = Process(100.0, -0.05, P1.RiskFreeRate, 0.2);

    [Fact]
    public void StulzPricesTheRestatedMarket()
    {
        foreach (BasketPayoff payoff in new BasketPayoff[]
        {
            new MinBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0)),
            new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Put, 100.0)),
        })
        {
            var option = new BasketOption(payoff, European());
            double twoCurves = StulzEngine.Calculate(option, Context, P1, P2, 0.5).NPV!.Value;
            double restated = StulzEngine.Calculate(option, Context, P1, P2Restated, 0.5).NPV!.Value;
            Assert.Equal(restated, twoCurves, Tol * restated);
        }

        // the Monte Carlo basket engine beside it, within four standard errors
        var minCall = new BasketOption(new MinBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0)), European());
        MultiAssetOptionResults mc = new MakeMCEuropeanBasketEngine<PseudoRandom, GeneralStatistics>(
                new StochasticProcessArray([P1, P2], Rho(0.5)), 42)
            .WithSteps(1)
            .WithSamples(100000)
            .ToEngine()(minCall, Context);
        Assert.Equal(mc.NPV!.Value, StulzEngine.Calculate(minCall, Context, P1, P2, 0.5).NPV!.Value, 4 * mc.ErrorEstimate!.Value);
    }

    [Fact]
    public void TheEuropeanMargrabeEnginePricesTheRestatedMarket()
    {
        var option = new MargrabeOption(1, 1, European());
        MargrabeOptionResults twoCurves = AnalyticEuropeanMargrabeEngine.Calculate(option, Context, P1, P2, 0.5);
        MargrabeOptionResults restated = AnalyticEuropeanMargrabeEngine.Calculate(option, Context, P1, P2Restated, 0.5);
        Assert.Equal(restated.NPV!.Value, twoCurves.NPV!.Value, Tol * restated.NPV!.Value);
        Assert.Equal(restated.Delta1!.Value, twoCurves.Delta1!.Value, Tol);
        Assert.Equal(restated.Delta2!.Value, twoCurves.Delta2!.Value, Tol);
        Assert.Equal(restated.Gamma1!.Value, twoCurves.Gamma1!.Value, Tol);
        Assert.Equal(restated.Gamma2!.Value, twoCurves.Gamma2!.Value, Tol);
    }

    [Fact]
    public void TheEuropeanMargrabeThetaIsTheRestatedMarkets()
    {
        var option = new MargrabeOption(1, 1, European());
        double twoCurves = AnalyticEuropeanMargrabeEngine.Calculate(option, Context, P1, P2, 0.5).Theta!.Value;
        double restated = AnalyticEuropeanMargrabeEngine.Calculate(option, Context, P1, P2Restated, 0.5).Theta!.Value;
        Assert.Equal(restated, twoCurves, 1e-8);
    }

    [Fact]
    public void TheAmericanMargrabeEnginePricesTheRestatedMarket()
    {
        var option = new MargrabeOption(1, 1, new AmericanExercise(Today, Today + 365));
        double twoCurves = AnalyticAmericanMargrabeEngine.Calculate(option, Context, P1, P2, 0.5).NPV!.Value;
        double restated = AnalyticAmericanMargrabeEngine.Calculate(option, Context, P1, P2Restated, 0.5).NPV!.Value;
        Assert.Equal(restated, twoCurves, Tol * restated);
    }

    [Fact]
    public void TheTwoAssetCorrelationEnginePricesTheRestatedMarket()
    {
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            var option = new TwoAssetCorrelationOption(type, 95.0, 105.0, European());
            double twoCurves = AnalyticTwoAssetCorrelationEngine.Calculate(option, Context, P1, P2, 0.5).NPV!.Value;
            double restated = AnalyticTwoAssetCorrelationEngine.Calculate(option, Context, P1, P2Restated, 0.5).NPV!.Value;
            Assert.Equal(restated, twoCurves, Tol * restated);
        }
    }
}
