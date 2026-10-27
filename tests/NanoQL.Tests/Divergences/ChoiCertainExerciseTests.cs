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
/// Choi's engine at a negative effective strike (the S-bar plan's BR7 (a), a refusal lifted). QuantLib's Choi engine
/// refuses it ("effective strike should to be positive", <c>choiasianengine.cpp:92</c>), where its Turnbull-Wakeman
/// engine prices the certain exercise in closed form, the discounted forward of the average less the strike: on spot
/// 100, q 3%, r 6%, σ 20% (Act/360), six past fixings summing to 900 and six monthly fixings to come, a call at 60
/// prices 63.505838672811066 by Turnbull-Wakeman where QuantLib's Choi throws (the S-bar run's <c>c3_div_ql_a.py</c>).
/// nanoql's Choi engine prices it by Turnbull-Wakeman's branch, its value alone, as its other branches set. Check:
/// Turnbull-Wakeman's value.
/// </summary>
public class ChoiCertainExerciseTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.03, A360), new FlatForward(Today, 0.06, A360), new BlackConstantVol(Today, new NullCalendar(), 0.20, A360));

    [Fact]
    public void ChoiPricesTheCertainExerciseAsTurnbullWakeman()
    {
        var option = new DiscreteAveragingAsianOption(
            AverageType.Arithmetic,
            900.0,
            6,
            [.. Enumerable.Range(1, 6).Select(i => Today + (30 * i))],
            new PlainVanillaPayoff(OptionType.Call, 60.0),
            new EuropeanExercise(Today + 180));
        OneAssetOptionResults choi = ChoiAsianEngine.Calculate(option, Context, Process, 10, 2 << 12);
        OneAssetOptionResults tw = TurnbullWakemanAsianEngine.Calculate(option, Context, Process);
        Assert.Equal(63.505838672811066, tw.NPV!.Value, 1e-12);
        Assert.Equal(tw.NPV, choi.NPV);
        Assert.Null(choi.Delta);
        Assert.Null(choi.Gamma);
    }
}
