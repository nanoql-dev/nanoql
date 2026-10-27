// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The cap pseudo-derivative's implied volatility (the S-mmp plan's MMPR4 (xviii), ruled by the user). QuantLib solves
/// for the flat undisplaced Black volatility in [0.99, 1.01] times its least displaced and greatest scaled caplet
/// volatility (<c>swaptionpseudojacobian.cpp:331</c>), which away from the money on a displaced model misses the root: on
/// six semi-annual rates, FlatVol 20%, 3 factors, displacement 2%, the cap from rate 1 to 5, its bracket [0.198,
/// 0.332323] refuses strikes 0.5%, 1% and 2% "root not bracketed" (this run's <c>probes\c4_facts.cpp</c>; the planner's
/// MPP22). nanoql solves by Brent's bracketing from QuantLib's guess there. Checks: the volatility reprices the model's
/// cap through the class's undisplaced Black sum; QuantLib's bits where its bracket holds the root (3.2%).
/// </summary>
public class CapPseudoDerivativeBracketTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Theory]
    [InlineData(0.005)]
    [InlineData(0.01)]
    [InlineData(0.02)]
    public void AwayFromTheMoneyTheVolatilityRepricesTheCap(double strike)
    {
        MarketModel model = Model();
        double vol = new CapPseudoDerivative(model, strike, 1, 5, 0.95).ImpliedVolatility;
        (double price, double atVol) = Prices(model, strike, vol);
        Assert.True(Math.Abs(atVol - price) <= 1e-9, $"{atVol} against {price}");

        // outside QuantLib's bracket
        Assert.True(vol > 0.332323 || vol < 0.198, $"{vol}");
    }

    [Fact]
    public void WhereQuantLibsBracketHoldsTheRootTheVolatilityIsQuantLibs() =>
        Assert.Equal(0.32537576476731966, new CapPseudoDerivative(Model(), 0.032, 1, 5, 0.95).ImpliedVolatility);

    private static MarketModel Model() =>
        Market.FlatVol(new EvolutionDescription(Market.RateTimes, Market.RateTimes[..^1]), 3, 0.20, 0.02);

    // The model's displaced cap, and the undisplaced Black sum at a flat volatility.
    private static (double Price, double AtVol) Prices(MarketModel model, double strike, double vol)
    {
        var cs = new LMMCurveState(Market.RateTimes);
        cs.SetOnForwardRates(Market.Forwards);
        Matrix total = model.TotalCovariance(model.NumberOfSteps - 1);
        double price = 0.0, atVol = 0.0;
        for (int j = 1; j < 5; ++j)
        {
            double annuity = cs.DiscountRatio(j + 1, 0) * Market.Accruals[j] * 0.95;
            price += BlackFormulaFunctions.BlackFormula(OptionType.Call, strike, Market.Forwards[j], Math.Sqrt(total[j, j]), annuity, 0.02);
            atVol += BlackFormulaFunctions.BlackFormula(OptionType.Call, strike, Market.Forwards[j], vol * Math.Sqrt(Market.RateTimes[j]), annuity);
        }

        return (price, atVol);
    }
}
