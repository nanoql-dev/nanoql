// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// C3's instruments' refusals (the S-exo plan's ED14; C3's review, B3-3): each check's exception type, QuantLib's text
/// and the parameter it names, at construction or in validation, as QuantLib's arguments check them.
/// </summary>
public class ExoticInstrumentErrorTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly EuropeanExercise Year = new(Today + 365);

    [Fact]
    public void TheCliquetOptionsChecks()
    {
        Assert.Equal(
            "negative or zero moneyness given",
            Refused(() => new CliquetOption(new PercentageStrikePayoff(OptionType.Call, 0.0), Year, [Today + 91]).Validate()));
        Assert.Equal(
            "no reset dates given",
            Refused(() => new CliquetOption(new PercentageStrikePayoff(OptionType.Call, 1.0), Year, []).Validate()));
        Assert.Equal(
            "reset date greater or equal to maturity",
            Refused(() => new CliquetOption(new PercentageStrikePayoff(OptionType.Call, 1.0), Year, [Today + 365]).Validate()));
        Assert.Equal(
            "unsorted reset dates",
            Refused(() => new CliquetOption(
                new PercentageStrikePayoff(OptionType.Call, 1.0), Year, [Today + 182, Today + 91]).Validate()));
    }

    [Fact]
    public void TheForwardVanillaOptionsChecks()
    {
        PlainVanillaPayoff call = new(OptionType.Call, 0.0);
        ArgumentException noDate = Assert.Throws<ArgumentException>(() => new ForwardVanillaOption(1.0, default, call, Year));
        Assert.Equal("resetDate", noDate.ParamName);
        Assert.StartsWith("null reset date given", noDate.Message, StringComparison.Ordinal);
        Assert.Equal(
            "reset date in the past",
            Refused(() => new ForwardVanillaOption(1.0, Today - 1, call, Year).Validate(Context)));
        Assert.Equal(
            "negative or zero moneyness given",
            Refused(() => new ForwardVanillaOption(0.0, Today + 91, call, Year).Validate()));
        Assert.Equal(
            "reset date later or equal to maturity",
            Refused(() => new ForwardVanillaOption(1.0, Today + 365, call, Year).Validate()));
    }

    [Fact]
    public void TheChooserOptionsChecks()
    {
        ArgumentException simple = Assert.Throws<ArgumentException>(() => new SimpleChooserOption(default, 100.0, Year));
        Assert.Equal("choosingDate", simple.ParamName);
        Assert.StartsWith(" no choosing date given", simple.Message, StringComparison.Ordinal);
        Assert.Equal(
            "choosing date later than or equal to maturity date",
            Refused(() => new SimpleChooserOption(Today + 365, 100.0, Year).Validate()));

        var half = new EuropeanExercise(Today + 182);
        ArgumentException complex = Assert.Throws<ArgumentException>(
            () => new ComplexChooserOption(default, 100.0, 100.0, Year, half));
        Assert.Equal("choosingDate", complex.ParamName);
        Assert.StartsWith(" no choosing date given", complex.Message, StringComparison.Ordinal);
        Assert.Equal(
            "choosing date later than or equal to Call maturity date",
            Refused(() => new ComplexChooserOption(Today + 182, 100.0, 100.0, half, Year).Validate()));
        Assert.Equal(
            "choosing date later than or equal to Put maturity date",
            Refused(() => new ComplexChooserOption(Today + 182, 100.0, 100.0, Year, half).Validate()));
    }

    [Fact]
    public void TheExtensibleAndCompoundOptionsChecks()
    {
        PlainVanillaPayoff call = new(OptionType.Call, 100.0);
        var half = new EuropeanExercise(Today + 182);
        ArgumentException noDate = Assert.Throws<ArgumentException>(
            () => new HolderExtensibleOption(OptionType.Call, 1.0, default, 105.0, call, half));
        Assert.Equal("secondExpiryDate", noDate.ParamName);
        Assert.StartsWith("no extending date given", noDate.Message, StringComparison.Ordinal);
        Assert.Equal(
            "negative premium not allowed",
            Refused(() => new HolderExtensibleOption(OptionType.Call, -1.0, Today + 365, 105.0, call, half).Validate()));
        Assert.Equal(
            "extended date is earlier than or equal to first maturity date",
            Refused(() => new HolderExtensibleOption(OptionType.Call, 1.0, Today + 91, 105.0, call, half).Validate()));

        Assert.Equal(
            "second exercise date is not later than the first",
            Refused(() => new WriterExtensibleOption(call, Year, new PlainVanillaPayoff(OptionType.Call, 105.0), half).Validate()));
        Assert.Equal(
            "maturity of compound option exceeds maturity of underlying option",
            Refused(() => new CompoundOption(new PlainVanillaPayoff(OptionType.Call, 5.0), Year, call, half).Validate()));
    }

    private static string Refused(Action act) => Assert.Throws<NanoQLException>(act).Message;
}
