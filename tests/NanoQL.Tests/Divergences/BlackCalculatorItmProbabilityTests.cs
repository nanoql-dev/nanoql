// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="BlackCalculator"/>'s in-the-money probabilities for a put. QuantLib's <c>itmCashProbability()</c> and
/// <c>itmAssetProbability()</c> return N(d2) and N(d1) for every payoff (<c>blackcalculator.hpp:129-135</c>), a call's,
/// and the analytic engine reports the first for puts (<c>analyticeuropeanengine.cpp:105</c>): a put struck at 90 on a
/// forward of 100 at 20% reads 0.665238, where QuantLib's own cash-or-nothing put values 0.334762. nanoql's put reads
/// 1 − N(d2) and 1 − N(d1), the expressions QuantLib's cash-or-nothing and asset-or-nothing puts value, which check it.
/// </summary>
public class BlackCalculatorItmProbabilityTests
{
    [Fact]
    public void APutsProbabilitiesAreItsDigitalsValues()
    {
        var put = new BlackCalculator(OptionType.Put, 90.0, 100.0, 0.2);
        double cash = new BlackCalculator(new CashOrNothingPayoff(OptionType.Put, 90.0, 1.0), 100.0, 0.2).Value;
        double asset = new BlackCalculator(new AssetOrNothingPayoff(OptionType.Put, 90.0), 100.0, 0.2).Value;

        Assert.Equal(cash, put.ItmCashProbability, 1e-15);
        Assert.Equal(asset / 100.0, put.ItmAssetProbability, 1e-15);
        Assert.Equal(0.3347615642027689, put.ItmCashProbability, 1e-15);
    }
}
