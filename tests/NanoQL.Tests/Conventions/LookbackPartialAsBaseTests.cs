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
/// A partial-time lookback passed where its full-period base is taken (the S-bar plan's SD8, U15). QuantLib's partial
/// options derive from the full ones, and its full engines' arguments can't hold a partial option's ("wrong argument
/// type" at <c>setupArguments</c>); nanoql's partial options derive from the unsealed full ones too, so the two full
/// analytic engines, the Monte Carlo engine's full-period overloads and the builder's delegate for a full option each
/// refuse one with QuantLib's text, rather than price it as a full-period option.
/// </summary>
public class LookbackPartialAsBaseTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise AYear = new(Today + 365);
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly ContinuousFloatingLookbackOption PartialFloating =
        new ContinuousPartialFloatingLookbackOption(100.0, 1.0, Today + 180, new FloatingTypePayoff(OptionType.Call), AYear);

    private static readonly ContinuousFixedLookbackOption PartialFixed =
        new ContinuousPartialFixedLookbackOption(Today + 180, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);

    [Fact]
    public void TheFullFloatingEngineRefusesAPartialOption() =>
        AssertWrongType(() => AnalyticContinuousFloatingLookbackEngine.Calculate(PartialFloating, Context, Process));

    [Fact]
    public void TheFullFixedEngineRefusesAPartialOption() =>
        AssertWrongType(() => AnalyticContinuousFixedLookbackEngine.Calculate(PartialFixed, Context, Process));

    [Fact]
    public void TheMonteCarloFullPeriodOverloadsRefuseAPartialOption()
    {
        AssertWrongType(() => MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            PartialFloating, Context, Process, 10, null, false, false, 100, null, null, 42));
        AssertWrongType(() => MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            PartialFixed, Context, Process, 10, null, false, false, 100, null, null, 42));
    }

    [Fact]
    public void TheBuildersFullPeriodDelegateRefusesAPartialOption()
    {
        Func<ContinuousFixedLookbackOption, EvaluationContext, OneAssetOptionResults> fixedEngine =
            new MakeMCLookbackEngine<ContinuousFixedLookbackOption, PseudoRandom, GeneralStatistics>(Process, 42).WithSteps(10).WithSamples(100).ToEngine();
        AssertWrongType(() => fixedEngine(PartialFixed, Context));
        Func<ContinuousFloatingLookbackOption, EvaluationContext, OneAssetOptionResults> floatingEngine =
            new MakeMCLookbackEngine<ContinuousFloatingLookbackOption, PseudoRandom, GeneralStatistics>(Process, 42).WithSteps(10).WithSamples(100).ToEngine();
        AssertWrongType(() => floatingEngine(PartialFloating, Context));
    }

    private static void AssertWrongType(Action act) =>
        Assert.Equal("wrong argument type", Assert.Throws<NanoQLException>(act).Message);
}
