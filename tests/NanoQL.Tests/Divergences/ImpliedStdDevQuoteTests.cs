// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Quotes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="ImpliedStdDevQuote"/>'s solve after its inputs move (the S-core plan's CR1 (d)): QuantLib's quote starts
/// each solve after the first from the last one's result (<c>impliedstddevquote.cpp:34</c>, <c>:59</c>); nanoql's
/// quote is immutable, a new forward or price is a new quote solved from its caller's guess, which may be the last
/// quote's value. QuantLib's call at a 4% forward and strike, priced 2%, solved from 0.15 to 1e-6, gives
/// 1.3489795003921468; its forward moved to 5%, the second solve, from that value, gives 0.83176179858025456, and a
/// fresh quote from 0.15 gives 0.83176159858703613, two roots within the accuracy (this run's <c>probes\c1_facts.cpp</c>).
/// </summary>
public class ImpliedStdDevQuoteTests
{
    [Fact]
    public void AMovedForwardIsSolvedFromTheCallersGuess()
    {
        var first = new ImpliedStdDevQuote(OptionType.Call, 0.04, 0.02, 0.04, 0.15, 1.0e-6);
        Assert.Equal(1.3489795003921468, first.Value, 15);

        // a new quote from the caller's guess: QuantLib's fresh quote
        var fresh = new ImpliedStdDevQuote(OptionType.Call, 0.05, 0.02, 0.04, 0.15, 1.0e-6);
        Assert.Equal(0.83176159858703613, fresh.Value, 15);

        // QuantLib's second value is the solve from the first value, which the caller gives as the guess
        var chained = new ImpliedStdDevQuote(OptionType.Call, 0.05, 0.02, 0.04, first.Value, 1.0e-6);
        Assert.Equal(0.83176179858025456, chained.Value, 15);
        Assert.True(Math.Abs(chained.Value - fresh.Value) < 1.0e-6);
        Assert.NotEqual(chained.Value, fresh.Value);
    }
}
