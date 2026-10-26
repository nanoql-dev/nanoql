// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo barrier engine at seed 5 (the S-bar plan's BR5 (v), fixed under R8). QuantLib's bridge draws its
/// uniforms from a constant MT(5) of the path's dimension, so on pseudo-random paths at seed 5 each step's increment
/// and its extreme read the same uniforms: a down-and-out call at 100, barrier 95, r 5%, σ 30% over a year on 10 steps
/// and 200,000 samples prices 6.860215 ± 0.042713, +31.9 standard errors above the analytic 5.498097, where seeds 4 and
/// 6 are within 1.6 (the planner's P3b; the S-bar run's <c>c1_seed5.py</c>). nanoql's bridge reads MT(6) at seed 5 on
/// the Mersenne Twister traits. Low-discrepancy paths share nothing with MT(5), so they keep QuantLib's stream and its
/// values, <c>testBabsiriValues</c>' among them (Revision 2, O1-3). Checks: the analytic barrier engine, and QuantLib's
/// low-discrepancy values at seed 5.
/// </summary>
public class McBarrierSeed5Tests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void PseudoRandomPathsAtSeedFiveAreUnbiased()
    {
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.0, dc), new FlatForward(Today, 0.05, dc),
            new BlackConstantVol(Today, new NullCalendar(), 0.3, dc));
        var option = new BarrierOption(
            BarrierType.DownOut, 95.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        double analytic = AnalyticBarrierEngine.Calculate(option, Context, process).NPV!.Value;
        Assert.Equal(5.4980967987369525, analytic, 1e-12);

        OneAssetOptionResults mc = new MakeMCBarrierEngine<PseudoRandom, GeneralStatistics>(process, 5)
            .WithSteps(10)
            .WithSamples(200000)
            .ToEngine()(option, Context);
        double deviation = (mc.NPV!.Value - analytic) / mc.ErrorEstimate!.Value;
        Assert.True(Math.Abs(deviation) <= 3.0, $"{mc.NPV} ± {mc.ErrorEstimate}: {deviation:F2} standard errors");
    }

    [Fact]
    public void LowDiscrepancyPathsAtSeedFiveKeepQuantLibsValues()
    {
        DayCounter dc = new Actual360();
        foreach ((BarrierType type, double barrier, double sigma, double quantLib) in new[]
        {
            (BarrierType.DownIn, 90.0, 0.30, 4.55698499274094),
            (BarrierType.UpIn, 110.0, 0.10, 4.794477481200488),
        })
        {
            var process = new BlackScholesMertonProcess(
                100.0, new FlatForward(Today, 0.02, dc), new FlatForward(Today, 0.05, dc),
                new BlackConstantVol(Today, new NullCalendar(), sigma, dc));
            var option = new BarrierOption(
                type, barrier, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 360));
            double value = new MakeMCBarrierEngine<LowDiscrepancy, GeneralStatistics>(process, 5)
                .WithStepsPerYear(1)
                .WithBrownianBridge()
                .WithSamples(131071)
                .WithMaxSamples(1048575)
                .ToEngine()(option, Context).NPV!.Value;
            Assert.Equal(quantLib, value, 1e-12 * quantLib);
        }
    }
}
