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
/// The partial-time barrier engine's rebate (the S-bar plan's BR5 (xii), fixed under R8). The instrument takes a rebate
/// and no formula of QuantLib's engine reads it: 11.116881 at rebates 0 and 5 (the planner's P2). nanoql prices a
/// start-window knock-out's rebate, paid at the hit before the cover event, as QuantLib's analytic barrier engine
/// prices a knock-out's rebate to the cover event; no formula covers the end windows' or the knock-ins' rebates, which
/// are refused ("rebate not supported for this partial-time barrier"). Check: QuantLib's analytic barrier engine with
/// the cover event as expiry.
/// </summary>
public class PartialTimeRebateTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise Exercise = new(Today + 365);
    private static readonly Date Cover = Today + 120;

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.0, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    [Theory]
    [InlineData(BarrierType.DownOut, 90.0, OptionType.Call)]
    [InlineData(BarrierType.UpOut, 110.0, OptionType.Put)]
    public void AStartWindowKnockOutsRebateIsTheBarrierEnginesToTheCoverEvent(BarrierType type, double barrier, OptionType optionType)
    {
        var payoff = new PlainVanillaPayoff(optionType, 100.0);
        double Price(double rebate) => AnalyticPartialTimeBarrierOptionEngine.Calculate(
            new PartialTimeBarrierOption(type, PartialBarrierRange.Start, barrier, rebate, Cover, payoff, Exercise),
            Context,
            Process).NPV!.Value;
        double Hit(double rebate) => AnalyticBarrierEngine.Calculate(
            new BarrierOption(type, barrier, rebate, payoff, new EuropeanExercise(Cover)), Context, Process).NPV!.Value;
        Assert.Equal(Hit(5.0) - Hit(0.0), Price(5.0) - Price(0.0), 1e-12);
        Assert.True(Price(5.0) > Price(0.0));
    }

    [Fact]
    public void AnEndWindowsOrAKnockInsRebateIsRefused()
    {
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        foreach ((BarrierType type, PartialBarrierRange range) in new[]
        {
            (BarrierType.DownOut, PartialBarrierRange.EndB1),
            (BarrierType.DownOut, PartialBarrierRange.EndB2),
            (BarrierType.DownIn, PartialBarrierRange.Start),
        })
        {
            var option = new PartialTimeBarrierOption(type, range, 90.0, 5.0, Cover, payoff, Exercise);
            var e = Assert.Throws<NanoQLException>(() => AnalyticPartialTimeBarrierOptionEngine.Calculate(option, Context, Process));
            Assert.Equal("rebate not supported for this partial-time barrier", e.Message);
        }
    }
}
