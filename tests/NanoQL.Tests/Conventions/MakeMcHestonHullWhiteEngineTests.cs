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
/// <see cref="MakeMCHestonHullWhiteEngine{TRng, TStats}"/>'s seed is explicit (S2R4; the S-hes plan's HR2 (v)): 0,
/// QuantLib's seed from the clock, is refused, naming the parameter. QuantLib's checks keep their texts: the samples beside
/// no tolerance, a tolerance only where the traits estimate an error, and the steps given exactly one way at the
/// conversion; the engine is <see cref="MCHestonHullWhiteEngine"/>'s with the settings given, bit for bit; and the engine
/// refuses an exercise that isn't European.
/// </summary>
public class MakeMcHestonHullWhiteEngineTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);

    private static readonly HybridHestonHullWhiteProcess Process = new(
        new HestonProcess(RiskFree, new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.4, -0.6),
        new HullWhiteForwardProcess(RiskFree, 0.1, 0.01).WithForwardMeasureTime(2.0),
        -0.4);

    [Fact]
    public void ASeedOfZeroIsRefused()
    {
        ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(Process, 0));
        Assert.Equal("seed", refused.ParamName);

        var builder = new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(Process, 1);
        Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithSeed(0)).ParamName);
    }

    [Fact]
    public void QuantLibsChecksKeepTheirTexts()
    {
        var builder = new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(Process, 42);

        Assert.Equal(
            "number of steps overspecified",
            Assert.Throws<NanoQLException>(() => builder.WithStepsPerYear(4).WithSteps(2).WithSamples(10).ToEngine()).Message);
        Assert.Equal(
            "tolerance already set",
            Assert.Throws<NanoQLException>(() => builder.WithAbsoluteTolerance(0.1).WithSamples(10)).Message);
        Assert.Equal(
            "number of samples already set",
            Assert.Throws<NanoQLException>(() => builder.WithSamples(10).WithAbsoluteTolerance(0.1)).Message);
        Assert.Equal(
            "chosen random generator policy does not allow an error estimate",
            Assert.Throws<NanoQLException>(
                () => new MakeMCHestonHullWhiteEngine<LowDiscrepancy, GeneralStatistics>(Process, 42).WithAbsoluteTolerance(0.1))
                .Message);
        Assert.Equal("number of steps not given", Assert.Throws<NanoQLException>(() => builder.WithSamples(10).ToEngine()).Message);
    }

    [Fact]
    public void TheEngineIsTheStaticOnesWithTheSettings()
    {
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 105.0), new EuropeanExercise(Today + 365));

        OneAssetOptionResults built = new MakeMCHestonHullWhiteEngine<PseudoRandom, GeneralStatistics>(Process, 7)
            .WithSteps(1)
            .WithSamples(2000)
            .WithAntitheticVariate()
            .WithControlVariate()
            .ToEngine()(option, Context);
        OneAssetOptionResults direct = MCHestonHullWhiteEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, Process, 1, null, true, true, 2000, null, null, 7);

        Assert.Equal(direct.NPV, built.NPV);
        Assert.Equal(direct.ErrorEstimate, built.ErrorEstimate);

        var american = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 95.0), new AmericanExercise(Today, Today + 365));
        Assert.Equal(
            "only european exercise is supported",
            Assert.Throws<NanoQLException>(
                () => MCHestonHullWhiteEngine.Calculate<PseudoRandom, GeneralStatistics>(
                    american, Context, Process, 1, null, false, false, 100, null, null, 7)).Message);
    }
}
