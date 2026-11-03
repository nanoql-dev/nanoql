// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The two-asset barrier engine's exercise. QuantLib's engine never reads the exercise's type and prices any exercise
/// to its last date: a down-and-out call struck at 100 on the first asset (spot 100, r 5%, q 1%, σ 20%), barrier 90 on
/// the second (q 2%, σ 30%), ρ 0.5, over a year prices 4.219587870592 for an American exercise, its European value.
/// nanoql refuses a non-European exercise, as the family's European engines do: the formula prices a European exercise
/// only. The European value stands, within a measured 3.3e-5 of QuantLib's (West's bivariate normal for Drezner's).
/// </summary>
public class TwoAssetBarrierExerciseTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ANonEuropeanExerciseIsRefused()
    {
        GeneralizedBlackScholesProcess first = Process(0.01, 0.20);
        GeneralizedBlackScholesProcess second = Process(0.02, 0.30);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        var american = new TwoAssetBarrierOption(BarrierType.DownOut, 90.0, payoff, new AmericanExercise(Today, Today + 365));
        var e = Assert.Throws<NanoQLException>(() => AnalyticTwoAssetBarrierEngine.Calculate(american, Context, first, second, 0.5));
        Assert.Equal("only european style option are supported", e.Message);

        var european = new TwoAssetBarrierOption(BarrierType.DownOut, 90.0, payoff, new EuropeanExercise(Today + 365));
        Assert.Equal(4.219587870592, AnalyticTwoAssetBarrierEngine.Calculate(european, Context, first, second, 0.5).NPV!.Value, 3.3e-5);
    }

    private static GeneralizedBlackScholesProcess Process(double dividend, double volatility) => new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, dividend, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), volatility, A365));
}
