// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Deng, Li and Zhou's engine at a negative strike with exactly one negative weight. QuantLib adds |K| as a
/// zero-volatility asset of weight +1 and lumps it with the positive leg into one lognormal
/// (<c>denglizhoubasketengine.cpp:86-96</c>, <c>:117-140</c>), where the payoff's identity, the call on (w, K) the put
/// on (−w, −K), reaches the method's own setting of one positive weight: on a two-asset spread at K = −10 the direct
/// route is 0.08 to 0.09 above the identity's, the put at ρ 0.9 0.852254 against 0.766746 (Choi 0.766703, the Monte
/// Carlo 0.7682 ± 0.0035). nanoql prices those inputs by the identity, and every other negative strike by QuantLib's
/// branch, so <c>testDengLiZhouWithNegativeStrike</c> (two negative weights) keeps 3.34412 (the S-exo plan's ER5
/// (xxxv)): QuantLib's engine on the flipped inputs, bit for bit, and Choi's engine are the checks.
/// </summary>
public class DengLiZhouNegativeStrikeTests
{
    [Fact]
    public void ASpreadAtANegativeStrikeIsTheFlippedPut()
    {
        ImmutableArray<GeneralizedBlackScholesProcess> p = Both(Process(110.0, 0.0, 0.05, 0.35), Process(100.0, 0.0, 0.05, 0.2));
        foreach ((OptionType type, OptionType flipped) in new[] { (OptionType.Put, OptionType.Call), (OptionType.Call, OptionType.Put) })
        {
            double calculated = DengLiZhou(Average(type, -10.0, 1.0, -1.0), p, 0.9);
            double onFlipped = DengLiZhou(Average(flipped, 10.0, -1.0, 1.0), p, 0.9);
            double choi = ChoiBasketEngine.Calculate(Average(type, -10.0, 1.0, -1.0), Context, p, Rho(0.9)).NPV!.Value;
            Assert.Equal(onFlipped, calculated);
            Assert.Equal(choi, calculated, 2e-4);
        }
    }

    [Fact]
    public void TwoPositiveWeightsAndOneNegativeMeetChoi()
    {
        ImmutableArray<GeneralizedBlackScholesProcess> p =
            [Process(60.0, 0.0, 0.05, 0.3), Process(50.0, 0.0, 0.05, 0.25), Process(100.0, 0.0, 0.05, 0.2)];
        BasketOption option = Average(OptionType.Call, -10.0, 1.0, 1.0, -1.0);
        double dlz = DengLiZhou(option, p, 0.6);
        double onFlipped = DengLiZhou(Average(OptionType.Put, 10.0, -1.0, -1.0, 1.0), p, 0.6);
        double choi = ChoiBasketEngine.Calculate(option, Context, p, Rho(0.6, 3)).NPV!.Value;
        Assert.Equal(onFlipped, dlz);

        // the identity's route 4.3e-5 from Choi's, QuantLib's direct route 0.0197
        Assert.Equal(choi, dlz, 2e-4);
    }

    private static double DengLiZhou(BasketOption option, ImmutableArray<GeneralizedBlackScholesProcess> p, double rho) =>
        DengLiZhouBasketEngine.Calculate(option, Context, p, Rho(rho, p.Length)).NPV!.Value;
}
