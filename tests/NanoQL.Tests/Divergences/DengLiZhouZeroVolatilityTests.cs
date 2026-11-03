// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Deng, Li and Zhou's basket engine at a zero volatility of its positive leg: a real asset, or a negative strike's
/// zero-volatility pseudo-asset where it is the only positive weight. QuantLib divides 0 by 0 and its <c>max(0.0,
/// NaN)</c> returns 0 (<c>denglizhoubasketengine.cpp:173-179</c>, <c>:238</c>), where the price is continuous
/// (9.36646274 at 1e-12 on the test's market). nanoql floors the positive leg's standard deviation at QL_EPSILON²,
/// Choi's engine's floor (<c>choibasketengine.cpp:76-78</c>): the limit and Choi's engine are the checks.
/// </summary>
public class DengLiZhouZeroVolatilityTests
{
    [Fact]
    public void AZeroVolatilityOfThePositiveAssetPricesItsLimit()
    {
        var p2 = Process(100.0, 0.0, 0.05, 0.3);
        BasketOption option = Spread(OptionType.Call, 5.0);
        double zero = DengLiZhouBasketEngine.Calculate(
            option, Context, Both(Process(110.0, 0.0, 0.05, 0.0), p2), Rho(0.5)).NPV!.Value;
        double tiny = DengLiZhouBasketEngine.Calculate(
            option, Context, Both(Process(110.0, 0.0, 0.05, 1e-12), p2), Rho(0.5)).NPV!.Value;
        double choi = ChoiBasketEngine.Calculate(
            option, Context, Both(Process(110.0, 0.0, 0.05, 0.0), p2), Rho(0.5)).NPV!.Value;
        Assert.True(zero > 0.0);
        Assert.Equal(tiny, zero, 1e-8);
        Assert.Equal(choi, zero, 1e-6 * choi);
    }

    [Fact]
    public void ANegativeStrikesPseudoAssetAloneOnThePositiveSidePricesItsLimit()
    {
        // two negative weights and a negative strike: the strike's zero-volatility pseudo-asset is the positive leg
        var p1 = Process(40.0, 0.0, 0.05, 0.3);
        var p2 = Process(30.0, 0.0, 0.05, 0.25);
        BasketOption option = Average(OptionType.Call, -100.0, -1.0, -1.0);
        double dlz = DengLiZhouBasketEngine.Calculate(option, Context, Both(p1, p2), Rho(0.4)).NPV!.Value;
        double choi = ChoiBasketEngine.Calculate(option, Context, Both(p1, p2), Rho(0.4)).NPV!.Value;
        Assert.True(dlz > 0.0);
        Assert.Equal(choi, dlz, 1e-3 * choi);
    }
}
