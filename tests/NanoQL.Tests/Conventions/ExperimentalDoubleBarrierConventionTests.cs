// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// C1's refusals and builders (the S-xeq plan's §C1.2): each refusal's type and QuantLib's text, the Monte Carlo
/// builder's required seed (S2R4, XED3), the Heston forward engine's vol-of-vol and reset refusals, and the quanto
/// double barrier's results.
/// </summary>
public class ExperimentalDoubleBarrierConventionTests
{
    private static readonly Date Today = new(15, May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void TheMonteCarloBuilderNeedsASeedAndItsSteps()
    {
        GeneralizedBlackScholesProcess process = Process();
        Assert.Throws<ArgumentOutOfRangeException>(() => new MakeMCDoubleBarrierEngine<PseudoRandom, GeneralStatistics>(process, 0));
        var builder = new MakeMCDoubleBarrierEngine<PseudoRandom, GeneralStatistics>(process, 1);
        Assert.Equal("number of steps not given", Assert.Throws<NanoQLException>(() => builder.ToEngine()).Message);
        Assert.Equal(
            "number of steps overspecified",
            Assert.Throws<NanoQLException>(() => builder.WithSteps(10).WithStepsPerYear(10).ToEngine()).Message);
        Assert.Equal(
            "tolerance already set",
            Assert.Throws<NanoQLException>(() => builder.WithAbsoluteTolerance(0.1).WithSamples(10)).Message);
        Assert.Equal(
            "chosen random generator policy does not allow an error estimate",
            Assert.Throws<NanoQLException>(
                () => new MakeMCDoubleBarrierEngine<LowDiscrepancy, GeneralStatistics>(process, 1).WithAbsoluteTolerance(0.1)).Message);
    }

    [Fact]
    public void TheMonteCarloEngineRefusesWhatItsPathPricerCantPrice()
    {
        Func<DoubleBarrierOption, EvaluationContext, OneAssetOptionResults> engine =
            new MakeMCDoubleBarrierEngine<PseudoRandom, GeneralStatistics>(Process(), 1).WithSteps(10).WithSamples(10).ToEngine();
        Assert.Equal(
            "unknown barrier type",
            Assert.Throws<NanoQLException>(() => engine(Option(DoubleBarrierType.KIKO, 100.0), Context)).Message);
        Assert.Equal(
            "barrier touched",
            Assert.Throws<NanoQLException>(() => engine(Option(DoubleBarrierType.KnockOut, 100.0, 100.0, 120.0), Context)).Message);
        Assert.Throws<ArgumentException>(() => new DoubleBarrierPathPricer(
            DoubleBarrierType.KnockOut, 0.0, 120.0, 0.0, OptionType.Call, 100.0, [1.0, 1.0]));
        Assert.Throws<ArgumentException>(() => new DoubleBarrierPathPricer(
            DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, OptionType.Call, -1.0, [1.0, 1.0]));
    }

    [Fact]
    public void SuoWangRefusesWhatItsFormulasDontCover()
    {
        GeneralizedBlackScholesProcess process = Process();
        Assert.Equal(
            "only KnockIn and KnockOut options supported",
            Assert.Throws<NanoQLException>(
                () => SuoWangDoubleBarrierEngine.Calculate(Option(DoubleBarrierType.KOKI, 100.0), Context, process)).Message);
        Assert.Equal(
            "barrier touched",
            Assert.Throws<NanoQLException>(() => SuoWangDoubleBarrierEngine.Calculate(
                Option(DoubleBarrierType.KnockOut, 100.0, 80.0, 100.0), Context, process)).Message);
        var cash = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, new CashOrNothingPayoff(OptionType.Call, 100.0, 1.0),
            new EuropeanExercise(Today + 182));
        Assert.Equal(
            "non-plain payoff given",
            Assert.Throws<NanoQLException>(() => SuoWangDoubleBarrierEngine.Calculate(cash, Context, process)).Message);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SuoWangDoubleBarrierEngine.Calculate(Option(DoubleBarrierType.KnockOut, 100.0), Context, process, -1));
    }

    [Fact]
    public void TheBinomialEngineAndItsAssetsRefuse()
    {
        GeneralizedBlackScholesProcess process = Process();
        Assert.Throws<ArgumentException>(() => BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDoubleBarrierOption>(
            Option(DoubleBarrierType.KnockOut, 100.0), Context, process, 0));
        Assert.Equal(
            "unsupported barrier type",
            Assert.Throws<NanoQLException>(() => BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniDoubleBarrierOption>(
                Option(DoubleBarrierType.KIKO, 100.0), Context, process, 50)).Message);

        // the plain asset prices the mixed types, as QuantLib's
        double kiko = BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDoubleBarrierOption>(
            Option(DoubleBarrierType.KIKO, 100.0), Context, process, 50).NPV!.Value;
        Assert.True(double.IsFinite(kiko) && kiko >= 0.0);
    }

    [Fact]
    public void TheVannaVolgaEngineRefusesItsQuotesAndMixedTypes()
    {
        var atm = new DeltaVolQuote(0.1, DeltaVolQuoteDeltaType.Fwd, 1.0, DeltaVolQuoteAtmType.AtmDeltaNeutral);
        var put25 = new DeltaVolQuote(-0.25, 0.11, 1.0, DeltaVolQuoteDeltaType.Fwd);
        var call25 = new DeltaVolQuote(0.25, 0.11, 1.0, DeltaVolQuoteDeltaType.Fwd);
        var curve = new FlatForward(Today, 0.01, A365);
        OneAssetOptionResults Run(DoubleBarrierOption o, DeltaVolQuote p, DeltaVolQuote c) => VannaVolgaDoubleBarrierEngine.Calculate(
            o, Context, SuoWangDoubleBarrierEngine.Calculate, atm, p, c, 100.0, curve, curve);
        DoubleBarrierOption ko = Option(DoubleBarrierType.KnockOut, 100.0);
        Assert.Throws<ArgumentException>(() => Run(ko, new DeltaVolQuote(-0.1, 0.11, 1.0, DeltaVolQuoteDeltaType.Fwd), call25));
        Assert.Throws<ArgumentException>(() => Run(ko, put25, new DeltaVolQuote(0.1, 0.11, 1.0, DeltaVolQuoteDeltaType.Fwd)));
        Assert.Throws<ArgumentException>(() => Run(ko, put25, new DeltaVolQuote(0.25, 0.11, 2.0, DeltaVolQuoteDeltaType.Fwd)));
        Assert.Equal(
            "Only same type barrier supported",
            Assert.Throws<NanoQLException>(() => Run(Option(DoubleBarrierType.KIKO, 100.0), put25, call25)).Message);
    }

    [Fact]
    public void ThePerturbativeEngineRefusesWhatItDoesntManage()
    {
        GeneralizedBlackScholesProcess process = Process();
        var exercise = new EuropeanExercise(Today + 182);
        var put = new PlainVanillaPayoff(OptionType.Put, 100.0);
        string Refusal(BarrierOption o) =>
            Assert.Throws<NanoQLException>(() => PerturbativeBarrierOptionEngine.Calculate(o, Context, process)).Message;
        Assert.Equal("this engine only manages up-and-out options", Refusal(new BarrierOption(BarrierType.DownOut, 90.0, 0.0, put, exercise)));
        Assert.Equal("this engine does not manage non-null rebates", Refusal(new BarrierOption(BarrierType.UpOut, 110.0, 1.0, put, exercise)));
        Assert.Equal(
            "this engine only manages put options",
            Refusal(new BarrierOption(BarrierType.UpOut, 110.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0), exercise)));
        var option = new BarrierOption(BarrierType.UpOut, 110.0, 0.0, put, exercise);
        Assert.Throws<ArgumentException>(() => PerturbativeBarrierOptionEngine.Calculate(option, Context, process, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerturbativeBarrierOptionEngine.Calculate(option, Context, process, -1));
        Assert.Throws<ArgumentException>(() => PerturbativeBarrierOptionEngine.Calculate(option, Context, process, 1, false, 0));
        Assert.Throws<ArgumentException>(() => PerturbativeBarrierOptionEngine.Calculate(option, Context, process, 2, false, 10, 0));
    }

    [Fact]
    public void TheHestonEnginesRefuseQuantLibsCases()
    {
        YieldTermStructure r = new FlatForward(Today, 0.03, A365), q = new FlatForward(Today, 0.01, A365);
        var e = Assert.Throws<ArgumentException>(
            () => new AnalyticHestonForwardEuropeanEngine(new HestonProcess(r, q, 100.0, 0.04, 1.5, 0.04, 0.1, -0.5)));
        Assert.StartsWith("Very low values (<~10%) for Heston Vol-of-Vol cause numerical issues", e.Message);
        var engine = new AnalyticHestonForwardEuropeanEngine(new HestonProcess(r, q, 100.0, 0.04, 1.5, 0.04, 0.3, -0.5));
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        Assert.Equal(
            "reset date in the past",
            Assert.Throws<NanoQLException>(() => engine.Calculate(
                new ForwardVanillaOption(1.0, Today - 1, payoff, new EuropeanExercise(Today + 365)), Context)).Message);
        Assert.Equal(
            "not an European option",
            Assert.Throws<NanoQLException>(() => engine.Calculate(
                new ForwardVanillaOption(1.0, Today + 30, payoff, new AmericanExercise(Today, Today + 365)), Context)).Message);
        var asian = new AnalyticContinuousGeometricAveragePriceAsianHestonEngine(
            new HestonProcess(r, q, 100.0, 0.04, 1.5, 0.04, 0.3, -0.5));
        Assert.Equal(
            "not a geometric average option",
            Assert.Throws<NanoQLException>(() => asian.Calculate(
                new ContinuousAveragingAsianOption(AverageType.Arithmetic, payoff, new EuropeanExercise(Today + 365)), Context)).Message);
    }

    [Fact]
    public void TheQuantoDoubleBarrierReturnsQuantoResults()
    {
        var option = new QuantoDoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0),
            new EuropeanExercise(Today - 1));
        QuantoOptionResults expired = option.SetupExpired();
        Assert.Equal(0.0, expired.Qvega);
        Assert.Equal(0.0, expired.Qrho);
        Assert.Equal(0.0, expired.Qlambda);
        Assert.Throws<ArgumentException>(() => option.FetchResults(new OneAssetOptionResults { NPV = 1.0 }));
    }

    private static DoubleBarrierOption Option(DoubleBarrierType type, double strike, double lo = 80.0, double hi = 120.0) =>
        new(type, lo, hi, 0.0, new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Today + 182));

    private static GeneralizedBlackScholesProcess Process() =>
        new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
}
