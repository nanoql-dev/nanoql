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
/// Levy's engine at an effective strike at or below zero (the S-bar plan's BR5 (xxxi), fixed under R8). A seasoned
/// option's effective strike is X = K − ((T − T₂)/T)·A; QuantLib takes log(X) and prices NaN wherever X ≤ 0 (the
/// planner's BP17; the S-bar run's <c>c3_div_ql_a.py</c>: on spot 100, q 5%, r 10%, σ 15% (Act/360), averaging from 180
/// days ago to 90 days on, the current average 300 or 285 with the call at 95 or 100, every value NaN). nanoql prices
/// the certain exercise, the call Se − X·e^(−rT₂) and the put 0, the formula's own limit as X → 0⁺. Check: the formula
/// at a tiny positive X, where N(d₁) and N(d₂) are 1 in double.
/// </summary>
public class LevyEffectiveStrikeTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.05, A360), new FlatForward(Today, 0.10, A360), new BlackConstantVol(Today, new NullCalendar(), 0.15, A360));

    [Fact]
    public void AnEffectiveStrikeAtOrBelowZeroIsTheCertainExercise()
    {
        double discount = Process.RiskFreeRate.Discount(Today + 90);

        // X = 95 − (2/3)·142.5 = 0, and 95 − (2/3)·142.49999 just above it
        double atZero = Price(OptionType.Call, 142.5);
        Assert.Equal(32.71436924146326, atZero, 1e-12);
        double tinyX = 95.0 - (180.0 / 270.0 * 142.49999);
        Assert.True(tinyX > 0.0);
        Assert.Equal(atZero - (tinyX * discount), Price(OptionType.Call, 142.49999), 1e-10 * atZero);
        Assert.Equal(0.0, Price(OptionType.Put, 142.5));
        Assert.Equal(0.0, Price(OptionType.Put, 142.49999), 1e-10 * atZero);

        // X = 95 − 200 = −105: the call's value moves by −X·D, the put stays 0
        Assert.Equal(atZero + (105.0 * discount), Price(OptionType.Call, 300.0), 1e-12 * atZero);
        Assert.Equal(0.0, Price(OptionType.Put, 300.0));
    }

    private static double Price(OptionType type, double average)
    {
        var option = new ContinuousAveragingAsianOption(
            AverageType.Arithmetic, Today - 180, new PlainVanillaPayoff(type, 95.0), new EuropeanExercise(Today + 90));
        return ContinuousArithmeticAsianLevyEngine.Calculate(option, Context, Process, average).NPV!.Value;
    }
}
