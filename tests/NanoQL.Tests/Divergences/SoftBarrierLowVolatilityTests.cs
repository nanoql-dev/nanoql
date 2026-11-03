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
/// The soft barrier at small volatility (fixed). Haug's form is NaN from σ = 0.02 down at a 4% carry over a year, where
/// its powers overflow. nanoql takes the band average wherever Haug's value isn't finite, its adaptive Gauss-Kronrod
/// rule split at the strike, the spot, the forward and their transition widths. Checks: the band average by Simpson's
/// rule (<see cref="SoftBarrierBandAverage"/>); the deterministic limit, the knocked-in fraction of the band times the
/// forward payoff (a down-in call at 80 on [90, 99], r 1%, q 5%, 5.166684728565), which the average approaches as σ²,
/// 1.0e-5 below it at σ 1e-4 (QUADPACK's value) and 1.0e-7 at 1e-5 (a 200,000-interval Simpson's rule across the step
/// at the forward agreeing with the engine to 6e-14, where QUADPACK, missing the step's width, met the limit itself),
/// so within 1e-10 from σ 1e-7; and QUADPACK's average at σ 1% on the same market, 5.2810970596, where a first-estimate
/// scale at 1e-12 never ends.
/// </summary>
public class SoftBarrierLowVolatilityTests
{
    private static readonly Date Today = new(1, January, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise Exercise = new(Today + 365);

    [Fact]
    public void WhereHaugsFormIsNotANumberTheValueIsTheBandAverage()
    {
        // a down-in call at 100 on [85, 95], r 5%, q 1%, σ 2%
        GeneralizedBlackScholesProcess process = Process(0.05, 0.01, 0.02);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        double value = AnalyticSoftBarrierEngine.Calculate(
            new SoftBarrierOption(BarrierType.DownIn, 85.0, 95.0, payoff, Exercise), Context, process).NPV!.Value;
        Assert.True(double.IsFinite(value));
        Assert.Equal(SoftBarrierBandAverage.KnockIn(BarrierType.DownIn, 85.0, 95.0, payoff, Exercise, Context, process), value, 1e-10);
    }

    [Fact]
    public void AtVanishingVolatilityTheValueIsTheDeterministicLimit()
    {
        // the σ² approach: 1.0155e-7 below the limit at 1e-5, the step at the forward resolved
        Assert.Equal(5.166684627017483, MarketA(1e-5), 1e-12);
        foreach (double sigma in new[] { 1e-7, 1e-8 })
        {
            Assert.Equal(5.166684728565332, MarketA(sigma), 1e-10);
        }
    }

    [Fact]
    public void AtOnePercentTheValueIsQuadpacksAverage()
    {
        Assert.Equal(5.2810970596, MarketA(0.01), 1e-9);
    }

    // The low-volatility market: a down-in call at 80 on [90, 99], r 1%, q 5%, a year
    private static double MarketA(double sigma) => AnalyticSoftBarrierEngine.Calculate(
        new SoftBarrierOption(BarrierType.DownIn, 90.0, 99.0, new PlainVanillaPayoff(OptionType.Call, 80.0), Exercise),
        Context,
        Process(0.01, 0.05, sigma)).NPV!.Value;

    private static GeneralizedBlackScholesProcess Process(double r, double q, double sigma) => new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, q, A365),
        new FlatForward(Today, r, A365),
        new BlackConstantVol(Today, new NullCalendar(), sigma, A365));
}
