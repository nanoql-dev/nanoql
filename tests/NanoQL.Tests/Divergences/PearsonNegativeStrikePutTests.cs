// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Pearson's spread engine where the conditional strike, the second forward at a node plus the strike, is zero or
/// less. QuantLib's integrand takes the call's intrinsic value there whatever the type
/// (<c>pearsonspreadengine.cpp:73-75</c>), so a put prices that intrinsic (136.84 on the plan's market where Choi's
/// engine gives 0.00304, Deng-Li-Zhou's 0.00303 and a quadrature 0.00313). nanoql's put takes its conditional value,
/// 0 there (the S-exo plan's ER5 (ix)): Choi's engine and the put-call parity, which QuantLib's put breaks, are the
/// checks.
/// </summary>
public class PearsonNegativeStrikePutTests
{
    private const Rate R = 0.05;

    [Fact]
    public void APutAtNegativeConditionalStrikesMeetsChoi()
    {
        // the second asset above the first, the strike below minus the second forward over much of its range
        var p1 = Process(50.0, 0.0, R, 0.3);
        var p2 = Process(60.0, 0.0, R, 0.6);
        BasketOption put = Spread(OptionType.Put, -40.0);
        double pearson = PearsonSpreadEngine.Calculate(put, Context, p1, p2, 0.3).NPV!.Value;
        double choi = ChoiBasketEngine.Calculate(put, Context, Both(p1, p2), Rho(0.3)).NPV!.Value;
        Assert.Equal(choi, pearson, 2e-4);
    }

    [Fact]
    public void PutCallParityHoldsAtNegativeConditionalStrikes()
    {
        var p1 = Process(50.0, 0.0, R, 0.3);
        var p2 = Process(60.0, 0.0, R, 0.6);
        double call = PearsonSpreadEngine.Calculate(Spread(OptionType.Call, -40.0), Context, p1, p2, 0.3).NPV!.Value;
        double put = PearsonSpreadEngine.Calculate(Spread(OptionType.Put, -40.0), Context, p1, p2, 0.3).NPV!.Value;
        double df = Flat(R).Discount(Today + 365);
        double f1 = 50.0 / df, f2 = 60.0 / df;
        Assert.Equal(df * (f1 - f2 + 40.0), call - put, 1e-8);
    }
}
