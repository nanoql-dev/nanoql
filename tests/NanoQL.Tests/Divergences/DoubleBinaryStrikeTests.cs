// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The double-barrier binary's payoff strike and type (fixed). Hui's series integrates the killed density over the
/// whole corridor and never reads the payoff's strike or type (<c>analyticdoublebarrierbinaryengine.cpp:78</c>, "this
/// is not used ?"), so a knock-out with L 80, U 120, cash 10 prices 6.69467 for every strike and both types, a put
/// struck at 0 among them. nanoql integrates over the payoff's region, [max(K, L), U] for a call and [L, min(K, U)] for
/// a put, in closed form, QuantLib's value standing for a call at K ≤ L and a put at K ≥ U. Checks: an independent
/// Crank-Nicolson solve (the call and put at K 100, 3.046527 and 3.648146, the full corridor 6.694673, extrapolated to
/// N = 4,000), QuantLib's finite-difference Heston double-barrier engine at a vanishing vol of vol, and call + put =
/// the full corridor.
/// </summary>
public class DoubleBinaryStrikeTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EuropeanExercise Exercise = new(Today + 182);

    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.02, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.20, A365));

    [Fact]
    public void TheCallAndThePutAtTheMoneyMatchAnIndependentSolve()
    {
        // QuantLib: 6.69467282 for both
        Assert.Equal(3.046527, KnockOut(OptionType.Call, 100.0), 1e-6);
        Assert.Equal(3.648146, KnockOut(OptionType.Put, 100.0), 1e-6);
    }

    [Fact]
    public void TheFiniteDifferenceHestonEngineAgreesAtAVanishingVolOfVol()
    {
        var model = new HestonModel(new HestonProcess(
            Process.RiskFreeRate, Process.DividendYield, 100.0, 0.04, 1.0, 0.04, 1e-4, 0.0));
        foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
        {
            var option = Option(type, 100.0);
            double fd = FdHestonDoubleBarrierEngine.Calculate(option, Context, model, 200, 400, 30, 0, FdmSchemeDesc.Hundsdorfer()).NPV!.Value;
            // the grid's error at 400 points (3.05215 for the call), the payoff's jump at the strike in its cell
            Assert.Equal(fd, KnockOut(type, 100.0), 1e-2);
        }
    }

    [Fact]
    public void CallPlusPutIsTheFullCorridor()
    {
        double full = KnockOut(OptionType.Call, 0.0);
        Assert.Equal(6.694672821096, full, 1e-11);
        foreach (double strike in new[] { 85.0, 100.0, 117.5 })
        {
            Assert.Equal(full, KnockOut(OptionType.Call, strike) + KnockOut(OptionType.Put, strike), 1e-12);
        }

        // QuantLib's value stands for a put at K >= U, and a put struck at 0 pays nothing (QuantLib 6.69467)
        Assert.Equal(full, KnockOut(OptionType.Put, 130.0), 1e-15);
        Assert.Equal(0.0, KnockOut(OptionType.Put, 0.0));
    }

    private static DoubleBarrierOption Option(OptionType type, double strike) =>
        new(DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, new CashOrNothingPayoff(type, strike, 10.0), Exercise);

    private static double KnockOut(OptionType type, double strike) =>
        AnalyticDoubleBarrierBinaryEngine.Calculate(Option(type, strike), Context, Process).NPV!.Value;
}
