// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The operator-splitting spread engine at a zero volatility of the second asset. QuantLib's corrections are 0·∞
/// there (<c>operatorsplittingspreadengine.cpp:60-65</c>, <c>:138-139</c>), so the price is NaN, from a volatility of
/// about 1e-200, where its variance underflows; above, its corrections tend to 0 and its price to Kirk's, exact there
/// (9.9375389513, and 9.937538951281 at 1e-12 on the plan's market). nanoql takes the corrections' limit, 0 (the S-exo
/// plan's ER5 (xi)): QuantLib's own price at a tiny volatility and Kirk's engine are the checks.
/// </summary>
public class OperatorSplittingZeroVolatilityTests
{
    [Fact]
    public void BothOrdersPriceKirksLimit()
    {
        var p1 = Process(110.0, 0.02, 0.05, 0.3);
        foreach (OperatorSplittingSpreadEngineOrder order in new[]
            { OperatorSplittingSpreadEngineOrder.First, OperatorSplittingSpreadEngineOrder.Second })
        {
            double zero = OperatorSplittingSpreadEngine.Calculate(
                Spread(OptionType.Call, 10.0), Context, p1, Process(100.0, 0.03, 0.05, 0.0), 0.6, order).NPV!.Value;
            double tiny = OperatorSplittingSpreadEngine.Calculate(
                Spread(OptionType.Call, 10.0), Context, p1, Process(100.0, 0.03, 0.05, 1e-12), 0.6, order).NPV!.Value;
            double kirk = KirkEngine.Calculate(
                Spread(OptionType.Call, 10.0), Context, p1, Process(100.0, 0.03, 0.05, 0.0), 0.6).NPV!.Value;
            Assert.False(double.IsNaN(zero));
            Assert.Equal(tiny, zero, 1e-9);
            Assert.Equal(kirk, zero, 1e-10);
        }
    }

    [Fact]
    public void ThePutFollowsByParity()
    {
        var p1 = Process(110.0, 0.02, 0.05, 0.3);
        var p2 = Process(100.0, 0.03, 0.05, 0.0);
        double call = OperatorSplittingSpreadEngine.Calculate(Spread(OptionType.Call, 10.0), Context, p1, p2, 0.6).NPV!.Value;
        double put = OperatorSplittingSpreadEngine.Calculate(Spread(OptionType.Put, 10.0), Context, p1, p2, 0.6).NPV!.Value;
        double df = Flat(0.05).Discount(Today + 365);
        double f1 = 110.0 * Flat(0.02).Discount(Today + 365) / df, f2 = 100.0 * Flat(0.03).Discount(Today + 365) / df;
        Assert.Equal(df * (f1 - f2 - 10.0), call - put, 1e-10);
    }
}
