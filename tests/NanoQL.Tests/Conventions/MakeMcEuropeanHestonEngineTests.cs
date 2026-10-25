// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="MakeMCEuropeanHestonEngine{TRng, TStats}"/>'s seed is explicit (S2R4; the S-hes plan's HR2 (v)): 0, QuantLib's
/// seed from the clock, is refused, naming the parameter. QuantLib's checks keep their texts: the steps given one way
/// at most, the samples beside no tolerance, a tolerance only where the traits estimate an error, and the steps given
/// before the conversion; and the engine is <see cref="MCEuropeanHestonEngine"/>'s with the settings given, bit for
/// bit, its payoff plain vanilla.
/// </summary>
public class MakeMcEuropeanHestonEngineTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    private static readonly HestonProcess Process = new(
        new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.4, -0.6);

    [Fact]
    public void ASeedOfZeroIsRefused()
    {
        ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MakeMCEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(Process, 0));
        Assert.Equal("seed", refused.ParamName);

        var builder = new MakeMCEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(Process, 1);
        Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithSeed(0)).ParamName);
    }

    [Fact]
    public void QuantLibsChecksKeepTheirTexts()
    {
        var builder = new MakeMCEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(Process, 42);

        Assert.Equal(
            "number of steps per year already set",
            Assert.Throws<NanoQLException>(() => builder.WithStepsPerYear(4).WithSteps(2)).Message);
        Assert.Equal(
            "number of steps already set",
            Assert.Throws<NanoQLException>(() => builder.WithSteps(2).WithStepsPerYear(4)).Message);
        Assert.Equal(
            "tolerance already set",
            Assert.Throws<NanoQLException>(() => builder.WithAbsoluteTolerance(0.1).WithSamples(10)).Message);
        Assert.Equal(
            "number of samples already set",
            Assert.Throws<NanoQLException>(() => builder.WithSamples(10).WithAbsoluteTolerance(0.1)).Message);
        Assert.Equal(
            "chosen random generator policy does not allow an error estimate",
            Assert.Throws<NanoQLException>(
                () => new MakeMCEuropeanHestonEngine<LowDiscrepancy, GeneralStatistics>(Process, 42).WithAbsoluteTolerance(0.1))
                .Message);
        Assert.Equal("number of steps not given", Assert.Throws<NanoQLException>(() => builder.WithSamples(10).ToEngine()).Message);
    }

    [Fact]
    public void TheEngineIsTheStaticOnesWithTheSettings()
    {
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 105.0), new EuropeanExercise(Today + 365));

        OneAssetOptionResults built = new MakeMCEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(Process, 7)
            .WithSteps(10)
            .WithSamples(2000)
            .WithAntitheticVariate()
            .ToEngine()(option, Context);
        OneAssetOptionResults direct = MCEuropeanHestonEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, Process, 10, null, true, 2000, null, null, 7);

        Assert.Equal(direct.NPV, built.NPV);
        Assert.Equal(direct.ErrorEstimate, built.ErrorEstimate);

        var digital = new VanillaOption(new CashOrNothingPayoff(OptionType.Call, 105.0, 10.0), new EuropeanExercise(Today + 365));
        Assert.Equal(
            "non-plain payoff given",
            Assert.Throws<NanoQLException>(
                () => MCEuropeanHestonEngine.Calculate<PseudoRandom, GeneralStatistics>(
                    digital, Context, Process, 10, null, false, 100, null, null, 7)).Message);
    }
}
