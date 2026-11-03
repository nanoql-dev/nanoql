// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Monte Carlo lookback engine's builder and engine: QuantLib's checks with its texts, each refusal's type, message
/// and parameter asserted, and the seed explicit: 0, which QuantLib reads as a seed drawn from the clock, refused
/// naming the parameter. QuantLib's builder checks the samples beside a tolerance and an error estimate the traits
/// can't give as they are set, and the steps at the conversion to an engine; its engine's constructor checks the steps;
/// and the engine checks the payoff and the spot as it prices. The builder's type parameter is the instrument, which
/// QuantLib's template can't instantiate for another type; nanoql's refuses one at the conversion, naming the four it
/// prices.
/// </summary>
public class MakeMcLookbackEngineTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise AYear = new(Today + 365);
    private static readonly GeneralizedBlackScholesProcess Process = ProcessAt(100.0);

    private static readonly ContinuousFixedLookbackOption FixedCall = new(100.0, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);

    [Fact]
    public void TheBuilderRefusesAsQuantLibs()
    {
        var builder = new MakeMCLookbackEngine<ContinuousFixedLookbackOption, PseudoRandom, GeneralStatistics>(Process, 42);
        AssertRefused("tolerance already set", () => builder.WithAbsoluteTolerance(0.01).WithSamples(10));
        AssertRefused("number of samples already set", () => builder.WithSamples(10).WithAbsoluteTolerance(0.01));
        AssertRefused(
            "chosen random generator policy does not allow an error estimate",
            () => new MakeMCLookbackEngine<ContinuousFixedLookbackOption, LowDiscrepancy, GeneralStatistics>(Process, 1).WithAbsoluteTolerance(0.01));
        AssertRefused("number of steps not given", () => builder.WithSamples(10).ToEngine());
        AssertRefused("number of steps overspecified", () => builder.WithSteps(10).WithStepsPerYear(10).ToEngine());
        Assert.Equal("samples", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithSamples(-1)).ParamName);
        Assert.Equal("samples", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithMaxSamples(-1)).ParamName);
    }

    [Fact]
    public void TheBuilderRefusesAnInstrumentItDoesntPrice()
    {
        AssertRefused(
            "no Monte Carlo lookback engine for VanillaOption: the engine prices ContinuousFixedLookbackOption, "
            + "ContinuousFloatingLookbackOption, ContinuousPartialFixedLookbackOption and ContinuousPartialFloatingLookbackOption",
            () => new MakeMCLookbackEngine<VanillaOption, PseudoRandom, GeneralStatistics>(Process, 42).WithSteps(10).WithSamples(10).ToEngine());
    }

    [Fact]
    public void SeedZeroIsRefusedNamingTheParameter()
    {
        Assert.Equal(
            "seed",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MakeMCLookbackEngine<ContinuousFixedLookbackOption, PseudoRandom, GeneralStatistics>(Process, 0)).ParamName);
        Assert.Equal(
            "seed",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MakeMCLookbackEngine<ContinuousFixedLookbackOption, PseudoRandom, GeneralStatistics>(Process, 42).WithSeed(0)).ParamName);
        Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => Engine(FixedCall, Process, 10, null, 0)).ParamName);
    }

    [Fact]
    public void TheEnginesConstructorRefusesAsQuantLibs()
    {
        AssertArgument("no time steps provided", "timeSteps", () => Engine(FixedCall, Process, null, null));
        AssertArgument("both time steps and time steps per year were provided", "timeStepsPerYear", () => Engine(FixedCall, Process, 10, 10));
        AssertArgument("timeSteps must be positive, 0 not allowed", "timeSteps", () => Engine(FixedCall, Process, 0, null));
        AssertArgument("timeStepsPerYear must be positive, 0 not allowed", "timeStepsPerYear", () => Engine(FixedCall, Process, null, 0));
    }

    [Fact]
    public void TheEngineRefusesAsQuantLibs()
    {
        var cash = new ContinuousFixedLookbackOption(100.0, new CashOrNothingPayoff(OptionType.Call, 100.0, 10.0), AYear);
        AssertRefused("non-plain payoff given", () => Engine(cash, Process, 10, null));
        var plainFloating = new ContinuousFloatingLookbackOption(100.0, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);
        AssertRefused(
            "non-floating payoff given",
            () => MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
                plainFloating, Context, Process, 10, null, false, false, 100, null, null, 42));
        AssertRefused("negative or null underlying given", () => Engine(FixedCall, ProcessAt(0.0), 10, null));
    }

    private static OneAssetOptionResults Engine(
        ContinuousFixedLookbackOption option, GeneralizedBlackScholesProcess process, int? steps, int? stepsPerYear, long seed = 42) =>
        MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, process, steps, stepsPerYear, false, false, 100, null, null, seed);

    private static GeneralizedBlackScholesProcess ProcessAt(double spot) => new BlackScholesMertonProcess(
        spot, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static void AssertRefused(string message, Action act) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(act).Message);

    private static void AssertArgument(string message, string paramName, Action act)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(act);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message);
    }
}
