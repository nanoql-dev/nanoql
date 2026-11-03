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
/// The partial-time barrier engine with the cover event on the evaluation date (fixed). QuantLib's formulas take t₁ = 0
/// into Drezner's bivariate normal with infinite arguments and fail ("case not handled"). nanoql takes the formulas'
/// limit: the start window monitors only the spot, so a knock-out is the vanilla and a knock-in 0; the end windows
/// monitor the barrier from today to expiry, so B1 is the standard knock-out on the spot's side of the barrier and B2
/// the standard knock-out on its own side, out at once with the spot beyond it, the knock-ins by in-out parity. A cover
/// event before the evaluation date stays refused as QuantLib refuses it ("negative time"). Checks: QuantLib's analytic
/// European and barrier engines, and the engine's own values at a cover event a day out.
/// </summary>
public class PartialTimeCoverTodayTests
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

    private static readonly PlainVanillaPayoff Call = new(OptionType.Call, 100.0);

    [Fact]
    public void TheStartWindowIsTheVanillaOrNothing()
    {
        double vanilla = AnalyticEuropeanEngine.Calculate(new VanillaOption(Call, Exercise), Context, Process).NPV!.Value;

        // QuantLib: "case not handled"
        Assert.Equal(vanilla, Price(BarrierType.DownOut, PartialBarrierRange.Start, 90.0, Today));
        Assert.Equal(0.0, Price(BarrierType.DownIn, PartialBarrierRange.Start, 90.0, Today));
        Assert.Equal(vanilla, Price(BarrierType.DownOut, PartialBarrierRange.Start, 90.0, Today + 1), 1e-6);
    }

    [Fact]
    public void TheEndWindowsAreTheStandardBarrierOption()
    {
        double DownOut(double barrier) => AnalyticBarrierEngine.Calculate(
            new BarrierOption(BarrierType.DownOut, barrier, 0.0, Call, Exercise), Context, Process).NPV!.Value;
        double UpOut(double barrier) => AnalyticBarrierEngine.Calculate(
            new BarrierOption(BarrierType.UpOut, barrier, 0.0, Call, Exercise), Context, Process).NPV!.Value;
        double vanilla = AnalyticEuropeanEngine.Calculate(new VanillaOption(Call, Exercise), Context, Process).NPV!.Value;

        // B1: the knock-out on the spot's side, whatever the type's direction
        Assert.Equal(DownOut(90.0), Price(BarrierType.DownOut, PartialBarrierRange.EndB1, 90.0, Today));
        Assert.Equal(UpOut(130.0), Price(BarrierType.DownOut, PartialBarrierRange.EndB1, 130.0, Today));

        // B2: the knock-out on its own side, out at once with the spot beyond it
        Assert.Equal(DownOut(90.0), Price(BarrierType.DownOut, PartialBarrierRange.EndB2, 90.0, Today));
        Assert.Equal(0.0, Price(BarrierType.DownOut, PartialBarrierRange.EndB2, 110.0, Today));

        // the knock-ins by parity
        Assert.Equal(vanilla - DownOut(90.0), Price(BarrierType.DownIn, PartialBarrierRange.EndB1, 90.0, Today), 1e-14);

        // continuity with a cover event a day out
        Assert.Equal(DownOut(90.0), Price(BarrierType.DownOut, PartialBarrierRange.EndB1, 90.0, Today + 1), 2e-3);

        // a cover event before the evaluation date stays refused, as QuantLib refuses it
        Assert.Throws<NanoQLException>(() => Price(BarrierType.DownOut, PartialBarrierRange.EndB1, 90.0, Today - 1));
    }

    private static double Price(BarrierType type, PartialBarrierRange range, double barrier, Date cover) =>
        AnalyticPartialTimeBarrierOptionEngine.Calculate(
            new PartialTimeBarrierOption(type, range, barrier, 0.0, cover, Call, Exercise), Context, Process).NPV!.Value;
}
