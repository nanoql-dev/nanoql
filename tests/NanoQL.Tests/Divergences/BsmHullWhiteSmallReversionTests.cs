// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The BSM-Hull-White engine's variance offset at small mean reversion (§3.3's small-reversion class). QuantLib's
/// closed form, v = σ²/a²·(t + 2e^(−at)/a − e^(−2at)/(2a) − 3/(2a)) and μ = 2ρση/a·(t − (1 − e^(−at))/a)
/// (<c>analyticbsmhullwhiteengine.cpp:97-110</c>), cancels to σ²t³/3 and ρσηt² with a relative error of about
/// 3ε/(a·t)³, used down to (a·t)⁴ = ε. nanoql takes the series to (a·t)⁴ below a·t = 1e-2 and the closed form above.
/// QuantLib's check: its own closed form evaluated to 50 digits (Python's decimal) on both sides of the cut, priced by
/// QuantLib's analytic European engine on the total variance η²t + v + μ (QuantLib-Python 1.44).
/// </summary>
public class BsmHullWhiteSmallReversionTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void BelowTheCutTheSeriesIsTheClosedFormsExactValue()
    {
        // a·t = 5e-3: the exact total variance 0.5330421659384413; QuantLib's engine 32.516295104153265, 2e-11 off
        AssertExact(5e-4, 32.5162951035221);
    }

    [Fact]
    public void AboveTheCutTheClosedFormIsKept()
    {
        // a·t = 2e-2: the exact total variance 0.5321746202401476 and the price 32.501928361198566, which QuantLib's
        // closed form reaches to its 3ε/(a·t)³, 8e-11 of the offset and 5e-13 of the price; its bits are kept
        Assert.Equal(32.50192836118365, Price(2e-3));
        Assert.True(Math.Abs(Price(2e-3) - 32.501928361198566) <= 1e-12 * 32.501928361198566);
    }

    private static void AssertExact(double a, double exact)
    {
        double value = Price(a);
        Assert.True(Math.Abs(value - exact) <= 1e-13 * exact, $"{value} against the exact {exact}");
    }

    // a ten-year call at 100 on a spot of 100, q 2%, r 5%, η 20%, the Hull-White σ 1% and ρ 0.5
    private static double Price(double a)
    {
        YieldTermStructure riskFree = new FlatForward(Today, 0.05, A365);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), riskFree, new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));
        var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 3650));
        return AnalyticBSMHullWhiteEngine.Calculate(option, Context, 0.5, process, new HullWhite(riskFree, a, 0.01)).NPV!.Value;
    }
}
