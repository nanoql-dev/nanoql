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
/// The Monte Carlo barrier engine's builder, constructor and pricers: QuantLib's checks with its texts, each refusal's
/// type, message and parameter asserted, and the seed explicit: 0, which QuantLib reads as a seed drawn from the clock,
/// refused naming the parameter. QuantLib's builder checks the samples beside a tolerance and an error estimate the
/// traits can't give as they are set, and the steps at the conversion to an engine; its engine's constructor checks the
/// steps; its pricers check the strike and the barrier; and the engine checks the payoff and the spot as it prices.
/// </summary>
public class MakeMcBarrierEngineTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly BarrierOption DownOut = new(
        BarrierType.DownOut, 90.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void TheBuilderRefusesAsQuantLibs()
    {
        var builder = new MakeMCBarrierEngine<PseudoRandom, GeneralStatistics>(Process, 42);
        AssertRefused("tolerance already set", () => builder.WithAbsoluteTolerance(0.01).WithSamples(10));
        AssertRefused("number of samples already set", () => builder.WithSamples(10).WithAbsoluteTolerance(0.01));
        AssertRefused(
            "chosen random generator policy does not allow an error estimate",
            () => new MakeMCBarrierEngine<LowDiscrepancy, GeneralStatistics>(Process, 1).WithAbsoluteTolerance(0.01));
        AssertRefused("number of steps not given", () => builder.WithSamples(10).ToEngine());
        AssertRefused("number of steps overspecified", () => builder.WithSteps(10).WithStepsPerYear(10).ToEngine());
        Assert.Equal("samples", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithSamples(-1)).ParamName);
        Assert.Equal("samples", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithMaxSamples(-1)).ParamName);
    }

    [Fact]
    public void SeedZeroIsRefusedNamingTheParameter()
    {
        Assert.Equal(
            "seed",
            Assert.Throws<ArgumentOutOfRangeException>(() => new MakeMCBarrierEngine<PseudoRandom, GeneralStatistics>(Process, 0)).ParamName);
        Assert.Equal(
            "seed",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MakeMCBarrierEngine<PseudoRandom, GeneralStatistics>(Process, 42).WithSeed(0)).ParamName);
        Assert.Equal("seed", Assert.Throws<ArgumentOutOfRangeException>(() => Engine(DownOut, 10, null, 0)).ParamName);
    }

    [Fact]
    public void TheEnginesConstructorRefusesAsQuantLibs()
    {
        AssertArgument("no time steps provided", "timeSteps", () => Engine(DownOut, null, null));
        AssertArgument("both time steps and time steps per year were provided", "timeStepsPerYear", () => Engine(DownOut, 10, 10));
        AssertArgument("timeSteps must be positive, 0 not allowed", "timeSteps", () => Engine(DownOut, 0, null));
        AssertArgument("timeStepsPerYear must be positive, 0 not allowed", "timeStepsPerYear", () => Engine(DownOut, null, 0));
    }

    [Fact]
    public void TheEngineAndItsPricersRefuseAsQuantLibs()
    {
        var cash = new BarrierOption(
            BarrierType.DownOut, 90.0, 0.0, new CashOrNothingPayoff(OptionType.Call, 100.0, 10.0), new EuropeanExercise(Today + 365));
        AssertRefused("non-plain payoff given", () => Engine(cash, 10, null));
        var touched = new BarrierOption(
            BarrierType.UpOut, 95.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        AssertRefused("barrier touched", () => Engine(touched, 10, null));

        double[] discounts = [1.0, 0.95];
        AssertArgument(
            "strike less than zero not allowed",
            "strike",
            () => new BarrierPathPricer(
                BarrierType.DownOut, 90.0, 0.0, OptionType.Call, -1.0, discounts, Process, new MersenneTwisterUniformRsg(1, 5)));
        AssertArgument(
            "barrier less/equal zero not allowed",
            "barrier",
            () => new BarrierPathPricer(
                BarrierType.DownOut, 0.0, 0.0, OptionType.Call, 100.0, discounts, Process, new MersenneTwisterUniformRsg(1, 5)));
        AssertArgument(
            "strike less than zero not allowed",
            "strike",
            () => new BiasedBarrierPathPricer(BarrierType.DownOut, 90.0, 0.0, OptionType.Call, -1.0, discounts));
        AssertArgument(
            "barrier less/equal zero not allowed",
            "barrier",
            () => new BiasedBarrierPathPricer(BarrierType.DownOut, 0.0, 0.0, OptionType.Call, 100.0, discounts));
    }

    private static OneAssetOptionResults Engine(BarrierOption option, int? steps, int? stepsPerYear, long seed = 42) =>
        MCBarrierEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, Process, steps, stepsPerYear, false, false, 100, null, null, false, seed);

    private static void AssertRefused(string message, Action act) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(act).Message);

    private static void AssertArgument(string message, string paramName, Action act)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(act);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message);
    }
}
