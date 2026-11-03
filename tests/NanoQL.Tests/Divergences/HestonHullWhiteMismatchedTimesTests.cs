// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston-Hull-White engines and process on structures dated or counted differently. QuantLib's finite-difference
/// operator reads the dividend curve at the risk-free curve's times (<c>fdmhestonhullwhiteop.cpp:62</c>), the hybrid
/// process its dividend forward rate likewise (<c>hybridhestonhullwhiteprocess.cpp:126</c>), and the engines take their
/// times from the risk-free curve's date. nanoql's run on the Heston process's model clock, from the earliest reference
/// date of its curves, each read at the instants its times denote, the Hull-White parts on the risk-free curve restated
/// on the clock. QuantLib's check: its engines and process on the market restated on one clock, which nanoql's on the
/// original market reproduce to rounding (QuantLib-Python 1.44 and QuantLib 1.44).
/// </summary>
public class HestonHullWhiteMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly DayCounter A360 = new Actual360();

    private static readonly VanillaOption Put =
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void TheFiniteDifferenceEngineReadsTheDividendCurveAtItsOwnTimes()
    {
        // q 2% on Act/365 beside r 5% on Act/360; QuantLib's restated market: q 2%·360/365 on Act/360
        YieldTermStructure r = new FlatForward(Today, 0.05, A360);
        var model = new HestonModel(Heston(r, new FlatForward(Today, 0.02, A365)));
        double npv = FdHestonHullWhiteVanillaEngine.Calculate(Put, Context, model, new HullWhiteProcess(r, 0.1, 0.01), -0.5, 20, 40, 10, 7).NPV!.Value;
        AssertClose(5.887135159924036, npv, 1e-11); // as given 5.887059036254077
    }

    [Fact]
    public void TheHybridProcessReadsTheDividendRateAtItsOwnTimes()
    {
        YieldTermStructure r = new FlatForward(Today, 0.05, A360);
        var process = new HybridHestonHullWhiteProcess(
            Heston(r, new FlatForward(Today, 0.02, A365)), new HullWhiteForwardProcess(r, 0.1, 0.01).WithForwardMeasureTime(2.0), -0.4);
        Vector y = process.Evolve(0.0, new Vector([100.0, 0.04, 0.0]), 1.0, new Vector([0.3, -1.2, 0.7]));

        // as given [102.31762101658819, -0.0512, 0.012706357731174563]
        double[] restated = [102.34565708190577, -0.051200000000000009, 0.012706357731174563];
        for (int i = 0; i < 3; ++i)
        {
            AssertClose(restated[i], y[i], 4e-15);
        }
    }

    [Fact]
    public void TheAnalyticEnginesRunOnTheEarliestClock()
    {
        // r 5% settling T+2 beside q 2% today; QuantLib's restated market: the risk-free discount 1 to T+2 and 5% after
        YieldTermStructure r = new FlatForward(Today + 2, 0.05, A365);
        var model = new HestonModel(Heston(r, new FlatForward(Today, 0.02, A365)));
        var hullWhite = new HullWhite(r, 0.1, 0.01);
        AssertClose(5.9581851113398585, AnalyticHestonHullWhiteEngine.Calculate(Put, Context, model, hullWhite, 144).NPV!.Value, 1e-11); // as given 5.939768257316999
        AssertClose(6.00838427228738, AnalyticH1HWEngine.Calculate(Put, Context, model, hullWhite, 0.3, 144).NPV!.Value, 1e-11); // as given 5.989555909705068
    }

    // v0 = theta 0.04, kappa 1.5, sigma 0.4, rho -0.6, spot 100
    private static HestonProcess Heston(YieldTermStructure riskFree, YieldTermStructure dividends) =>
        new(riskFree, dividends, 100.0, 0.04, 1.5, 0.04, 0.4, -0.6);

    private static void AssertClose(double expected, double calculated, double tolerance) =>
        Assert.True(
            Math.Abs(calculated - expected) <= tolerance * Math.Abs(expected),
            $"expected {expected}, calculated {calculated}");
}
