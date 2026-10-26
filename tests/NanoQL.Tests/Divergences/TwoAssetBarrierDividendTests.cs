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
/// The two-asset knock-in's vanilla (the S-bar plan's BR5 (viii), fixed under R8). QuantLib's <c>call()</c> and
/// <c>put()</c> drop asset 1's dividend yield, S₁N(d₁) for S₁e^((b₁−r)T)N(d₁), so its knock-in breaks in-out parity: a
/// down-and-in call at 100, barrier 90 on asset 2, prices 6.497953 and 6.961125 at q₁ of 3% and 8% where vanilla −
/// knock-out is 4.785982 and 3.270239, above the vanilla itself at 8% (the planner's P2); an up-and-in put at barrier 110
/// prices 1.466729 at 8% for 5.464209 (the S-bar run's <c>c1_ta_bb.py</c>). nanoql carries the dividends. Check: in-out
/// parity against QuantLib's knock-out and its analytic European engine on process 1. At q₁ 3% QuantLib's put is NaN:
/// its d₂ is −5.6e-17, where Drezner's 1978 algorithm returns NaN (the S-bar run's <c>c1_dr78_zero.py</c>), and nanoql's
/// bivariate normal is We04DP (BR4 (e)).
/// </summary>
public class TwoAssetBarrierDividendTests
{
    [Fact]
    public void ACallKnockInIsTheVanillaLessTheKnockOut()
    {
        Check(OptionType.Call, BarrierType.DownIn, BarrierType.DownOut, 90.0);
    }

    [Fact]
    public void APutKnockInIsTheVanillaLessTheKnockOut()
    {
        Check(OptionType.Put, BarrierType.UpIn, BarrierType.UpOut, 110.0);
    }

    private static void Check(OptionType type, BarrierType knockIn, BarrierType knockOut, double barrier)
    {
        var today = new Date(16, September, 2015);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        BlackScholesMertonProcess Process(double q, double sigma) => new(
            100.0, new FlatForward(today, q, dc), new FlatForward(today, 0.05, dc),
            new BlackConstantVol(today, new NullCalendar(), sigma, dc));
        var payoff = new PlainVanillaPayoff(type, 100.0);
        var exercise = new EuropeanExercise(today + 365);
        GeneralizedBlackScholesProcess process2 = Process(0.01, 0.3);
        foreach (double q1 in new[] { 0.03, 0.08 })
        {
            GeneralizedBlackScholesProcess process1 = Process(q1, 0.2);
            double Price(BarrierType barrierType) => AnalyticTwoAssetBarrierEngine.Calculate(
                new TwoAssetBarrierOption(barrierType, barrier, payoff, exercise), context, process1, process2, 0.5).NPV!.Value;
            double vanilla = AnalyticEuropeanEngine.Calculate(new VanillaOption(payoff, exercise), context, process1).NPV!.Value;
            Assert.Equal(vanilla - Price(knockOut), Price(knockIn), 1e-12);
        }
    }
}
