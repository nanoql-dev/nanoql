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
/// The soft barrier at Haug's singular carries (fixed). Haug's term 1 divides by 2(μ + ½), zero at b = −σ²
/// (<c>analyticsoftbarrierengine.cpp:141-142</c>): 47.207 at r 0, q 4%, σ 20% where both sides give 1.37353; around
/// both singular points, b = 0 and b = −σ², its double cancels beyond the tolerance out to about 1e-5 of the carry, and
/// QuantLib's nudge at |r − q| &lt; 1e-10, r moved by 1e-6, is itself off by 1.2e-6 to 4.2e-6. nanoql takes the band
/// average, the soft barrier's definition, wherever |b| or |b + σ²| is below 2e-3, superseding the nudge. Check: the
/// band average of QuantLib's analytic barrier engine, by Simpson's rule (<see cref="SoftBarrierBandAverage"/>).
/// </summary>
public class SoftBarrierSingularCarryTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise Exercise = new(Today + 365);

    [Fact]
    public void AtACarryOfMinusTheVarianceRateTheValueIsTheBandAverage()
    {
        // r 0, q 4%, σ 20%: QuantLib 47.207
        Check(BarrierType.DownIn, 0.0, 0.04, 0.20);
    }

    [Fact]
    public void AtZeroCarryTheValueIsTheBandAverage()
    {
        // r = q = 3%: QuantLib moves r by 1e-6
        Check(BarrierType.DownOut, 0.03, 0.03, 0.20);
    }

    private static void Check(BarrierType type, double r, double q, double sigma)
    {
        var process = new BlackScholesMertonProcess(
            100.0,
            new FlatForward(Today, q, A365),
            new FlatForward(Today, r, A365),
            new BlackConstantVol(Today, new NullCalendar(), sigma, A365));
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        double value = AnalyticSoftBarrierEngine.Calculate(
            new SoftBarrierOption(type, 85.0, 95.0, payoff, Exercise), Context, process).NPV!.Value;
        double knockIn = SoftBarrierBandAverage.KnockIn(BarrierType.DownIn, 85.0, 95.0, payoff, Exercise, Context, process);
        double expected = type == BarrierType.DownIn
            ? knockIn
            : AnalyticEuropeanEngine.Calculate(new VanillaOption(payoff, Exercise), Context, process).NPV!.Value - knockIn;
        Assert.Equal(expected, value, 1e-9);
    }
}
