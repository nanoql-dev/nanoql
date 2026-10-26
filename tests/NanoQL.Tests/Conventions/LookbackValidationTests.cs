// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The partial lookbacks' validation (the S-bar plan's BR5 (xxv)). QuantLib's dereferences a European exercise and a
/// floating payoff unchecked (<c>lookbackoption.cpp:103-118</c>, <c>:142-145</c>): another exercise or payoff is a null
/// read, undefined. nanoql refuses before the read, with texts naming the cause; QuantLib's own checks, their texts
/// (the lookback period's date, λ's side of 1), come after, as the harness's vectors read them.
/// </summary>
public class LookbackValidationTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EuropeanExercise AYear = new(Today + 365);
    private static readonly AmericanExercise American = new(Today, Today + 365);

    [Fact]
    public void APartialFloatingLookbackRefusesAnExerciseThatIsntEuropean()
    {
        var option = new ContinuousPartialFloatingLookbackOption(100.0, 1.0, Today + 180, new FloatingTypePayoff(OptionType.Call), American);
        Assert.Equal(
            "a partial-time floating lookback option needs a European exercise", Assert.Throws<NanoQLException>(option.Validate).Message);
    }

    [Fact]
    public void APartialFloatingLookbackRefusesAPayoffThatIsntFloating()
    {
        var option = new ContinuousPartialFloatingLookbackOption(100.0, 1.0, Today + 180, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);
        Assert.Equal(
            "a partial-time floating lookback option needs a floating payoff", Assert.Throws<NanoQLException>(option.Validate).Message);
    }

    [Fact]
    public void APartialFixedLookbackRefusesAnExerciseThatIsntEuropean()
    {
        var option = new ContinuousPartialFixedLookbackOption(Today + 180, new PlainVanillaPayoff(OptionType.Call, 100.0), American);
        Assert.Equal(
            "a partial-time fixed lookback option needs a European exercise", Assert.Throws<NanoQLException>(option.Validate).Message);
    }
}
