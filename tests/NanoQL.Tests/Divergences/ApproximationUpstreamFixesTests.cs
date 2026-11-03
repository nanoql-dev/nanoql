// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Two fixes QuantLib's master made to the American approximations after the pin, which nanoql takes, with QuantLib
/// 1.44's values recorded (QuantLib-Python 1.44). #2854 (<c>b19a8aa53</c>): below a variance of ε,
/// Bjerksund-Stensland's approximation divides by zero, and QuantLib 1.44 returns NaN; the master returns the European
/// value floored at the exercise value. #2906 (<c>513d6002a</c>): for short-dated, highly volatile options Ju's
/// correction can take the value below the European one, a lower bound for the American; the master floors it there,
/// with the European delta and gamma. QuantLib's check: the master's own cases,
/// <c>TestSuite/AmericanOptionTests.cs</c>' <c>TestBaroneAdesiWhaleyAndJuAtLowVolatility</c> and
/// <c>TestJuNotBelowEuropeanValue</c>; here the values themselves.
/// </summary>
public class ApproximationUpstreamFixesTests
{
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(OptionType.Put, 80.0, 20.0)] // QuantLib 1.44 NaN; the European value 18.3495606429239
    [InlineData(OptionType.Put, 100.0, 0.0)] // NaN; 0
    [InlineData(OptionType.Call, 120.0, double.NaN)] // NaN; the European value 21.257858009226265, above intrinsic
    public void BjerksundStenslandAtZeroVarianceIsTheFlooredEuropeanValue(OptionType type, double spot, double intrinsic)
    {
        // #2854's case: q 2%, r 5%, 180 days, a strike of 100, no volatility
        var today = new Date(15, Month.May, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        GeneralizedBlackScholesProcess process = Process(today, spot, 0.02, 0.05, 0.0);
        var payoff = new PlainVanillaPayoff(type, 100.0);
        double value = BjerksundStenslandApproximationEngine.Calculate(
            new VanillaOption(payoff, new AmericanExercise(today, today + 180)), context, process).NPV!.Value;
        double european = AnalyticEuropeanEngine.Calculate(
            new VanillaOption(payoff, new EuropeanExercise(today + 180)), context, process).NPV!.Value;
        Assert.Equal(double.IsNaN(intrinsic) ? european : Math.Max(european, intrinsic), value);
    }

    [Theory]
    [InlineData(OptionType.Put, 110.0, 0.02, 0.00, 0.80, 3)] // QuantLib 1.44 10.0, delta -1, gamma 0
    [InlineData(OptionType.Put, 100.0, 0.05, 0.03, 1.20, 1)] // 2.4151766287832372, -7.504694851415608, -1133.902614367008
    [InlineData(OptionType.Call, 90.0, 0.01, 0.03, 1.20, 3)] // 10.869932807014203, 0.838684413404956, 0.015994406871554248
    public void JuBelowTheEuropeanValueIsTheEuropeanResults(OptionType type, double strike, double r, double q, double vol, int days)
    {
        // #2906's cases on a spot of 100: the European value, delta and gamma
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        GeneralizedBlackScholesProcess process = Process(today, 100.0, q, r, vol);
        var payoff = new PlainVanillaPayoff(type, strike);
        OneAssetOptionResults ju = JuQuadraticApproximationEngine.Calculate(
            new VanillaOption(payoff, new AmericanExercise(today, today + days)), context, process);
        OneAssetOptionResults european = AnalyticEuropeanEngine.Calculate(
            new VanillaOption(payoff, new EuropeanExercise(today + days)), context, process);
        Assert.Equal(0.0, (ju.NPV!.Value / european.NPV!.Value) - 1.0, 1e-13);
        Assert.Equal(0.0, (ju.Delta!.Value / european.Delta!.Value) - 1.0, 1e-13);
        Assert.Equal(0.0, (ju.Gamma!.Value / european.Gamma!.Value) - 1.0, 1e-12);
    }

    private static GeneralizedBlackScholesProcess Process(Date today, double spot, double q, double r, double vol) =>
        new BlackScholesMertonProcess(
            spot, new FlatForward(today, q, A365), new FlatForward(today, r, A365), new BlackConstantVol(today, new NullCalendar(), vol, A365));
}
