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
/// Haug's soft-barrier form outside its domain (fixed). The closed form integrates the standard knock-in's branch for a
/// barrier at or below the strike, so it is the average only for a down barrier on a call with U ≤ K, or an up barrier
/// on a put with L ≥ K, the spot outside the band; QuantLib's cases all hold S 100, X 100, U 95. With the band reaching
/// above the strike it is high by up to 1.5%, and with the spot inside the band, the barriers above it already knocked
/// in, it prices 12.286 for 8.803. nanoql takes the band average wherever the band reaches across the strike or the
/// spot is inside it. Check: the band average of QuantLib's analytic barrier engine by Simpson's rule (<see
/// cref="SoftBarrierBandAverage"/>), and a deep out-of-the-money average, a call at 300 with the spot inside the band,
/// below 2.2e-4, which an unscaled tolerance would refuse at construction.
/// </summary>
public class SoftBarrierDomainTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise Exercise = new(Today + 365);

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.02, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    [Fact]
    public void ABandAcrossTheStrikeIsTheBandAverage()
    {
        Check(BarrierType.DownIn, 85.0, 95.0, 90.0);
    }

    [Fact]
    public void ASpotInsideTheBandIsTheBandAverage()
    {
        Check(BarrierType.DownIn, 95.0, 105.0, 100.0);
    }

    [Fact]
    public void ADeepOutOfTheMoneyAverageIsPriced()
    {
        double value = Check(BarrierType.DownIn, 95.0, 105.0, 300.0);
        Assert.True(value > 0.0 && value < 2.2e-4, $"{value}");
    }

    // the tolerance's measure on the average, its scale 1
    private static double Check(BarrierType type, double l, double u, double strike)
    {
        var payoff = new PlainVanillaPayoff(OptionType.Call, strike);
        double value = AnalyticSoftBarrierEngine.Calculate(
            new SoftBarrierOption(type, l, u, payoff, Exercise), Context, Process).NPV!.Value;
        double expected = SoftBarrierBandAverage.KnockIn(type, l, u, payoff, Exercise, Context, Process);
        Assert.True(
            Math.Abs(value - expected) <= 1e-10 * Math.Max(Math.Max(Math.Abs(value), Math.Abs(expected)), 1.0),
            $"{value:R} for {expected:R}");
        return value;
    }
}
