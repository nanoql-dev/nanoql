// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
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
/// The Monte Carlo vanilla engines' contract: an engine, the builder's delegate, is a pure function of its inputs and
/// the seed. QuantLib's <c>MCLongstaffSchwartzEngine</c> keeps its path pricer and calibration model in <c>mutable</c>
/// members between calls (<c>mclongstaffschwartzengine.hpp:110-113</c>); here each call builds its own simulation,
/// Longstaff-Schwartz pricer and exercise statistics, so prices on one engine don't depend on the order of the calls or
/// on the threads that make them, and the exercise probability is the call's.
/// </summary>
public class MonteCarloVanillaEngineContractTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        36.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.06, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));

    private static readonly Func<VanillaOption, EvaluationContext, OneAssetOptionResults> American =
        new MakeMCAmericanEngine<PseudoRandom, GeneralStatistics, PseudoRandom>(Process, 42).WithSteps(25).WithSamples(2000).ToEngine();

    private static readonly VanillaOption Atm = new(new PlainVanillaPayoff(OptionType.Put, 36.0), new AmericanExercise(Today, Today + 365));
    private static readonly VanillaOption Itm = new(new PlainVanillaPayoff(OptionType.Put, 44.0), new AmericanExercise(Today, Today + 365));

    [Fact]
    public void TwoPricesOnOneEngineInEitherOrderGiveTheSameBits()
    {
        OneAssetOptionResults atmFirst = American(Atm, Context);
        OneAssetOptionResults itmSecond = American(Itm, Context);
        OneAssetOptionResults itmFirst = American(Itm, Context);
        OneAssetOptionResults atmSecond = American(Atm, Context);
        Assert.Equal(atmFirst, atmSecond);
        Assert.Equal(itmFirst, itmSecond);
        Assert.NotEqual(atmFirst.NPV, itmFirst.NPV);

        Func<VanillaOption, EvaluationContext, OneAssetOptionResults> european =
            new MakeMCEuropeanEngine<PseudoRandom, GeneralStatistics>(Process, 42).WithSteps(10).WithSamples(2000).ToEngine();
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 36.0), new EuropeanExercise(Today + 365));
        Assert.Equal(european(call, Context), european(call, Context));
    }

    [Fact]
    public void EightThreadsGiveOnePrice()
    {
        OneAssetOptionResults reference = American(Atm, Context);
        var prices = new OneAssetOptionResults[8];
        Parallel.For(0, prices.Length, i => prices[i] = American(Atm, Context));
        Assert.All(prices, p => Assert.Equal(reference, p));
    }

    [Fact]
    public void TheExerciseProbabilityIsTheCalls()
    {
        double atm = American(Atm, Context).Result<double>("exerciseProbability");
        double itm = American(Itm, Context).Result<double>("exerciseProbability");
        Assert.True(itm > atm, $"in the money {itm}, at the money {atm}");
        Assert.Equal(atm, American(Atm, Context).Result<double>("exerciseProbability"));
        Assert.Equal(itm, American(Itm, Context).Result<double>("exerciseProbability"));
    }

    [Fact]
    public void ALongstaffSchwartzEngineObjectPricesACallMadeInsideAnother()
    {
        // a deriving engine (the basket engine's form) running a second call on the same object while the first draws
        // its calibration paths: each call prices on its own calibrated pricer, so both calls give a lone call's value
        // and exercise probability, where a pricer kept in a member between calls hands the outer call the inner one's
        // and leaves its own exercise statistics empty
        (double, double) lone = new ReentrantEngine(Atm, reenter: false).Price();
        var engine = new ReentrantEngine(Atm, reenter: true);
        (double, double) outer = engine.Price();
        Assert.Equal(lone, engine.Inner);
        Assert.Equal(lone, outer);
    }

    private sealed class ReentrantEngine(VanillaOption option, bool reenter)
        : MCLongstaffSchwartzEngine<VanillaOption, SingleVariate<PseudoRandom>, SingleVariate<PseudoRandom>, MonteCarloPath, double, GeneralStatistics>(
            option, MonteCarloVanillaEngineContractTests.Context, MonteCarloVanillaEngineContractTests.Process, 25, null, false, false, false, 2000, null, null, 42)
    {
        private bool reentered = !reenter;

        public (double Value, double ExerciseProbability)? Inner { get; private set; }

        public (double Value, double ExerciseProbability) Price()
        {
            (MonteCarloModel<MonteCarloPath, GeneralStatistics> model, double probability) = Run();
            return (model.SampleAccumulator.Mean(), probability);
        }

        protected override LongstaffSchwartzPathPricer<MonteCarloPath, double> LsmPathPricer()
        {
            NanoQL.TimeGrid grid = TimeGrid();
            var american = new AmericanPathPricer(Option.Payoff!, 2, LsmBasisSystemPolynomialType.Monomial);
            IEarlyExercisePathPricer<MonteCarloPath, double> pricer = reentered ? american : new Reentering(american, this);
            return new LongstaffSchwartzPathPricer<MonteCarloPath, double>(
                grid, pricer, MonteCarloVanillaEngineContractTests.Process.RiskFreeRate, ExerciseTimes(grid));
        }

        private void Reenter()
        {
            if (!reentered)
            {
                reentered = true;
                Inner = Price();
            }
        }

        // the outer call's early-exercise pricer: its first evaluation, in the calibration, runs the inner call
        private sealed class Reentering(AmericanPathPricer american, ReentrantEngine engine)
            : IEarlyExercisePathPricer<MonteCarloPath, double>
        {
            public double Evaluate(MonteCarloPath path, int t)
            {
                engine.Reenter();
                return american.Evaluate(path, t);
            }

            public double State(MonteCarloPath path, int t) => american.State(path, t);

            public ImmutableArray<Func<double, double>> BasisSystem() => american.BasisSystem();
        }
    }
}
