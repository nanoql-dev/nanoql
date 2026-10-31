// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Extensions;

/// <summary><see cref="AmortizingBond"/>'s refusals: each exception's type, message and <c>ParamName</c>.</summary>
public class AmortizingBondErrorTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(16, September, 2015) };

    [Fact]
    public void ANullDayCounterOrContextIsRefused()
    {
        Assert.Equal("dCounter", Assert.Throws<ArgumentNullException>(() => Bond(nullDayCounter: true)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => Bond(nullContext: true)).ParamName);
    }

    [Fact]
    public void AMethodOutsideItsTypeIsRefused()
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => Bond(method: (AmortizingMethod)1));
        Assert.Equal("method", e.ParamName);
        Assert.StartsWith("not an amortising method", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 1, 2005)]
    [InlineData(2, 1, 2005)]
    public void ATradeOnOrAfterMaturityIsRefused(int day, int month, int year)
    {
        var trade = new Date(day, (Month)month, year);
        var e = Assert.Throws<ArgumentException>(() => Bond(trade: trade));
        Assert.Equal("tradeDate", e.ParamName);
        Assert.StartsWith(
            $"trade date ({trade}) must be before the maturity date", e.Message, StringComparison.Ordinal);
    }

    private static AmortizingBond Bond(
        bool nullDayCounter = false,
        bool nullContext = false,
        AmortizingMethod method = AmortizingMethod.EffectiveInterestRate,
        Date? trade = null) =>
        new(40000, 43412, 0.06, new Date(1, January, 2001), new Date(1, January, 2005),
            trade ?? new Date(1, January, 2004), Frequency.Semiannual,
            nullDayCounter ? null! : new Thirty360(Thirty360Convention.USA), method, new NullCalendar(),
            nullContext ? null! : Context);
}
