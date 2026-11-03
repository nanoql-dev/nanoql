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
/// The Monte Carlo vanilla engines' builders and constructors: QuantLib's checks with its texts, each refusal's type,
/// message and parameter asserted. QuantLib's builders check the samples beside a tolerance and an error estimate the
/// traits can't give as they are set, and the steps at the conversion to an engine; its engines' constructors check the
/// steps; and the engines check the payoff and exercise as they price.
/// </summary>
public class MakeMcEngineTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        36.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.06, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));

    [Fact]
    public void TheBuildersRefuseAsQuantLibs()
    {
        var european = new MakeMCEuropeanEngine<PseudoRandom, GeneralStatistics>(Process, 42);
        var digital = new MakeMCDigitalEngine<PseudoRandom, GeneralStatistics>(Process, 42);
        var american = new MakeMCAmericanEngine<PseudoRandom, GeneralStatistics, PseudoRandom>(Process, 42);

        AssertRefused("tolerance already set", () => european.WithAbsoluteTolerance(0.01).WithSamples(10));
        AssertRefused("number of samples already set", () => european.WithSamples(10).WithAbsoluteTolerance(0.01));
        AssertRefused("tolerance already set", () => digital.WithAbsoluteTolerance(0.01).WithSamples(10));
        AssertRefused("number of samples already set", () => digital.WithSamples(10).WithAbsoluteTolerance(0.01));
        AssertRefused("tolerance already set", () => american.WithAbsoluteTolerance(0.01).WithSamples(10));
        AssertRefused("number of samples already set", () => american.WithSamples(10).WithAbsoluteTolerance(0.01));

        const string NoEstimate = "chosen random generator policy does not allow an error estimate";
        AssertRefused(NoEstimate, () => new MakeMCEuropeanEngine<LowDiscrepancy, GeneralStatistics>(Process, 1).WithAbsoluteTolerance(0.01));
        AssertRefused(NoEstimate, () => new MakeMCDigitalEngine<LowDiscrepancy, GeneralStatistics>(Process, 1).WithAbsoluteTolerance(0.01));
        AssertRefused(
            NoEstimate,
            () => new MakeMCAmericanEngine<LowDiscrepancy, GeneralStatistics, PseudoRandom>(Process, 1).WithAbsoluteTolerance(0.01));

        AssertRefused("number of steps not given", () => european.WithSamples(10).ToEngine());
        AssertRefused("number of steps overspecified", () => european.WithSteps(10).WithStepsPerYear(10).ToEngine());
        AssertRefused("number of steps not given", () => digital.WithSamples(10).ToEngine());
        AssertRefused("number of steps overspecified", () => digital.WithSteps(10).WithStepsPerYear(10).ToEngine());
        AssertRefused("number of steps not given", () => american.WithSamples(10).ToEngine());
        AssertRefused("number of steps overspecified", () => american.WithSteps(10).WithStepsPerYear(10).ToEngine());

        Assert.Equal("samples", Assert.Throws<ArgumentOutOfRangeException>(() => european.WithSamples(-1)).ParamName);
        Assert.Equal("samples", Assert.Throws<ArgumentOutOfRangeException>(() => digital.WithMaxSamples(-1)).ParamName);
        Assert.Equal(
            "calibrationSamples", Assert.Throws<ArgumentOutOfRangeException>(() => american.WithCalibrationSamples(-1)).ParamName);
    }

    [Fact]
    public void TheEnginesConstructorsRefuseAsQuantLibs()
    {
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 40.0), new EuropeanExercise(Today + 365));
        AssertArgument("no time steps provided", "timeSteps", () => European(put, null, null));
        AssertArgument("both time steps and time steps per year were provided", "timeStepsPerYear", () => European(put, 10, 10));
        AssertArgument("timeSteps must be positive, 0 not allowed", "timeSteps", () => European(put, 0, null));
        AssertArgument("timeStepsPerYear must be positive, 0 not allowed", "timeStepsPerYear", () => European(put, null, 0));
        var american = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 40.0), new AmericanExercise(Today, Today + 365));
        AssertArgument("no time steps provided", "timeSteps", () => American(american, null));
        AssertArgument("timeSteps must be positive, 0 not allowed", "timeSteps", () => American(american, 0));
    }

    [Fact]
    public void TheEnginesRefuseTheirPayoffsAndExercisesAsQuantLibs()
    {
        var cash = new CashOrNothingPayoff(OptionType.Put, 40.0, 10.0);
        AssertRefused("non-plain payoff given", () => European(new VanillaOption(cash, new EuropeanExercise(Today + 365)), 10, null));
        AssertRefused(
            "wrong payoff given",
            () => Digital(new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 40.0), new AmericanExercise(Today, Today + 365))));
        AssertRefused("wrong exercise given", () => Digital(new VanillaOption(cash, new EuropeanExercise(Today + 365))));
        AssertRefused(
            "wrong exercise given",
            () => American(new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 40.0), new EuropeanExercise(Today + 365)), 10));
        AssertRefused(
            "payoff at expiry not handled",
            () => American(new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 40.0), new AmericanExercise(Today, Today + 365, true)), 10));
        AssertArgument(
            "insufficient polynomial type",
            "polynomialType",
            () => MCAmericanEngine.Calculate<PseudoRandom, GeneralStatistics, PseudoRandom>(
                new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 40.0), new AmericanExercise(Today, Today + 365)),
                Context, Process, 10, null, false, false, 100, null, null, 42, 2, LsmBasisSystemPolynomialType.Legendre));
    }

    private static OneAssetOptionResults European(VanillaOption option, int? steps, int? stepsPerYear) =>
        MCEuropeanEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, Process, steps, stepsPerYear, false, false, 100, null, null, 42);

    private static OneAssetOptionResults Digital(VanillaOption option) =>
        MCDigitalEngine.Calculate<PseudoRandom, GeneralStatistics>(option, Context, Process, 10, null, false, false, 100, null, null, 42);

    private static OneAssetOptionResults American(VanillaOption option, int? steps) =>
        MCAmericanEngine.Calculate<PseudoRandom, GeneralStatistics, PseudoRandom>(
            option, Context, Process, steps, null, false, false, 100, null, null, 42, 2, LsmBasisSystemPolynomialType.Monomial);

    private static void AssertRefused(string message, Action act) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(act).Message);

    private static void AssertArgument(string message, string paramName, Action act)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(act);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message);
    }
}
