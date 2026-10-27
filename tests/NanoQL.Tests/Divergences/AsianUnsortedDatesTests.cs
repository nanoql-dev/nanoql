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
/// The all-past-fixings constructor's date order (the S-bar plan's BR5 (xl), fixed under R8). QuantLib sorts the
/// first constructor's fixing dates and keeps the second's in the caller's order, then splits them at the evaluation
/// date by counting, so reversed dates read the wrong dates as past and the engines integrate them backwards: on spot
/// 100, q 3%, r 6%, σ 20% (Act/360), a call at 100 fixing 60 and 30 days ago at 95 and 105 and monthly over a year
/// prices 6.156224656227607 by Turnbull-Wakeman and 6.605506555285245 by the FD engine on the dates reversed, where
/// sorted they price 4.69672540647389 and 4.71727034815397 (the S-bar run's <c>c3_div_ql_a.py</c>). nanoql sorts both
/// constructors' dates, stably. Check: the sorted dates' route, bit for bit.
/// </summary>
public class AsianUnsortedDatesTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.03, A360), new FlatForward(Today, 0.06, A360), new BlackConstantVol(Today, new NullCalendar(), 0.20, A360));

    private static readonly Date[] Sorted = [Today - 60, Today - 30, .. Enumerable.Range(1, 12).Select(i => Today + (30 * i))];

    [Fact]
    public void TurnbullWakemanPricesReversedDatesAsSorted()
    {
        Assert.Equal(4.69672540647389, TurnbullWakemanAsianEngine.Calculate(Option(Sorted), Context, Process).NPV!.Value, 1e-12);
        Assert.Equal(
            TurnbullWakemanAsianEngine.Calculate(Option(Sorted), Context, Process).NPV,
            TurnbullWakemanAsianEngine.Calculate(Option([.. Sorted.Reverse()]), Context, Process).NPV);
    }

    [Fact]
    public void TheFiniteDifferenceEnginePricesReversedDatesAsSorted()
    {
        Assert.Equal(4.71727034815397, FdBlackScholesAsianEngine.Calculate(Option(Sorted), Context, Process, 100, 100, 50).NPV!.Value, 1e-10);
        Assert.Equal(
            FdBlackScholesAsianEngine.Calculate(Option(Sorted), Context, Process, 100, 100, 50).NPV,
            FdBlackScholesAsianEngine.Calculate(Option([.. Sorted.Reverse()]), Context, Process, 100, 100, 50).NPV);
    }

    private static DiscreteAveragingAsianOption Option(Date[] dates) =>
        new(AverageType.Arithmetic, dates, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 360), [95.0, 105.0]);
}
