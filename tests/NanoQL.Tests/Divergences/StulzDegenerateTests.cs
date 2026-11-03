// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using static NanoQL.PricingEngines.BlackFormulaFunctions;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Stulz's engine where the assets move together or one doesn't move. At a correlation of 1 with equal volatilities, or
/// at two zero volatilities, its combined variance is 0 or a rounding negative, and QuantLib's derived correlations are
/// NaN or infinite, refused by the bivariate normal ("rho must be >= -1.0 (-nan(ind) not allowed)",
/// <c>stulzengine.cpp:44-48</c>); at one zero volatility its bivariate normal reads an infinite bound and returns NaN
/// (We04DP's, <c>Divergences/BivariateInfiniteArgumentTests.cs</c>). nanoql prices the co-moving limit, the minimum the
/// asset of the smaller forward and the maximum that of the larger by Black's formula, and the bivariate normal's limit
/// at an infinite bound: QuantLib's <c>blackFormula</c> (15.7766093, between Stulz's minimum 15.7766090 and maximum
/// 15.7766097 at ρ = 1 − 1e-15 on the test's market), QuantLib's own value near the limit, and the deterministic value
/// are the checks.
/// </summary>
public class StulzDegenerateTests
{
    private static readonly double Df = Flat(0.05).Discount(Today + 365);

    [Fact]
    public void AssetsMovingTogetherPriceBlacksFormula()
    {
        var p1 = Process(100.0, 0.0, 0.05, 0.25);
        var p2 = Process(105.0, 0.0, 0.05, 0.25);
        double f1 = 100.0 / Df, f2 = 105.0 / Df;
        double sd = 0.25;
        double min = Stulz(MinCall(100.0), p1, p2, 1.0);
        double max = Stulz(MaxCall(100.0), p1, p2, 1.0);
        Assert.Equal(BlackFormula(OptionType.Call, 100.0, f1, sd) * Df, min, 1e-12);
        Assert.Equal(BlackFormula(OptionType.Call, 100.0, f2, sd) * Df, max, 1e-12);

        // QuantLib's own value just inside the limit
        Assert.Equal(Stulz(MinCall(100.0), p1, p2, 1.0 - 1e-12), min, 1e-5);
        Assert.Equal(Stulz(MaxCall(100.0), p1, p2, 1.0 - 1e-12), max, 1e-5);
    }

    [Fact]
    public void OneZeroVolatilityPricesItsLimit()
    {
        var p2 = Process(105.0, 0.0, 0.05, 0.3);
        foreach (BasketOption option in new[] { MinCall(100.0), MaxCall(100.0), MinPut(100.0), MaxPut(100.0) })
        {
            double zero = Stulz(option, Process(100.0, 0.0, 0.05, 0.0), p2, 0.5);
            double tiny = Stulz(option, Process(100.0, 0.0, 0.05, 1e-8), p2, 0.5);
            Assert.False(double.IsNaN(zero));
            Assert.Equal(tiny, zero, 1e-6);
        }

        // at the money on zero rates, the first forward exactly the strike: d's numerator is 0 with its deviation
        var atTheMoney = Process(105.0, 0.0, 0.0, 0.3);
        foreach (BasketOption option in new[] { MinCall(100.0), MaxCall(100.0), MinPut(100.0), MaxPut(100.0) })
        {
            double zero = Stulz(option, Process(100.0, 0.0, 0.0, 0.0), atTheMoney, 0.5);
            double tiny = Stulz(option, Process(100.0, 0.0, 0.0, 1e-8), atTheMoney, 0.5);
            Assert.False(double.IsNaN(zero));
            Assert.Equal(tiny, zero, 1e-6);
        }
    }

    [Fact]
    public void TwoZeroVolatilitiesPriceTheDeterministicValue()
    {
        var p1 = Process(100.0, 0.0, 0.05, 0.0);
        var p2 = Process(105.0, 0.0, 0.05, 0.0);
        double f1 = 100.0 / Df, f2 = 105.0 / Df;
        Assert.Equal(Df * Math.Max(Math.Min(f1, f2) - 103.0, 0.0), Stulz(MinCall(103.0), p1, p2, 0.5), 1e-12);
        Assert.Equal(Df * Math.Max(Math.Max(f1, f2) - 103.0, 0.0), Stulz(MaxCall(103.0), p1, p2, 0.5), 1e-12);
    }

    private static double Stulz(
        BasketOption option, Processes.GeneralizedBlackScholesProcess p1, Processes.GeneralizedBlackScholesProcess p2, double rho) =>
        StulzEngine.Calculate(option, Context, p1, p2, rho).NPV!.Value;

    private static BasketOption MinCall(Real k) => new(new MinBasketPayoff(new PlainVanillaPayoff(OptionType.Call, k)), European());

    private static BasketOption MaxCall(Real k) => new(new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Call, k)), European());

    private static BasketOption MinPut(Real k) => new(new MinBasketPayoff(new PlainVanillaPayoff(OptionType.Put, k)), European());

    private static BasketOption MaxPut(Real k) => new(new MaxBasketPayoff(new PlainVanillaPayoff(OptionType.Put, k)), European());
}
