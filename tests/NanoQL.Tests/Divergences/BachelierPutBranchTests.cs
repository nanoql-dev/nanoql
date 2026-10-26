// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Bachelier calculator's put branch. QuantLib tells a call from a put by <c>alpha &gt;= 0</c>
/// (<c>bacheliercalculator.cpp:102-114</c>, <c>:183-188</c>), and a put's alpha, N(d) − 1, rounds to 0 where N(d)
/// rounds to 1, so a put far out of the money prices as the call: F 100, K 50, σ√T 1, D 0.95 prices 47.5 where its value
/// is 0, and at a zero deviation an out-of-the-money put (K 99) prices 0.95. nanoql tells them apart by the payoff's
/// type (the S-exo plan's ER5 (xxvi)). The checks: Bachelier's put formula, and QuantLib's own call through parity.
/// </summary>
public class BachelierPutBranchTests
{
    [Fact]
    public void AFarOutOfTheMoneyPutIsWorthBacheliersValue()
    {
        var put = new BachelierCalculator(OptionType.Put, 50.0, 100.0, 1.0, 0.95);
        var call = new BachelierCalculator(OptionType.Call, 50.0, 100.0, 1.0, 0.95);
        double d = (100.0 - 50.0) / 1.0;
        var n = new CumulativeNormalDistribution();
        var density = new NormalDistribution();
        double bachelier = 0.95 * (((50.0 - 100.0) * n.Evaluate(-d)) + (1.0 * density.Evaluate(d)));
        Assert.Equal(bachelier, put.Value, 1e-15);

        // the call less the discounted forward over the strike, where QuantLib's put branch prices 47.5
        Assert.Equal(call.Value - (0.95 * (100.0 - 50.0)), put.Value, 1e-12);
        Assert.Equal(0.0, put.DeltaForward, 1e-15);
        Assert.Equal(0.0, put.ItmCashProbability, 1e-15);
    }

    [Fact]
    public void AnOutOfTheMoneyPutAtAZeroDeviationIsWorthNothing()
    {
        var put = new BachelierCalculator(OptionType.Put, 99.0, 100.0, 0.0, 0.95);
        Assert.Equal(0.0, put.Value);
        Assert.Equal(0.0, put.DeltaForward);
        Assert.Equal(0.0, put.StrikeSensitivity);
        Assert.Equal(0.0, put.ItmCashProbability);
        Assert.Equal(0.0, put.ItmAssetProbability);

        // QuantLib's own call through parity: 0.95 less the discounted forward over the strike
        var call = new BachelierCalculator(OptionType.Call, 99.0, 100.0, 0.0, 0.95);
        Assert.Equal(call.Value - (0.95 * (100.0 - 99.0)), put.Value, 1e-15);
    }
}
