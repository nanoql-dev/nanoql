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
/// The partial-time barrier engine's exercise. QuantLib's engine never reads the exercise's type: on spot 100, r 5%, q
/// 1%, σ 20% over a year, a call struck at 100, barrier 90, cover event at 182 days, its start-window and end-window B1
/// down-and-outs price an American exercise at their European values, 8.258475774804 and 9.269946137985, and its
/// start-window down-and-in crashes ("Boost assertion failed: px != 0", the European leg built on a null exercise).
/// nanoql refuses a non-European exercise, as the family's European engines do: Heynen and Kat's formulas price a
/// European exercise only. The European values stand, within a measured 3.3e-5 of QuantLib's (West's bivariate normal
/// for Drezner's).
/// </summary>
public class PartialTimeExerciseTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.01, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));

    [Theory]
    [InlineData(BarrierType.DownOut, PartialBarrierRange.Start, 8.258475774804)]
    [InlineData(BarrierType.DownIn, PartialBarrierRange.Start, 1.567822007935)]
    [InlineData(BarrierType.DownOut, PartialBarrierRange.EndB1, 9.269946137985)]
    public void ANonEuropeanExerciseIsRefused(BarrierType type, PartialBarrierRange range, double quantLibEuropean)
    {
        PartialTimeBarrierOption american = Option(type, range, new AmericanExercise(Today, Today + 365));
        var e = Assert.Throws<NanoQLException>(() => AnalyticPartialTimeBarrierOptionEngine.Calculate(american, Context, Process));
        Assert.Equal("only european style option are supported", e.Message);

        double european = AnalyticPartialTimeBarrierOptionEngine.Calculate(
            Option(type, range, new EuropeanExercise(Today + 365)), Context, Process).NPV!.Value;
        Assert.Equal(quantLibEuropean, european, 3.3e-5);
    }

    private static PartialTimeBarrierOption Option(BarrierType type, PartialBarrierRange range, Exercise exercise) =>
        new(type, range, 90.0, 0.0, Today + 182, new PlainVanillaPayoff(OptionType.Call, 100.0), exercise);
}
