// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Quotes;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="BlackDeltaCalculator"/> is immutable: QuantLib's <c>setDeltaType</c> and <c>setOptionType</c>, which
/// write the calculator, return a copy holding the new convention or type, and leave the receiver as it was (the S-rx
/// plan's SRXD15, PR6.4b). Each copy computes what a calculator built with its arguments does, bit for bit.
/// </summary>
public class BlackDeltaCalculatorCopyTests
{
    [Fact]
    public void TheSettersReturnCopies()
    {
        const double spot = 1.421, dDf = 0.997306, fDf = 0.992266, stdDev = 0.1180654, strike = 1.608080;
        var calculator = new BlackDeltaCalculator(OptionType.Call, DeltaVolQuoteDeltaType.Spot, spot, dDf, fDf, stdDev);
        double before = calculator.DeltaFromStrike(strike);

        BlackDeltaCalculator forward = calculator.SetDeltaType(DeltaVolQuoteDeltaType.PaFwd);
        BlackDeltaCalculator put = calculator.SetOptionType(OptionType.Put);

        Assert.NotSame(calculator, forward);
        Assert.NotSame(calculator, put);
        Assert.Equal(before, calculator.DeltaFromStrike(strike));
        Assert.Equal(
            new BlackDeltaCalculator(OptionType.Call, DeltaVolQuoteDeltaType.PaFwd, spot, dDf, fDf, stdDev)
                .DeltaFromStrike(strike),
            forward.DeltaFromStrike(strike));
        Assert.Equal(
            new BlackDeltaCalculator(OptionType.Put, DeltaVolQuoteDeltaType.Spot, spot, dDf, fDf, stdDev)
                .DeltaFromStrike(strike),
            put.DeltaFromStrike(strike));
        Assert.NotEqual(before, forward.DeltaFromStrike(strike));
        Assert.NotEqual(before, put.DeltaFromStrike(strike));
    }
}
