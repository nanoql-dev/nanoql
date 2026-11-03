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
/// The Monte Carlo partial floating lookback's λ (fixed). QuantLib's pricer pays S_T − min where the instrument's
/// payoff, and QuantLib's analytic engine, is S_T − λ min: a partial floating call at λ 1.2 (running minimum 100) whose
/// window ends 90 days into 360, on spot 100, r 5%, q 2%, σ 20%, 4 steps, 100,000 antithetic samples at seed 42, prices
/// at 7.747172763156529, its value at λ 1. nanoql pays λ times the minimum. Check: the discrete payoff's exact value on
/// the engine's window {0, 90 days}, the quadrature <c>McLookbackWindowTests</c> takes: 3.042965056630764, within three
/// of the engine's error estimates.
/// </summary>
public class McLookbackLambdaTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ThePartialFloatingPayoffReadsLambda()
    {
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));
        var option = new ContinuousPartialFloatingLookbackOption(
            100.0, 1.2, Today + 90, new FloatingTypePayoff(OptionType.Call), new EuropeanExercise(Today + 360));
        OneAssetOptionResults mc = MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, process, 4, null, false, true, 100000, null, null, 42);
        double reference = McLookbackWindowTests.TwoNodeWindow(100.0, 0.05, 0.02, 0.20, 90 / 365.0, 360 / 365.0, 100.0, 1.2);
        Assert.Equal(3.042965056630764, reference, 1e-9);
        Assert.True(Math.Abs(mc.NPV!.Value - reference) <= 3.0 * mc.ErrorEstimate!.Value, $"{mc.NPV} against {reference}");
    }
}
