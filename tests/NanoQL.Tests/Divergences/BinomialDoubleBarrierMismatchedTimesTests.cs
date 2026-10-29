// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The binomial double-barrier engine's tree and steps (the S-xeq plan's XER5 (x), (xi), fixed under R8). QuantLib
/// flattens each structure from the risk-free curve's reference date on its own day counter and runs the tree over the
/// risk-free curve's time (<c>binomialdoublebarrierengine.hpp:73-117</c>), so a volatility on another day counter
/// integrates the wrong variance; and it builds the grid and the lattice on the step count given where the
/// Leisen-Reimer and Joshi trees build on one more at an even count, its 200 steps 3.8467695315 against 201's
/// 3.8822505099 on a Leisen-Reimer tree [executed: the planner's <c>xp_c1.cpp bin</c>]. nanoql's tree is S-bar's
/// binomial barrier engine's (BR5 (ii)) and its grid and lattice take the tree's own steps (SEQR7 (x)). Checks: the
/// market restated onto the risk-free curve's day counter; the tree's steps.
/// </summary>
public class BinomialDoubleBarrierMismatchedTimesTests
{
    private static readonly Date Today = new(15, May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AnAct365VolatilityBesideAnAct360CurvePricesAsOnTheRestatedMarket()
    {
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 130.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0),
            new EuropeanExercise(Today + 182));
        var mismatched = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A360), new FlatForward(Today, 0.05, A360),
            new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        var restated = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A360), new FlatForward(Today, 0.05, A360),
            new BlackConstantVol(Today, new NullCalendar(), 0.25 * Math.Sqrt(360.0 / 365.0), A360));
        double expected = BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniDoubleBarrierOption>(
            option, Context, restated, 400).NPV!.Value;
        double calculated = BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniDoubleBarrierOption>(
            option, Context, mismatched, 400).NPV!.Value;
        Assert.Equal(expected, calculated, 1e-12);
    }

    [Fact]
    public void AnEvenCountOnALeisenReimerTreeIsTheTreesOddOne()
    {
        // the planner's xp_c1.cpp bin market: S 100, q 0.02, r 0.05, σ 0.25, [80, 130], six months
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 130.0, 0.0, new PlainVanillaPayoff(OptionType.Call, 100.0),
            new EuropeanExercise(Today + 182));
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        double even = BinomialDoubleBarrierEngine.Calculate<LeisenReimer, DiscretizedDoubleBarrierOption>(
            option, Context, process, 200).NPV!.Value;
        double odd = BinomialDoubleBarrierEngine.Calculate<LeisenReimer, DiscretizedDoubleBarrierOption>(
            option, Context, process, 201).NPV!.Value;
        Assert.Equal(odd, even);
        Assert.Equal(3.8822505099, odd, 1e-9); // QuantLib's 201 steps
    }
}
