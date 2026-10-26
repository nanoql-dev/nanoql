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
/// The two-asset barrier's second risk-free curve (the S-bar plan's BR5 (vii), fixed under R8; the S-exo plan's ED27).
/// QuantLib takes asset 2's carry from process 1's rate, b₂ = r₁ − q₂, so the price ignores process 2's curve: a
/// down-and-out call at 100, barrier 90 on asset 2, r₁ 5%, q₂ 1%, prices 4.547879 at r₂ of 0, 5% and 10% (the
/// planner's P2). nanoql reads b₂ = r₂ − q₂ and discounts on process 1's curve. Check: QuantLib's engine on the market
/// restated onto process 1's curve, asset 2's dividend yield q₂ + r₁ − r₂: 4.045769328445, 4.547878997954 and
/// 5.044213261884 on QuantLib's Dr78 (the S-bar run's <c>c1_ta_bb.py</c>), to its six decimal places on the
/// formula's terms of weight 100 (BR4 (e)).
/// </summary>
public class TwoAssetBarrierTwoCurvesTests
{
    [Fact]
    public void AssetTwosCarryReadsItsOwnCurve()
    {
        var today = new Date(16, September, 2015);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        BlackScholesMertonProcess Process(double r, double q, double sigma) => new(
            100.0, new FlatForward(today, q, dc), new FlatForward(today, r, dc),
            new BlackConstantVol(today, new NullCalendar(), sigma, dc));
        var option = new TwoAssetBarrierOption(
            BarrierType.DownOut, 90.0, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(today + 365));
        GeneralizedBlackScholesProcess process1 = Process(0.05, 0.0, 0.2);
        double Price(GeneralizedBlackScholesProcess process2) =>
            AnalyticTwoAssetBarrierEngine.Calculate(option, context, process1, process2, 0.5).NPV!.Value;

        foreach ((double r2, double quantLibRestated) in new[]
        {
            (0.0, 4.045769328445090),
            (0.05, 4.547878997954061),
            (0.10, 5.044213261884106),
        })
        {
            double value = Price(Process(r2, 0.01, 0.3));
            Assert.Equal(Price(Process(0.05, 0.01 + 0.05 - r2, 0.3)), value, 1e-12);
            Assert.Equal(quantLibRestated, value, 1e-4);
        }
    }
}
