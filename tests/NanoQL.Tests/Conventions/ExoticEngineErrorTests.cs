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
/// C3's engines' refusals (the S-exo plan's ED14; C3's review, B3-3): each check's exception type, QuantLib's text and
/// the parameter it names. Where a check can't fire, the guard before it is asserted: the cliquet and performance
/// engines' European check behind the cliquet option's European exercise, the Black-Scholes calculator's spot and
/// growth checks behind its base's forward check, and the Monte Carlo forward base's control check through an engine
/// of the test's own that offers none.
/// </summary>
public class ExoticEngineErrorTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise Year = new(Today + 365);

    [Fact]
    public void TheCliquetAndPerformanceEnginesChecks()
    {
        var cliquet = new CliquetOption(new PercentageStrikePayoff(OptionType.Call, 1.0), Year, [Today + 91, Today + 182]);
        Assert.Equal("negative or null underlying", Refused(() => AnalyticCliquetEngine.Calculate(cliquet, Context, Process(0.0))));
        Assert.Equal("negative or null underlying", Refused(() => AnalyticPerformanceEngine.Calculate(cliquet, Context, Process(0.0))));

        // "not an European option": the option's exercise is European by its type
        Assert.All(
            typeof(CliquetOption).GetConstructors(),
            c => Assert.Equal(typeof(EuropeanExercise), c.GetParameters().Single(p => p.Name == "maturity").ParameterType));
    }

    [Fact]
    public void TheCompoundAndSimpleChooserEnginesChecks()
    {
        var half = new EuropeanExercise(Today + 182);
        CompoundOption Compound(double strikeMother, double strikeDaughter) => new(
            new PlainVanillaPayoff(OptionType.Call, strikeMother), half, new PlainVanillaPayoff(OptionType.Call, strikeDaughter), Year);
        Assert.Equal(
            "Daughter strike must be positive",
            Refused(() => AnalyticCompoundOptionEngine.Calculate(Compound(5.0, 0.0), Context, Process(100.0))));
        Assert.Equal(
            "Mother strike must be positive",
            Refused(() => AnalyticCompoundOptionEngine.Calculate(Compound(0.0, 100.0), Context, Process(100.0))));
        Assert.Equal(
            "negative or null underlying given",
            Refused(() => AnalyticCompoundOptionEngine.Calculate(Compound(5.0, 100.0), Context, Process(0.0))));

        var chooser = new SimpleChooserOption(Today + 91, 100.0, Year);
        Assert.Equal(
            "negative or null spot value", Refused(() => AnalyticSimpleChooserEngine.Calculate(chooser, Context, Process(0.0))));
        Assert.Equal(
            "negative or null strike value",
            Refused(() => AnalyticSimpleChooserEngine.Calculate(new SimpleChooserOption(Today + 91, 0.0, Year), Context, Process(100.0))));
        Assert.Equal(
            "negative or null volatility",
            Refused(() => AnalyticSimpleChooserEngine.Calculate(chooser, Context, Process(100.0, 0.0))));
        Assert.Equal(
            "choosing date earlier than or equal to evaluation date",
            Refused(() => AnalyticSimpleChooserEngine.Calculate(new SimpleChooserOption(Today, 100.0, Year), Context, Process(100.0))));
    }

    [Fact]
    public void TheForwardAndQuantoEnginesChecks()
    {
        var forward = new ForwardVanillaOption(1.0, Today + 91, new PlainVanillaPayoff(OptionType.Call, 0.0), Year);
        Assert.Equal(
            "negative or null underlying given",
            Refused(() => ForwardVanillaEngine.Calculate(forward, Context, Process(0.0), AnalyticEuropeanEngine.Calculate)));

        var quanto = new QuantoVanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), Year);
        var foreign = new FlatForward(Today, 0.02, A365);
        var fxVol = new BlackConstantVol(Today, new NullCalendar(), 0.1, A365);
        Assert.Equal(
            "negative or null underlying",
            Refused(() => QuantoEngine.Calculate(quanto, Context, Process(0.0), foreign, fxVol, 0.3, AnalyticEuropeanEngine.Calculate)));
        Assert.Equal(
            "non-striked payoff given",
            Refused(() => QuantoEngine.Calculate(
                quanto, new Unstruck(Year), Context, Process(100.0), foreign, fxVol, 0.3, (Unstruck o, EvaluationContext c, GeneralizedBlackScholesProcess p) =>
                    new OneAssetOptionResults())));
    }

    [Fact]
    public void TheCalculatorAndPathPricersChecks()
    {
        // the calculator's growth check: the Black calculator it derives refuses the zero forward first, as QuantLib's
        ArgumentException growth = Assert.Throws<ArgumentException>(
            () => new BlackScholesCalculator(new PlainVanillaPayoff(OptionType.Call, 100.0), 100.0, 0.0, 0.2, 0.95));
        Assert.Equal("forward", growth.ParamName);
        Assert.StartsWith("forward (0) must be positive", growth.Message, StringComparison.Ordinal);

        ArgumentException bs = Assert.Throws<ArgumentException>(() => new ForwardEuropeanBSPathPricer(OptionType.Call, -0.1, 1, 0.95));
        Assert.Equal("moneyness", bs.ParamName);
        Assert.StartsWith("moneyness less than zero not allowed", bs.Message, StringComparison.Ordinal);
        ArgumentException heston = Assert.Throws<ArgumentException>(
            () => new ForwardEuropeanHestonPathPricer(OptionType.Call, -0.1, 1, 0.95));
        Assert.Equal("moneyness", heston.ParamName);
        Assert.StartsWith("moneyness less than zero not allowed", heston.Message, StringComparison.Ordinal);

        Assert.Equal(
            "the path cannot be empty",
            Refused(() => new ForwardEuropeanBSPathPricer(OptionType.Call, 1.0, 0, 0.95).Evaluate(new MonteCarloPath(new NanoQL.TimeGrid()))));
        Assert.Equal(
            "the path cannot be empty",
            Refused(() => new ForwardEuropeanHestonPathPricer(OptionType.Call, 1.0, 0, 0.95).Evaluate(new MultiPath(1, new NanoQL.TimeGrid()))));
    }

    [Fact]
    public void TheMonteCarloForwardBaseRefusesAControlVariateWithoutAnEngine()
    {
        var forward = new ForwardVanillaOption(1.0, Today + 91, new PlainVanillaPayoff(OptionType.Call, 0.0), Year);
        Assert.Equal(
            "engine does not provide control variation pricing engine",
            Refused(() => new NoControlEngine(forward, Process(100.0)).Price()));
    }

    private static GeneralizedBlackScholesProcess Process(double spot, double vol = 0.2) =>
        new BlackScholesMertonProcess(
            spot, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), vol, A365));

    private static string Refused(Action act) => Assert.Throws<NanoQLException>(act).Message;

    // A one-asset instrument with a payoff that isn't striked.
    private sealed class Unstruck(Exercise exercise) : OneAssetOption(new NullPayoff(), exercise);

    // A Monte Carlo forward engine asking for a control variate and offering no control engine.
    private sealed class NoControlEngine(ForwardVanillaOption option, GeneralizedBlackScholesProcess process)
        : MCForwardVanillaEngine<SingleVariate<PseudoRandom>, MonteCarloPath, GeneralStatistics>(
            option, ExoticEngineErrorTests.Context, process, 4, null, false, false, 16, null, null, 42, controlVariate: true)
    {
        public OneAssetOptionResults Price() => Run();

        protected override PathPricer<MonteCarloPath> PathPricer() => new ForwardEuropeanBSPathPricer(OptionType.Call, 1.0, 1, 0.95);

        protected override PathPricer<MonteCarloPath>? ControlPathPricer() => PathPricer();
    }
}
