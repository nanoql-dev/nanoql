// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The FD Heston barrier, double-barrier and rebate engines refuse a spot beyond a barrier before the solve, with the
/// texts and the triggers of the family's analytic engines: "barrier touched" beyond a single barrier, and "barrier(s)
/// already touched" on or beyond either of a corridor, where QuantLib's engines build a grid that ends at the barrier
/// and fail interpolating the spot outside it.
/// </summary>
public class FdHestonBarrierTouchedTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly HestonModel Model = new(new HestonProcess(
        new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.5, -0.7));

    private static readonly PlainVanillaPayoff Call = new(OptionType.Call, 100.0);
    private static readonly EuropeanExercise Exercise = new(Today + 365);

    [Fact]
    public void TheBarrierEngineRefusesATouchedBarrier()
    {
        foreach ((BarrierType type, double barrier) in new[] { (BarrierType.DownOut, 105.0), (BarrierType.UpIn, 95.0), (BarrierType.DownIn, 101.0) })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => FdHestonBarrierEngine.Calculate(
                new BarrierOption(type, barrier, 0.0, Call, Exercise), Context, Model, 20, 20, 10));
            Assert.Equal("barrier touched", e.Message);
        }
    }

    [Fact]
    public void TheDoubleBarrierEngineRefusesATouchedBarrier()
    {
        foreach ((DoubleBarrierType type, double lo, double hi) in new[]
        {
            (DoubleBarrierType.KnockOut, 105.0, 130.0),
            (DoubleBarrierType.KnockOut, 80.0, 100.0),
            (DoubleBarrierType.KnockIn, 70.0, 95.0),
        })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => FdHestonDoubleBarrierEngine.Calculate(
                new DoubleBarrierOption(type, lo, hi, 0.0, Call, Exercise), Context, Model, 20, 20, 10));
            Assert.Equal("barrier(s) already touched", e.Message);
        }
    }

    [Fact]
    public void TheRebateEngineRefusesATouchedBarrier()
    {
        foreach ((BarrierType type, double barrier) in new[] { (BarrierType.DownIn, 105.0), (BarrierType.UpOut, 95.0) })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => FdHestonRebateEngine.Calculate(
                new BarrierOption(type, barrier, 2.0, Call, Exercise), Context, Model, 20, 20, 10));
            Assert.Equal("barrier touched", e.Message);
        }
    }
}
