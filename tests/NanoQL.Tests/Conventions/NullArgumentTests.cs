// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A null argument a member doesn't accept throws <see cref="ArgumentNullException"/> on entry, naming the caller's
/// parameter rather than one further down the call.
/// </summary>
public class NullArgumentTests
{
    [Fact]
    public void ANullResultDayCounterNamesTheCallersParameter()
    {
        Date today = new(15, Month.October, 2026);
        var curve = new FlatForward(today, 0.03, new Actual365Fixed());

        AssertParam("resultDayCounter", () => curve.ZeroRate(today + 365, null!, Compounding.Continuous));
        AssertParam("resultDayCounter", () => curve.ForwardRate(today + 30, today + 365, null!, Compounding.Simple));
        AssertParam("resultDayCounter", () => curve.ForwardRate(
            today + 30, new Period(1, TimeUnit.Years), null!, Compounding.Simple));
        AssertParam("resultDC", () => InterestRate.ImpliedRate(1.05, null!, Compounding.Simple, Frequency.Annual, 1.0));
        AssertParam("resultDC", () => InterestRate.ImpliedRate(
            1.05, null!, Compounding.Simple, Frequency.Annual, today, today + 365));
    }

    [Fact]
    public void CurrenciesAndTimeSeriesRefuseNullArguments()
    {
        var rounding = new Rounding();
        AssertParam("name", () => new Currency(null!, "CCY", 1, "#", string.Empty, 100, rounding));
        AssertParam("code", () => new Currency("Some Currency", null!, 1, "#", string.Empty, 100, rounding));
        AssertParam("symbol", () => new Currency("Some Currency", "CCY", 1, null!, string.Empty, 100, rounding));
        AssertParam("fractionSymbol", () => new Currency("Some Currency", "CCY", 1, "#", null!, 100, rounding));
        AssertParam("rounding", () => new Currency("Some Currency", "CCY", 1, "#", string.Empty, 100, null!));

        // A std::set<std::string> holds no null.
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new Currency("Some Currency", "CCY", 1, "#", string.Empty, 100, rounding, null, ["CCy", null!]));
        Assert.Equal("minorUnitCodes", e.ParamName);

        Date today = new(15, Month.October, 2026);
        AssertParam("dates", () => new TimeSeries<double>(null!, [1.0]));
        AssertParam("values", () => new TimeSeries<double>([today], null!));
        AssertParam("values", () => new TimeSeries<double>(today, null!));
    }

    private static void AssertParam(string paramName, Action action) =>
        Assert.Equal(paramName, Assert.Throws<ArgumentNullException>(action).ParamName);
}
