// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The American approximations' theta where early exercise is never optimal, on structures dated differently. There
/// Barone-Adesi-Whaley, Ju and Bjerksund-Stensland report the European results, and QuantLib's theta divides every term
/// by the volatility's time (<c>baroneadesiwhaleyengine.cpp:200-204</c>, <c>juquadraticengine.cpp:108-112</c>,
/// <c>bjerksundstenslandengine.cpp:145-149</c>), as its analytic European engine does. nanoql's analytic European
/// engine divides each term by its own structure's time (<c>Divergences/AnalyticThetaMismatchedTimesTests.cs</c>,
/// checked there against QuantLib's <c>blackScholesTheta</c>), and the approximations' European branch is that
/// engine's. QuantLib's check: through the analytic engine's. QuantLib's thetas, its approximations' and its analytic
/// engine's alike, recorded (QuantLib-Python 1.44). Where the structures share their dates, QuantLib's bits (the
/// engines' vectors).
/// </summary>
public class ApproximationThetaMismatchedTimesTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(7)] // QuantLib -36.22318546220382, nanoql's analytic engine -36.513410761523375
    [InlineData(365)] // -5.533328226046217 and -5.539585484451105
    public void ANeverExercisedPutsThetaIsTheAnalyticEngines(int days)
    {
        // a put at 100 with r -1% settling T+2, no dividend and volatility 25% today: never exercised early
        GeneralizedBlackScholesProcess process = Process(new FlatForward(Today, 0.0, A365), Settling(-0.01));
        double theta = European(OptionType.Put, days, process);
        AssertTheta(theta, BaroneAdesiWhaleyApproximationEngine.Calculate(American(OptionType.Put, days), Context, process));
        AssertTheta(theta, JuQuadraticApproximationEngine.Calculate(American(OptionType.Put, days), Context, process));
    }

    [Theory]
    [InlineData(7)] // QuantLib -36.22318546220381, nanoql's analytic engine -36.51341076152337
    [InlineData(365)] // -5.533328226046217 and -5.539585484451105
    public void ANeverExercisedCallsThetaIsTheAnalyticEngines(int days)
    {
        // a call at 100 with q -1% settling T+2, r 0 and volatility 25% today: Bjerksund-Stensland's European branch,
        // and the other two's
        GeneralizedBlackScholesProcess process = Process(Settling(-0.01), new FlatForward(Today, 0.0, A365));
        double theta = European(OptionType.Call, days, process);
        AssertTheta(theta, BjerksundStenslandApproximationEngine.Calculate(American(OptionType.Call, days), Context, process));
        AssertTheta(theta, BaroneAdesiWhaleyApproximationEngine.Calculate(American(OptionType.Call, days), Context, process));
        AssertTheta(theta, JuQuadraticApproximationEngine.Calculate(American(OptionType.Call, days), Context, process));
    }

    private static YieldTermStructure Settling(double rate) => FlatForward.Create(2, new TARGET(), rate, A365, Context);

    private static GeneralizedBlackScholesProcess Process(YieldTermStructure dividend, YieldTermStructure riskFree) =>
        new BlackScholesMertonProcess(100.0, dividend, riskFree, new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static VanillaOption American(OptionType type, int days) =>
        new(new PlainVanillaPayoff(type, 100.0), new AmericanExercise(Today, Today + days));

    private static double European(OptionType type, int days, GeneralizedBlackScholesProcess process) =>
        AnalyticEuropeanEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(type, 100.0), new EuropeanExercise(Today + days)), Context, process)
            .Theta!.Value;

    private static void AssertTheta(double theta, OneAssetOptionResults results) =>
        Assert.Equal(0.0, (results.Theta!.Value / theta) - 1.0, 1e-13);
}
