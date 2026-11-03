// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CEV engine at a time of 0 (a run find). The formula's non-centrality x₀/t is infinite at t = 0, which Boost
/// refuses ("Non centrality parameter is inf"), so QuantLib throws on an option exercised today with the reference
/// date's events included; and an exercise before the discount curve's reference date throws QuantLib's "negative time"
/// (<c>analyticcevengine.cpp:104-111</c>). nanoql reads the curve by date, nothing accrued before its reference date
/// (the playbook's row for structures on another clock), and at t = 0 the value is the payoff at f0, the limit
/// QuantLib's own values approach: a day out, its call at K 90 on f0 100 is 9.999178115968299 (QuantLib-Python 1.44; σ
/// 20%, r 3% on Actual/365 (Fixed), from 15 October 2026).
/// </summary>
public class CevAtExpiryTests
{
    private const double F0 = 100.0;

    private static readonly Date Today = new(15, Month.October, 2026);

    [Theory]
    [InlineData(0.5, OptionType.Call, 10.0)]
    [InlineData(0.5, OptionType.Put, 0.0)]
    [InlineData(1.5, OptionType.Call, 10.0)]
    [InlineData(1.5, OptionType.Put, 0.0)]
    public void ExercisedTodayTheValueIsThePayoff(double beta, OptionType type, double payoff)
    {
        // QuantLib throws with the reference date's events included; without them the option is expired, NPV 0, in both
        var context = new EvaluationContext { EvaluationDate = Today, IncludeReferenceDateEvents = true };
        YieldTermStructure discount = new FlatForward(Today, 0.03, new Actual365Fixed());
        Assert.Equal(payoff, Price(context, discount, type, Today, beta));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.5)]
    public void ExercisedBeforeTheCurvesDateTheValueIsThePayoff(double beta)
    {
        // the curve dated two days out, the call exercised tomorrow: QuantLib throws "negative time (-0.00273973) given"
        var context = new EvaluationContext { EvaluationDate = Today };
        YieldTermStructure discount = new FlatForward(Today + 2, 0.03, new Actual365Fixed());
        Assert.Equal(10.0, Price(context, discount, OptionType.Call, Today + 1, beta));
    }

    private static double Price(
        EvaluationContext context, YieldTermStructure discount, OptionType type, Date exercise, double beta) =>
        AnalyticCEVEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(type, 90.0), new EuropeanExercise(exercise)),
            context,
            F0,
            0.2 * Math.Pow(F0, 1 - beta),
            beta,
            discount).NPV!.Value;
}
