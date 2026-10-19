// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the payoffs' refusals, which the vectors record only as throws: <see cref="NullPayoff"/>'s
/// <see cref="NullPayoff.Evaluate"/> and <see cref="FloatingTypePayoff"/>'s one-argument
/// <see cref="FloatingTypePayoff.Evaluate(double)"/> always refuse, QuantLib's <c>QL_FAIL</c>s on the payoff itself
/// (<c>payoffs.cpp:36, 62</c>), as <see cref="NanoQLException"/> with QuantLib's texts; the superfund and supershare
/// payoffs' checks of their strikes (<c>payoffs.hpp:213-218, 240-242</c>) throw <see cref="ArgumentException"/> naming
/// the strike; and an option type outside <see cref="OptionType"/>, which QuantLib stores and refuses only when the
/// payoff is evaluated or described, <see cref="ArgumentOutOfRangeException"/> on construction, with the text of
/// QuantLib's switches on it (<c>payoffs.cpp:72, 98</c>).
/// </summary>
public class PayoffErrorTests
{
    [Fact]
    public void ThePayoffsRefusals()
    {
        var floating = new FloatingTypePayoff(OptionType.Put);
        foreach (double price in new[] { 0.0, 100.0, double.NaN })
        {
            Assert.Equal("dummy payoff given", Assert.Throws<NanoQLException>(() => new NullPayoff().Evaluate(price)).Message);
            Assert.Equal("floating payoff not handled", Assert.Throws<NanoQLException>(() => floating.Evaluate(price)).Message);
        }

        // The two-argument form evaluates.
        Assert.Equal(20.0, floating.Evaluate(80.0, 100.0));

        ArgumentException e = Assert.Throws<ArgumentException>(() => new SuperFundPayoff(0.0, 1.0));
        Assert.Equal("strike", e.ParamName);
        Assert.StartsWith("strike (0) must be positive", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentException>(() => new SuperFundPayoff(100.0, 90.0));
        Assert.Equal("secondStrike", e.ParamName);
        Assert.StartsWith("second strike (90) must be higher than first strike (100)", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentException>(() => new SuperSharePayoff(100.0, 100.0, 10.0));
        Assert.Equal("secondStrike", e.ParamName);
        Assert.StartsWith("second strike (100) must be higher than first strike (100)", e.Message, StringComparison.Ordinal);

        // SuperShare has no positivity check, as in QuantLib.
        Assert.Equal(-1.0, new SuperSharePayoff(-1.0, 1.0, 5.0).Strike);

        foreach (Func<object> f in new Func<object>[]
        {
            () => new FloatingTypePayoff((OptionType)0),
            () => new PlainVanillaPayoff((OptionType)2, 100.0),
            () => new PercentageStrikePayoff((OptionType)0, 1.0),
            () => new AssetOrNothingPayoff((OptionType)(-2), 100.0),
            () => new CashOrNothingPayoff((OptionType)0, 100.0, 10.0),
            () => new GapPayoff((OptionType)0, 100.0, 90.0),
        })
        {
            ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(f);
            Assert.Equal("type", r.ParamName);
            Assert.StartsWith("unknown/illegal option type", r.Message, StringComparison.Ordinal);
        }
    }
}
