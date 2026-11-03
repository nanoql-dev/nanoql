// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The barrier engines' refusals of a touched barrier and of a partial-time rebate, each a
/// <see cref="NanoQLException"/> with its text: QuantLib's "barrier touched" from the two-asset and binomial engines
/// and its "barrier(s) already touched" from the analytic double-barrier engine; nanoql's "barrier touched" from the
/// partial-time engine's start window, the family's text; and "rebate not supported for this partial-time barrier"
/// where no formula prices the rebate.
/// </summary>
public class BarrierRefusalTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly PlainVanillaPayoff Call = new(OptionType.Call, 100.0);
    private static readonly EuropeanExercise Exercise = new(Today + 365);

    [Fact]
    public void ATouchedBarrierIsRefused()
    {
        AssertRefused(
            "barrier touched",
            () => AnalyticPartialTimeBarrierOptionEngine.Calculate(
                new PartialTimeBarrierOption(BarrierType.UpOut, PartialBarrierRange.Start, 95.0, 0.0, Today + 120, Call, Exercise),
                Context,
                Process));
        AssertRefused(
            "barrier touched",
            () => AnalyticTwoAssetBarrierEngine.Calculate(
                new TwoAssetBarrierOption(BarrierType.DownOut, 105.0, Call, Exercise), Context, Process, Process, 0.5));
        AssertRefused(
            "barrier touched",
            () => BinomialBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedBarrierOption>(
                new BarrierOption(BarrierType.UpIn, 95.0, 0.0, Call, Exercise), Context, Process, 50));
        AssertRefused(
            "barrier(s) already touched",
            () => AnalyticDoubleBarrierEngine.Calculate(
                new DoubleBarrierOption(DoubleBarrierType.KnockOut, 80.0, 100.0, 0.0, Call, Exercise), Context, Process));
    }

    [Fact]
    public void APartialTimeRebateNoFormulaPricesIsRefused()
    {
        AssertRefused(
            "rebate not supported for this partial-time barrier",
            () => AnalyticPartialTimeBarrierOptionEngine.Calculate(
                new PartialTimeBarrierOption(BarrierType.UpIn, PartialBarrierRange.EndB1, 110.0, 1.0, Today + 120, Call, Exercise),
                Context,
                Process));
    }

    private static void AssertRefused(string message, Action act) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(act).Message);
}
