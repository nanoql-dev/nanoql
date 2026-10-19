// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the smile sections' refusals, which the vectors record only as throws: a section's
/// constructor refuses its arguments with <see cref="ArgumentException"/> naming them (an exercise date before the
/// reference date, a negative or NaN exercise time), a null day counter in the date forms with
/// <see cref="ArgumentNullException"/>, and an unknown volatility or option type with
/// <see cref="ArgumentOutOfRangeException"/>; what the section can't give, a reference date for a section by time, a
/// price, vega or converted volatility without an ATM level and a normal section's vega, throws
/// <see cref="NanoQLException"/>, with QuantLib's texts; and a Black formula's refusal of a strike reaches the caller
/// as the formula raises it.
/// </summary>
public class SmileSectionErrorTests
{
    private static readonly Date Reference = new(15, Month.January, 2025);

    [Fact]
    public void TheSmileSectionsRefusals()
    {
        var dc = new Actual365Fixed();
        Date before = Reference - 1;
        AssertArgument(
            "d",
            "expiry date (January 14th, 2025) must be greater than reference date (January 15th, 2025)",
            () => new FlatSmileSection(before, 0.2, dc, Reference));
        AssertArgument(
            "d",
            "expiry date (January 14th, 2025) must be greater than reference date (January 15th, 2025)",
            () => FlatSmileSection.Create(before, 0.2, dc, new EvaluationContext { EvaluationDate = Reference }));
        AssertArgument("exerciseTime", "expiry time must be positive: -0.25 not allowed", () => new FlatSmileSection(-0.25, 0.2, dc));
        AssertArgument("exerciseTime", "expiry time must be positive: NaN not allowed", () => new FlatSmileSection(double.NaN, 0.2, null));
        Assert.Equal("dc", Assert.Throws<ArgumentNullException>(() => new FlatSmileSection(Reference + 30, 0.2, null!, Reference)).ParamName);
        Assert.Equal(
            "context", Assert.Throws<ArgumentNullException>(() => FlatSmileSection.Create(Reference + 30, 0.2, dc, null!)).ParamName);
        Assert.Equal("d", Assert.Throws<ArgumentException>(() => new FlatSmileSection(default, 0.2, dc, Reference)).ParamName);
        Assert.Equal("referenceDate", Assert.Throws<ArgumentException>(() => new FlatSmileSection(Reference, 0.2, dc, default)).ParamName);
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new FlatSmileSection(1.0, 0.2, dc, 0.03, (VolatilityType)2));
        Assert.Equal("type", e.ParamName);
        Assert.StartsWith("Unknown volatility type (2)", e.Message, StringComparison.Ordinal);

        var noAtm = new FlatSmileSection(1.0, 0.2, dc);
        AssertFailure("referenceDate not available for this instance", () => noAtm.ReferenceDate());
        AssertFailure("smile section must provide atm level to compute option price", () => noAtm.OptionPrice(0.03));
        AssertFailure("smile section must provide atm level to compute option price", () => noAtm.DigitalOptionPrice(0.03));
        AssertFailure("smile section must provide atm level to compute option vega", () => noAtm.Vega(0.03));
        AssertFailure(
            "smile section must provide atm level to compute converted volatilties",
            () => noAtm.Volatility(0.03, VolatilityType.Normal));
        var normal = new FlatSmileSection(1.0, 0.01, dc, 0.03, VolatilityType.Normal);
        AssertFailure("vega for normal smilesection not yet implemented", () => normal.Vega(0.03));

        var section = new FlatSmileSection(Reference + 365, 0.2, dc, Reference, 0.03, VolatilityType.ShiftedLognormal, 0.01);
        Assert.Equal("type", Assert.Throws<ArgumentOutOfRangeException>(() => section.OptionPrice(0.03, (OptionType)0)).ParamName);
        AssertArgument("strike", "strike + displacement (-0.02 + 0.01) must be non-negative", () => section.OptionPrice(-0.02));
    }

    private static void AssertArgument(string paramName, string message, Action action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void AssertFailure(string message, Func<object> action) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(action).Message);
}
