// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A payoff's <see cref="Payoff.Description"/> writes its numbers as QuantLib's <c>description()</c> streams them
/// (<c>payoffs.cpp:51, 78-79, 150, 174, 210</c>): in a C++ stream's default format, six significant digits in
/// <c>%g</c> style, with a lower-case, two-digit exponent below 1e-4 and from 1e6, and <c>inf</c> and <c>nan</c> for
/// the non-finite values. That isn't .NET's default, the shortest round-trip text, nor its <c>G6</c>, whose exponent
/// is upper-case.
/// </summary>
public class PayoffDescriptionTests
{
    [Fact]
    public void ADescriptionWritesItsNumbersAsACppStreamDoes()
    {
        Assert.Equal("Vanilla Call, 100 strike", new PlainVanillaPayoff(OptionType.Call, 100.0).Description);
        Assert.Equal("Vanilla Put, 1e-07 strike", new PlainVanillaPayoff(OptionType.Put, 1e-7).Description);
        Assert.Equal("Vanilla Call, 0.333333 strike", new PlainVanillaPayoff(OptionType.Call, 1.0 / 3).Description);
        Assert.Equal("Vanilla Call, 1.23457e+06 strike", new PlainVanillaPayoff(OptionType.Call, 1234567.0).Description);
        Assert.Equal("Vanilla Call, -0 strike", new PlainVanillaPayoff(OptionType.Call, -0.0).Description);
        Assert.Equal("Vanilla Call, inf strike", new PlainVanillaPayoff(OptionType.Call, double.PositiveInfinity).Description);
        Assert.Equal(
            "CashOrNothing Put, 50 strike, 2.5e-06 cash payoff", new CashOrNothingPayoff(OptionType.Put, 50.0, 2.5e-6).Description);
        Assert.Equal("Gap Call, 100 strike, 7.65432e+06 strike payoff", new GapPayoff(OptionType.Call, 100.0, 7654321.0).Description);
        Assert.Equal(
            "SuperShare Call, 100 strike, 110 second strike, 0.142857 amount", new SuperSharePayoff(100.0, 110.0, 1.0 / 7).Description);

        // The names and the descriptions without numbers, and SuperFund's, which leaves out its second strike.
        Assert.Equal("FloatingType Put", new FloatingTypePayoff(OptionType.Put).Description);
        Assert.Equal("Null", new NullPayoff().Description);
        Assert.Equal("SuperFund Call, 0.5 strike", new SuperFundPayoff(0.5, 2.0).Description);
    }
}
