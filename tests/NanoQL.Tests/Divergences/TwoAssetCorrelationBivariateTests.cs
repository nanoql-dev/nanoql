// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The two-asset correlation engine's bivariate normal. QuantLib's engine reads Drezner's 1978 algorithm
/// (<c>analytictwoassetcorrelationengine.cpp:44</c>), "six decimal places accuracy" by its header
/// (<c>bivariatenormaldistribution.hpp:33</c>), where QuantLib's default bivariate normal is West's; the value moves by
/// 2.5e-7 on QuantLib's own case. nanoql reads West's (the S-exo plan's ER7, ruled by the user on 2026-10-25): a
/// quadrature over the joint lognormal (<see cref="TwoAssetCorrelationQuadrature"/>) is the check, at 1e-9, which
/// QuantLib's value meets within Drezner's accuracy.
/// </summary>
public class TwoAssetCorrelationBivariateTests
{
    [Fact]
    public void TheValueMeetsTheQuadrature()
    {
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            foreach (double rho in new[] { -0.5, 0.3, 0.75 })
            {
                var option = new TwoAssetCorrelationOption(type, 50.0, 70.0, European());
                double calculated = AnalyticTwoAssetCorrelationEngine.Calculate(
                    option, Context, Process(52.0, 0.0, 0.1, 0.2), Process(65.0, 0.0, 0.1, 0.3), rho).NPV!.Value;
                double quadrature = TwoAssetCorrelationQuadrature.Value(
                    type, 50.0, 70.0, 52.0, 65.0, 0.0, 0.0, 0.1, 0.2, 0.3, rho, 1.0);
                Assert.Equal(quadrature, calculated, 1e-9);
            }
        }
    }
}
