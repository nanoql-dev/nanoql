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
/// The Monte Carlo Asian engines' builders and engines (the S-bar plan's BR1 (iii)): QuantLib's checks with its texts,
/// each refusal's type, message and parameter asserted (the S-van plan's VD19), and the seed explicit: 0, which QuantLib
/// reads as a seed drawn from the clock, refused naming the parameter (S2R4). QuantLib's builders check the samples
/// beside a tolerance and an error estimate the traits can't give as they are set, its Heston builders the steps beside
/// the steps a year; its Heston engines' constructors refuse both; and every engine refuses every fixing in the past, or
/// the one left today, with QuantLib's "all fixings are in the past", the text of its internal
/// <c>detail::PastFixingsOnly</c> (SD14).
/// </summary>
public class MakeMcAsianEngineTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly HestonProcess Heston = new(
        new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.3, -0.7);

    [Fact]
    public void TheBlackScholesBuildersRefuseAsQuantLibs()
    {
        var arithmetic = new MakeMCDiscreteArithmeticAPEngine<PseudoRandom, GeneralStatistics>(Process, 42);
        AssertRefused("tolerance already set", () => arithmetic.WithAbsoluteTolerance(0.01).WithSamples(10));
        AssertRefused("number of samples already set", () => arithmetic.WithSamples(10).WithAbsoluteTolerance(0.01));
        AssertRefused(
            "chosen random generator policy does not allow an error estimate",
            () => new MakeMCDiscreteArithmeticAPEngine<LowDiscrepancy, GeneralStatistics>(Process, 1).WithAbsoluteTolerance(0.01));
        var strike = new MakeMCDiscreteArithmeticASEngine<PseudoRandom, GeneralStatistics>(Process, 42);
        AssertRefused("tolerance already set", () => strike.WithAbsoluteTolerance(0.01).WithSamples(10));
        AssertRefused("number of samples already set", () => strike.WithSamples(10).WithAbsoluteTolerance(0.01));
        var geometric = new MakeMCDiscreteGeometricAPEngine<PseudoRandom, GeneralStatistics>(Process, 42);
        AssertRefused("tolerance already set", () => geometric.WithAbsoluteTolerance(0.01).WithSamples(10));
        AssertRefused("number of samples already set", () => geometric.WithSamples(10).WithAbsoluteTolerance(0.01));
        Assert.Equal("samples", Assert.Throws<ArgumentOutOfRangeException>(() => arithmetic.WithSamples(-1)).ParamName);
        Assert.Equal("samples", Assert.Throws<ArgumentOutOfRangeException>(() => geometric.WithMaxSamples(-1)).ParamName);
    }

    [Fact]
    public void TheHestonBuildersRefuseAsQuantLibs()
    {
        var arithmetic = new MakeMCDiscreteArithmeticAPHestonEngine<PseudoRandom, GeneralStatistics>(Heston, 42);
        AssertRefused("number of steps per year already set", () => arithmetic.WithStepsPerYear(10).WithSteps(10));
        AssertRefused("number of steps already set", () => arithmetic.WithSteps(10).WithStepsPerYear(10));
        AssertRefused("tolerance already set", () => arithmetic.WithAbsoluteTolerance(0.01).WithSamples(10));
        var geometric = new MakeMCDiscreteGeometricAPHestonEngine<PseudoRandom, GeneralStatistics>(Heston, 42);
        AssertRefused("number of steps per year already set", () => geometric.WithStepsPerYear(10).WithSteps(10));
        AssertRefused("number of steps already set", () => geometric.WithSteps(10).WithStepsPerYear(10));
        AssertRefused(
            "chosen random generator policy does not allow an error estimate",
            () => new MakeMCDiscreteGeometricAPHestonEngine<LowDiscrepancy, GeneralStatistics>(Heston, 1).WithAbsoluteTolerance(0.01));
        Assert.Equal("steps", Assert.Throws<ArgumentOutOfRangeException>(() => geometric.WithSteps(-1)).ParamName);
    }

    [Fact]
    public void TheHestonEnginesRefuseBothStepSettings()
    {
        DiscreteAveragingAsianOption option = Option(AverageType.Arithmetic, [Today + 90, Today + 180]);
        ArgumentException e = Assert.Throws<ArgumentException>(() => MCDiscreteArithmeticAPHestonEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, Heston, false, 10, null, null, 42, 10, 10));
        Assert.Equal("timeStepsPerYear", e.ParamName);
        Assert.StartsWith("both time steps and time steps per year were provided", e.Message);
        e = Assert.Throws<ArgumentException>(() => MCDiscreteGeometricAPHestonEngine.Calculate<PseudoRandom, GeneralStatistics>(
            Option(AverageType.Geometric, [Today + 90, Today + 180]), Context, Heston, false, 10, null, null, 42, 10, 10));
        Assert.Equal("timeStepsPerYear", e.ParamName);
    }

    [Fact]
    public void SeedZeroIsRefusedNamingTheParameter()
    {
        Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => new MakeMCDiscreteArithmeticAPEngine<PseudoRandom, GeneralStatistics>(Process, 0)).ParamName);
        Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => new MakeMCDiscreteGeometricAPHestonEngine<PseudoRandom, GeneralStatistics>(Heston, 42).WithSeed(0)).ParamName);
        Assert.Equal(
            "seed",
            Assert.Throws<ArgumentOutOfRangeException>(() => MCDiscreteGeometricAPEngine.Calculate<PseudoRandom, GeneralStatistics>(
                Option(AverageType.Geometric, [Today + 90]), Context, Process, false, false, 10, null, null, 0)).ParamName);
    }

    [Fact]
    public void EveryEngineRefusesEveryFixingInThePast()
    {
        Date[][] schedules = [[Today - 20, Today - 10], [Today - 10, Today]];
        foreach (Date[] fixings in schedules)
        {
            DiscreteAveragingAsianOption arithmetic = Option(AverageType.Arithmetic, fixings, 190.0, 2);
            DiscreteAveragingAsianOption geometric = Option(AverageType.Geometric, fixings, 9000.0, 2);
            Action[] engines =
            [
                () => MCDiscreteArithmeticAPEngine.Calculate<PseudoRandom, GeneralStatistics>(arithmetic, Context, Process, false, false, false, 10, null, null, 42),
                () => MCDiscreteArithmeticASEngine.Calculate<PseudoRandom, GeneralStatistics>(arithmetic, Context, Process, false, false, 10, null, null, 42),
                () => MCDiscreteGeometricAPEngine.Calculate<PseudoRandom, GeneralStatistics>(geometric, Context, Process, false, false, 10, null, null, 42),
                () => MCDiscreteArithmeticAPHestonEngine.Calculate<PseudoRandom, GeneralStatistics>(arithmetic, Context, Heston, false, 10, null, null, 42),
                () => MCDiscreteGeometricAPHestonEngine.Calculate<PseudoRandom, GeneralStatistics>(geometric, Context, Heston, false, 10, null, null, 42),
            ];
            foreach (Action engine in engines)
            {
                AssertRefused("all fixings are in the past", engine);
            }
        }
    }

    private static DiscreteAveragingAsianOption Option(AverageType average, Date[] fixings, double running = 0.0, int past = 0) =>
        new(average, running, past, fixings, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));

    private static void AssertRefused(string message, Action act) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(act).Message);
}
