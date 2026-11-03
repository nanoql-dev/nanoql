// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The quanto engine's contract: a quanto instrument refuses a plain engine's results with QuantLib's text; the generic
/// core runs on any one-asset instrument through an engine on its terms, a test-side one here; an expired quanto
/// option's quanto sensitivities are 0; and the forward overload strikes its underlying at the moneyness times the
/// spot, the forward engine's strike, after refusing a payoff that isn't plain vanilla.
/// </summary>
public class QuantoEngineContractTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly YieldTermStructure Foreign = new FlatForward(Today, 0.03, A365);
    private static readonly BlackVolTermStructure FxVolatility = new BlackConstantVol(Today, new NullCalendar(), 0.1, A365);

    [Fact]
    public void AQuantoInstrumentRefusesPlainResults()
    {
        var plain = new OneAssetOptionResults { NPV = 1.0 };
        var vanilla = new QuantoVanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        var forward = new QuantoForwardVanillaOption(
            1.0, Today + 91, new PlainVanillaPayoff(OptionType.Call, 0.0), new EuropeanExercise(Today + 365));
        Assert.StartsWith(
            "no quanto results returned from pricing engine",
            Assert.Throws<ArgumentException>(() => vanilla.FetchResults(plain)).Message,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "no quanto results returned from pricing engine",
            Assert.Throws<ArgumentException>(() => forward.FetchResults(plain)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheGenericCoreRunsATestSideInstrument()
    {
        var payoff = new PlainVanillaPayoff(OptionType.Put, 95.0);
        var exercise = new EuropeanExercise(Today + 365);
        OneAssetOptionResults core = QuantoEngine.Calculate(
            new TestOption(payoff, exercise), new VanillaOption(payoff, exercise), Context, Process, Foreign, FxVolatility, 0.3,
            AnalyticEuropeanEngine.Calculate);
        QuantoOptionResults quanto = Assert.IsType<QuantoOptionResults>(core);
        Assert.Equal(
            QuantoEngine.Calculate(
                new QuantoVanillaOption(payoff, exercise), Context, Process, Foreign, FxVolatility, 0.3, AnalyticEuropeanEngine.Calculate),
            quanto);
    }

    [Fact]
    public void AnExpiredQuantoOptionsSensitivitiesAreZero()
    {
        QuantoOptionResults vanilla = QuantoEngine.Calculate(
            new QuantoVanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today - 1)),
            Context, Process, Foreign, FxVolatility, 0.3, AnalyticEuropeanEngine.Calculate);
        QuantoOptionResults forward = QuantoEngine.Calculate(
            new QuantoForwardVanillaOption(1.0, Today - 30, new PlainVanillaPayoff(OptionType.Call, 0.0), new EuropeanExercise(Today - 1)),
            Context, Process, Foreign, FxVolatility, 0.3, (o, c, p) => ForwardVanillaEngine.Calculate(o, c, p, AnalyticEuropeanEngine.Calculate));
        foreach (QuantoOptionResults r in new[] { vanilla, forward })
        {
            Assert.Equal(0.0, r.NPV);
            Assert.Equal(0.0, r.Qvega);
            Assert.Equal(0.0, r.Qrho);
            Assert.Equal(0.0, r.Qlambda);
        }
    }

    [Fact]
    public void TheForwardTwinStrikesItsUnderlyingAtTheMoneynessTimesTheSpot()
    {
        var option = new QuantoForwardVanillaOption(
            1.05, Today + 91, new PlainVanillaPayoff(OptionType.Put, 0.0), new EuropeanExercise(Today + 365));
        var seen = new List<ForwardVanillaOption>();
        QuantoEngine.Calculate(
            option, Context, Process, Foreign, FxVolatility, 0.3,
            (o, c, p) =>
            {
                seen.Add(o);
                return ForwardVanillaEngine.Calculate(o, c, p, AnalyticEuropeanEngine.Calculate);
            });
        ForwardVanillaOption underlying = Assert.Single(seen);
        PlainVanillaPayoff payoff = Assert.IsType<PlainVanillaPayoff>(underlying.Payoff);
        Assert.Equal(OptionType.Put, payoff.OptionType);
        Assert.Equal(105.0, payoff.Strike);
        Assert.Equal(1.05, underlying.Moneyness);
        Assert.Equal(option.ResetDate, underlying.ResetDate);
        Assert.Same(option.Exercise, underlying.Exercise);
    }

    [Fact]
    public void ADigitalForwardQuantoIsRefusedBeforeItsRestrike()
    {
        var option = new QuantoForwardVanillaOption(
            1.05, Today + 91, new CashOrNothingPayoff(OptionType.Call, 0.0, 10.0), new EuropeanExercise(Today + 365));
        int calls = 0;
        NanoQLException e = Assert.Throws<NanoQLException>(() => QuantoEngine.Calculate(
            option, Context, Process, Foreign, FxVolatility, 0.3,
            (o, c, p) =>
            {
                ++calls;
                return ForwardVanillaEngine.Calculate(o, c, p, AnalyticEuropeanEngine.Calculate);
            }));
        Assert.Equal("non-plain payoff given", e.Message);
        Assert.Equal(0, calls);
    }

    // A one-asset instrument of the test's own, priced through the core on a vanilla engine.
    private sealed class TestOption(Payoff payoff, Exercise exercise) : OneAssetOption(payoff, exercise);
}
