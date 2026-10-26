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
/// A running extremum beyond the spot (the S-bar plan's BR5 (xxii), fixed under R8). The spot is observed, so a running
/// maximum below it or a minimum above it is impossible; QuantLib's engines price it as given: on spot 100, r 5%, q 0,
/// σ 20% over a year, a fixed call struck at 90 with a running maximum of 80 at 30.175098740702154, where the spot as
/// the maximum gives 28.67991950233946, and a floating put with that maximum at 19.69144860119077 for
/// 14.29056770740371 (the planner's P2, rerun as the S-bar run's <c>c2_lb_facts.py</c>). nanoql reads the extremum as
/// max(minmax, S) or min(minmax, S), in the analytic and Monte Carlo engines. Check: QuantLib's value at minmax = S.
/// </summary>
public class LookbackExtremeBeyondSpotTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise AYear = new(Today + 365);

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.0, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));

    [Theory]
    [InlineData(OptionType.Call, 90.0, 80.0, 28.67991950233946)]
    [InlineData(OptionType.Call, 90.0, 95.0, 28.67991950233946)]
    [InlineData(OptionType.Put, 110.0, 120.0, 21.852038932439406)]
    public void AFixedLookbacksExtremumIsTheSpotWhereItLiesBeyondIt(OptionType type, double strike, double minmax, double quantLibAtSpot)
    {
        var option = new ContinuousFixedLookbackOption(minmax, new PlainVanillaPayoff(type, strike), AYear);
        Assert.Equal(quantLibAtSpot, AnalyticContinuousFixedLookbackEngine.Calculate(option, Context, Process).NPV!.Value, 1e-12);
    }

    [Theory]
    [InlineData(OptionType.Put, 80.0, 14.29056770740371)]
    [InlineData(OptionType.Put, 95.0, 14.29056770740371)]
    [InlineData(OptionType.Call, 120.0, 17.216802237360877)]
    public void AFloatingLookbacksExtremumIsTheSpotWhereItLiesBeyondIt(OptionType type, double minmax, double quantLibAtSpot)
    {
        var option = new ContinuousFloatingLookbackOption(minmax, new FloatingTypePayoff(type), AYear);
        Assert.Equal(quantLibAtSpot, AnalyticContinuousFloatingLookbackEngine.Calculate(option, Context, Process).NPV!.Value, 1e-12);
    }
}
