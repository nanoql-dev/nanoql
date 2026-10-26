// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The two-asset correlation engine at a negative second strike. QuantLib checks the first strike but not the second
/// (<c>analytictwoassetcorrelationengine.cpp:45</c>), whose <c>log(s2/X2)</c> is NaN and reaches Drezner's "case not
/// handled", where the call, (S₂ − X₂)·1{S₁ &gt; X₁}, is s₂e^{(b₂−r)T}N(y₁ + ρσ₂√T) − X₂e^{−rT}N(y₁) (57.670585 at X₂ =
/// −10 on the plan's market; QuantLib's X₂ = 0 value is the same form, 52.851193) and the put worth nothing. nanoql
/// prices those forms (the S-exo plan's ER5 (xxxiv)): a quadrature over the joint lognormal (Revision 69,
/// <see cref="TwoAssetCorrelationQuadrature"/>) and QuantLib's X₂ = 0 value are the checks.
/// </summary>
public class TwoAssetCorrelationNegativeStrikeTests
{
    [Fact]
    public void TheCallMeetsTheQuadratureAndTheZeroStrikeValue()
    {
        var p1 = Process(52.0, 0.02, 0.1, 0.2);
        var p2 = Process(65.0, 0.01, 0.1, 0.3);
        double calculated = Price(OptionType.Call, -10.0, p1, p2);
        double quadrature = TwoAssetCorrelationQuadrature.Value(
            OptionType.Call, 50.0, -10.0, 52.0, 65.0, 0.02, 0.01, 0.1, 0.2, 0.3, 0.75, 1.0);
        Assert.Equal(quadrature, calculated, 1e-9);

        // QuantLib's own value at X2 = 0, where the bivariate normal's infinite bound gives the same form
        Assert.Equal(Price(OptionType.Call, 0.0, p1, p2), Price(OptionType.Call, -1e-9, p1, p2), 1e-8);
    }

    [Fact]
    public void ThePutIsWorthNothing()
    {
        Assert.Equal(
            0.0, Price(OptionType.Put, -10.0, Process(52.0, 0.02, 0.1, 0.2), Process(65.0, 0.01, 0.1, 0.3)));
    }

    private static double Price(
        OptionType type, double x2, Processes.GeneralizedBlackScholesProcess p1, Processes.GeneralizedBlackScholesProcess p2) =>
        AnalyticTwoAssetCorrelationEngine.Calculate(
            new TwoAssetCorrelationOption(type, 50.0, x2, European()), Context, p1, p2, 0.75).NPV!.Value;
}
