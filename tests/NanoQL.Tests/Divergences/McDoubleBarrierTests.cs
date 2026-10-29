// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo double-barrier engine's clock and exercise (the S-xeq plan's XER5 (xxxiii), and the S-xeq run's
/// R1-4, fixed under R8). QuantLib runs the paths and the discounts on the risk-free curve's time and reads each
/// structure there on its own clock (<c>mcdoublebarrierengine.hpp:164-201</c>), and prices any exercise to its last
/// date, never reading its type. nanoql runs them on S-bar's engine clock, each structure read at the instants its
/// times denote, and refuses a non-European exercise as S-bar's barrier engine does. Checks: the market restated onto
/// the risk-free curve's day counter; the refusal's text.
/// </summary>
public class McDoubleBarrierTests
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
        double Npv(BlackVolTermStructure vol) => MCDoubleBarrierEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context,
            new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.02, A360), new FlatForward(Today, 0.05, A360), vol),
            50, null, false, false, 2000, null, null, 42).NPV!.Value;

        double restated = Npv(new BlackConstantVol(Today, new NullCalendar(), 0.25 * Math.Sqrt(360.0 / 365.0), A360));
        double mismatched = Npv(new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        Assert.Equal(restated, mismatched, 1e-12);
    }

    [Fact]
    public void ANonEuropeanExerciseIsRefused()
    {
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 130.0, 0.0, new PlainVanillaPayoff(OptionType.Put, 100.0),
            new AmericanExercise(Today, Today + 182));
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
        var e = Assert.Throws<NanoQLException>(() => MCDoubleBarrierEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, process, 50, null, false, false, 100, null, null, 42));
        Assert.Equal("only european style option are supported", e.Message);
    }
}
