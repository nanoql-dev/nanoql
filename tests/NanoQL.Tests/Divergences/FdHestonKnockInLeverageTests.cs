// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The FD Heston knock-in's legs (the S-bar plan's BR5 (xviii), fixed under R8). QuantLib prices a knock-in as the
/// vanilla, plus the rebate, less the knock-out, and prices the vanilla and the rebate legs without the leverage
/// function and mixing factor the knock-out leg uses (<c>fdhestonbarrierengine.cpp:173-194</c>), so a stochastic-local
/// knock-in mixes pure Heston into it: on the market below the pure-Heston vanilla is 8.782 for the stochastic-local
/// 7.419. nanoql prices every leg on the model it is given. Check: in-out parity on the stochastic-local model against
/// QuantLib's FD Heston vanilla and rebate engines with the leverage function.
/// </summary>
public class FdHestonKnockInLeverageTests
{
    [Fact]
    public void EveryLegReadsTheLeverageFunction()
    {
        var today = new Date(16, September, 2015);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var model = new HestonModel(new HestonProcess(
            new FlatForward(today, 0.05, dc), new FlatForward(today, 0.02, dc), 100.0, 0.04, 2.0, 0.04, 0.5, -0.5));
        var leverage = new LocalConstantVol(today, 0.8, dc);
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        var exercise = new EuropeanExercise(today + 365);
        double vanilla = FdHestonVanillaEngine.Calculate(
            new VanillaOption(payoff, exercise), context, model, 50, 100, 25, 0, null, leverage).NPV!.Value;
        foreach (double rebate in new[] { 0.0, 3.0 })
        {
            double Price(BarrierType type) => FdHestonBarrierEngine.Calculate(
                new BarrierOption(type, 90.0, rebate, payoff, exercise), context, model, 50, 100, 25, 0, null, leverage).NPV!.Value;

            // QuantLib's rebate leg: the rebate at the hit or at expiry, on a quarter of the grid
            double rebateLeg = FdHestonRebateEngine.Calculate(
                new BarrierOption(BarrierType.DownIn, 90.0, rebate, payoff, exercise), context, model, 50, 25, 10, 0, null,
                leverage).NPV!.Value;
            Assert.Equal(vanilla + rebateLeg, Price(BarrierType.DownIn) + Price(BarrierType.DownOut), 1e-12);
        }
    }
}
