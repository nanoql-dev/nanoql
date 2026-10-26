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
/// C3's Monte Carlo builders (S2R4; SEQR2): <see cref="MakeMCForwardEuropeanBSEngine{TRng, TStats}"/>,
/// <see cref="MakeMCForwardEuropeanHestonEngine{TRng, TStats}"/> and <see cref="MakeMCPerformanceEngine{TRng, TStats}"/>
/// take the seed explicitly and refuse 0, QuantLib's seed from the clock, naming the parameter; QuantLib's checks keep
/// their texts; a builder kept and reused is never changed; and the forward engines refuse a payoff that isn't plain
/// vanilla and an exercise that isn't European with QuantLib's texts.
/// </summary>
public class MakeMcForwardEngineTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);
    private static readonly YieldTermStructure Dividends = new FlatForward(Today, 0.02, A365);
    private static readonly GeneralizedBlackScholesProcess BlackScholes = new BlackScholesMertonProcess(
        100.0, Dividends, RiskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly HestonProcess Heston = new(RiskFree, Dividends, 100.0, 0.04, 1.5, 0.04, 0.4, -0.6);

    private static readonly ForwardVanillaOption Forward = new(
        1.0, Today + 91, new PlainVanillaPayoff(OptionType.Call, 0.0), new EuropeanExercise(Today + 365));

    private static readonly CliquetOption Cliquet = new(
        new PercentageStrikePayoff(OptionType.Call, 1.0), new EuropeanExercise(Today + 365), [Today + 91, Today + 182]);

    [Fact]
    public void TheSeedIsRequiredAndZeroRefused()
    {
        Assert.Equal(
            "seed",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MakeMCForwardEuropeanBSEngine<PseudoRandom, GeneralStatistics>(BlackScholes, 0)).ParamName);
        Assert.Equal(
            "seed",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MakeMCForwardEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(Heston, 0)).ParamName);
        Assert.Equal(
            "seed",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MakeMCPerformanceEngine<PseudoRandom, GeneralStatistics>(BlackScholes, 0)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MakeMCForwardEuropeanBSEngine<PseudoRandom, GeneralStatistics>(BlackScholes, 42).WithSeed(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MakeMCForwardEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(Heston, 42).WithSeed(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MakeMCPerformanceEngine<PseudoRandom, GeneralStatistics>(BlackScholes, 42).WithSeed(0));
    }

    [Fact]
    public void TheForwardBuildersKeepQuantLibsChecks()
    {
        var bs = new MakeMCForwardEuropeanBSEngine<PseudoRandom, GeneralStatistics>(BlackScholes, 42);
        var heston = new MakeMCForwardEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(Heston, 42);
        Assert.Equal("number of steps not given", Assert.Throws<NanoQLException>(() => bs.WithSamples(10).ToEngine()).Message);
        Assert.Equal("number of steps not given", Assert.Throws<NanoQLException>(() => heston.WithSamples(10).ToEngine()).Message);
        Assert.Equal(
            "number of steps overspecified - set EITHER steps OR stepsPerYear",
            Assert.Throws<NanoQLException>(() => bs.WithSteps(2).WithStepsPerYear(4).ToEngine()).Message);
        Assert.Equal(
            "number of steps overspecified - set EITHER steps OR stepsPerYear",
            Assert.Throws<NanoQLException>(() => heston.WithSteps(2).WithStepsPerYear(4).ToEngine()).Message);
        Assert.Equal(
            "tolerance already set", Assert.Throws<NanoQLException>(() => bs.WithAbsoluteTolerance(0.1).WithSamples(10)).Message);
        Assert.Equal(
            "number of samples already set",
            Assert.Throws<NanoQLException>(() => heston.WithSamples(10).WithAbsoluteTolerance(0.1)).Message);
        Assert.Equal(
            "chosen random generator policy does not allow an error estimate",
            Assert.Throws<NanoQLException>(
                () => new MakeMCForwardEuropeanBSEngine<LowDiscrepancy, GeneralStatistics>(BlackScholes, 42)
                    .WithAbsoluteTolerance(0.1)).Message);

        // kept and reused, the builder gives the same engine
        Func<ForwardVanillaOption, EvaluationContext, OneAssetOptionResults> engine = bs.WithSteps(4).WithSamples(255).ToEngine();
        Assert.Equal(engine(Forward, Context), bs.WithSteps(4).WithSamples(255).ToEngine()(Forward, Context));
        Assert.Equal(
            heston.WithSteps(4).WithSamples(255).ToEngine()(Forward, Context),
            heston.WithSteps(4).WithSamples(255).ToEngine()(Forward, Context));
        Assert.Equal(
            "neither tolerance nor number of samples set",
            Assert.Throws<NanoQLException>(() => bs.WithSteps(4).ToEngine()(Forward, Context)).Message);
    }

    [Fact]
    public void ThePerformanceBuilderKeepsQuantLibsChecks()
    {
        var builder = new MakeMCPerformanceEngine<PseudoRandom, GeneralStatistics>(BlackScholes, 42);
        Assert.Equal(
            "tolerance already set",
            Assert.Throws<NanoQLException>(() => builder.WithAbsoluteTolerance(0.1).WithSamples(10)).Message);
        Assert.Equal(
            "number of samples already set",
            Assert.Throws<NanoQLException>(() => builder.WithSamples(10).WithAbsoluteTolerance(0.1)).Message);
        Assert.Equal(
            "chosen random generator policy does not allow an error estimate",
            Assert.Throws<NanoQLException>(
                () => new MakeMCPerformanceEngine<LowDiscrepancy, GeneralStatistics>(BlackScholes, 42)
                    .WithAbsoluteTolerance(0.1)).Message);
        Assert.Equal(builder.WithSamples(255).ToEngine()(Cliquet, Context), builder.WithSamples(255).ToEngine()(Cliquet, Context));
        Assert.Equal(
            "neither tolerance nor number of samples set",
            Assert.Throws<NanoQLException>(() => builder.ToEngine()(Cliquet, Context)).Message);
    }

    [Fact]
    public void TheForwardEnginesRefuseANonPlainPayoffAndANonEuropeanExercise()
    {
        var digital = new ForwardVanillaOption(
            1.0, Today + 91, new CashOrNothingPayoff(OptionType.Call, 0.0, 10.0), new EuropeanExercise(Today + 365));
        var american = new ForwardVanillaOption(
            1.0, Today + 91, new PlainVanillaPayoff(OptionType.Call, 0.0), new AmericanExercise(Today, Today + 365));
        foreach (Func<ForwardVanillaOption, EvaluationContext, OneAssetOptionResults> engine in new[]
        {
            new MakeMCForwardEuropeanBSEngine<PseudoRandom, GeneralStatistics>(BlackScholes, 42).WithSteps(4).WithSamples(10).ToEngine(),
            new MakeMCForwardEuropeanHestonEngine<PseudoRandom, GeneralStatistics>(Heston, 42).WithSteps(4).WithSamples(10).ToEngine(),
        })
        {
            Assert.Equal("non-plain payoff given", Assert.Throws<NanoQLException>(() => engine(digital, Context)).Message);
            Assert.Equal("wrong exercise given", Assert.Throws<NanoQLException>(() => engine(american, Context)).Message);
        }
    }
}
