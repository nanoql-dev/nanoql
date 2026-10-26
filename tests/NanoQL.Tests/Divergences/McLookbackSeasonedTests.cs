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
/// The Monte Carlo lookback engine on seasoned options (the S-bar plan's BR5 (xx), fixed under R8). QuantLib's pricers
/// take the extremum from the path's second node and never read the running extremum, so a seasoned option prices as a
/// new one: on spot 100, r 6%, q 0, σ 10% over a year, 2,000 steps, 20,000 antithetic samples at seed 1, a fixed call
/// struck at 100 with a running maximum of 115 at 11.196910337699553 and a floating call with a running minimum of 90
/// at 10.747874194538674, their values with the extremum at the spot, where the analytic engines give
/// 16.195934579797157 and 15.741880586612329 (the S-bar run's probe harness, <c>probe_mc_ql.py</c>). nanoql folds the
/// observed extremum into the path's. Check: QuantLib's analytic engines on the seasoned options, which the Monte Carlo
/// converges to, within QuantLib's own Monte Carlo test's 0.1.
/// </summary>
public class McLookbackSeasonedTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise AYear = new(Today + 365);

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.0, A365), new FlatForward(Today, 0.06, A365), new BlackConstantVol(Today, new NullCalendar(), 0.10, A365));

    [Fact]
    public void ASeasonedFixedLookbackReadsItsRunningMaximum()
    {
        var option = new ContinuousFixedLookbackOption(115.0, new PlainVanillaPayoff(OptionType.Call, 100.0), AYear);
        double monteCarlo = MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, Process, 2000, null, false, true, 20000, null, null, 1).NPV!.Value;
        Assert.Equal(AnalyticContinuousFixedLookbackEngine.Calculate(option, Context, Process).NPV!.Value, monteCarlo, 0.1);
    }

    [Fact]
    public void ASeasonedFloatingLookbackReadsItsRunningMinimum()
    {
        var option = new ContinuousFloatingLookbackOption(90.0, new FloatingTypePayoff(OptionType.Call), AYear);
        double monteCarlo = MCLookbackEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, Process, 2000, null, false, true, 20000, null, null, 1).NPV!.Value;
        Assert.Equal(AnalyticContinuousFloatingLookbackEngine.Calculate(option, Context, Process).NPV!.Value, monteCarlo, 0.1);
    }
}
