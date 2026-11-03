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

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo barrier engine's exercise. QuantLib's engine never reads the exercise's type and prices any exercise
/// to its last date: a down-and-out put struck at 100, barrier 80, on spot 100, r 5%, q 1%, σ 20% over a year, 20
/// steps, 20,000 pseudo-random samples at seed 42, prices 1.670455625787 for an American exercise, its European value,
/// where QuantLib's binomial engine (Cox-Ross-Rubinstein, Derman and Kani's asset, 400 steps) prices the American at
/// 6.364401494576 and the European at 1.674490747474. nanoql refuses a non-European exercise, as the family's European
/// engines do: the paths price a European exercise only, early exercise wanting a regression. The binomial engine
/// prices American barrier options and stays.
/// </summary>
public class McBarrierExerciseTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.01, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));

    [Fact]
    public void ANonEuropeanExerciseIsRefused()
    {
        Func<BarrierOption, EvaluationContext, OneAssetOptionResults> engine =
            new MakeMCBarrierEngine<PseudoRandom, GeneralStatistics>(Process, 42).WithSteps(20).WithSamples(20000).ToEngine();
        foreach (Exercise exercise in new Exercise[] { new AmericanExercise(Today, Today + 365), new BermudanExercise([Today + 182, Today + 365]) })
        {
            var e = Assert.Throws<NanoQLException>(() => engine(Option(exercise), Context));
            Assert.Equal("only european style option are supported", e.Message);
        }

        Assert.Equal(1.670455625787, engine(Option(new EuropeanExercise(Today + 365)), Context).NPV!.Value, 1e-10);

        // the binomial engine's American, which the Monte Carlo's QuantLib value misses by 74%
        double binomial = BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniBarrierOption>(
            Option(new AmericanExercise(Today, Today + 365)), Context, Process, 400).NPV!.Value;
        Assert.Equal(6.364401494576, binomial, 1e-10);
    }

    private static BarrierOption Option(Exercise exercise) =>
        new(BarrierType.DownOut, 80.0, 0.0, new PlainVanillaPayoff(OptionType.Put, 100.0), exercise);
}
