// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The analytic Heston engine's evaluation count comes back with each price (the S-eq plan's SEQR5): QuantLib keeps
/// the last call's count in a mutable member (<c>analytichestonengine.hpp:193</c>), which it resets at each call, so a
/// count is the call's own; here
/// <see cref="AnalyticHestonEngine.PriceVanillaPayoffWithStatistics(PlainVanillaPayoff, Date)"/> returns it with the
/// price and the engine's results carry it as "numberOfEvaluations", and one engine serves many calls. The counts are
/// QuantLib's own per integration (<c>hestonmodel.cpp:1617-1780</c>, ported as
/// <c>HestonModelTests.TestAllIntegrationMethods</c>).
/// </summary>
public class HestonEvaluationCountTests
{
    [Fact]
    public void EachCallReturnsItsOwnCount()
    {
        var today = new Date(7, Month.February, 2017);
        var context = new EvaluationContext { EvaluationDate = today };
        AnalyticHestonEngine engine = new(Model(today), 1e-8, 100000);
        var near = new PlainVanillaPayoff(OptionType.Put, 100.0);
        var far = new PlainVanillaPayoff(OptionType.Call, 140.0);
        Date maturity = today + new Period(1, TimeUnit.Years);

        (double Price, int Evaluations) first = engine.PriceVanillaPayoffWithStatistics(near, maturity);
        int[] others =
        [
            engine.PriceVanillaPayoffWithStatistics(far, today + new Period(3, TimeUnit.Years)).Evaluations,
            engine.PriceVanillaPayoffWithStatistics(far, today + new Period(1, TimeUnit.Months)).Evaluations,
            engine.PriceVanillaPayoffWithStatistics(near, today + new Period(10, TimeUnit.Years)).Evaluations,
        ];
        (double Price, int Evaluations) again = engine.PriceVanillaPayoffWithStatistics(near, maturity);

        // the adaptive integration's count depends on the option, and each call's is its own
        Assert.Contains(others, n => n != first.Evaluations);
        Assert.Equal(first, again);

        OneAssetOptionResults results = engine.Calculate(new VanillaOption(near, new EuropeanExercise(maturity)), context);
        Assert.Equal(first.Price, results.NPV);
        Assert.Equal(first.Evaluations, results.Result<int>("numberOfEvaluations"));
        Assert.Equal(first.Price, engine.PriceVanillaPayoff(near, maturity));
    }

    [Fact]
    public void QuantLibsCountsPerIntegration()
    {
        // QuantLib's testAllIntegrationMethods, the integrations S-eq ports: the expected put value and each count
        var today = new Date(7, Month.February, 2017);
        var context = new EvaluationContext { EvaluationDate = today };
        HestonModel model = Model(today);
        var option = new VanillaOption(
            new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(today + new Period(1, TimeUnit.Years)));
        const double expected = 10.147041515497;

        (FourierIntegration Integration, AnalyticHestonEngineComplexLogFormula Formula, double Tol, int Evaluations)[] cases =
        [
            (FourierIntegration.GaussLaguerre(), AnalyticHestonEngineComplexLogFormula.Gatheral, 1e-8, 256),
            (FourierIntegration.GaussLaguerre(), AnalyticHestonEngineComplexLogFormula.BranchCorrection, 1e-8, 256),
            (FourierIntegration.GaussLaguerre(), AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg, 1e-8, 128),
            (FourierIntegration.GaussLegendre(), AnalyticHestonEngineComplexLogFormula.Gatheral, 1e-8, 256),
            (FourierIntegration.GaussLegendre(), AnalyticHestonEngineComplexLogFormula.BranchCorrection, 1e-8, 256),
            (FourierIntegration.GaussLegendre(256), AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg, 1e-4, 256),
            (FourierIntegration.GaussChebyshev(512), AnalyticHestonEngineComplexLogFormula.Gatheral, 1e-4, 1024),
            (FourierIntegration.GaussChebyshev(512), AnalyticHestonEngineComplexLogFormula.BranchCorrection, 1e-4, 1024),
            (FourierIntegration.GaussChebyshev(512), AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg, 1e-4, 512),
            (FourierIntegration.GaussChebyshev2nd(512), AnalyticHestonEngineComplexLogFormula.Gatheral, 2e-4, 1024),
            (FourierIntegration.GaussChebyshev2nd(512), AnalyticHestonEngineComplexLogFormula.BranchCorrection, 2e-4, 1024),
            (FourierIntegration.GaussChebyshev2nd(512), AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg, 2e-4, 512),
            (FourierIntegration.GaussLaguerre(), AnalyticHestonEngineComplexLogFormula.AngledContour, 1e-8, 128),
            (FourierIntegration.GaussLaguerre(192), AnalyticHestonEngineComplexLogFormula.AngledContourNoCV, 1e-8, 192),
        ];

        foreach ((FourierIntegration integration, AnalyticHestonEngineComplexLogFormula formula, double tol, int evaluations) in cases)
        {
            OneAssetOptionResults results = AnalyticHestonEngine.Calculate(option, context, model, formula, integration, 1e-9);
            Assert.True(Math.Abs(results.NPV!.Value - expected) <= tol, $"{formula}: {results.NPV}");
            Assert.Equal(evaluations, results.Result<int>("numberOfEvaluations"));
        }

        // the adaptive integrations' prices, whose counts QuantLib doesn't fix
        (FourierIntegration Integration, double Tol)[] adaptive =
        [
            (FourierIntegration.GaussLobatto(1e-8, null), 1e-8),
            (FourierIntegration.Simpson(1e-8), 1e-6),
            (FourierIntegration.Trapezoid(1e-8), 1e-6),
        ];
        foreach ((FourierIntegration integration, double tol) in adaptive)
        {
            foreach (AnalyticHestonEngineComplexLogFormula formula in new[]
                     { AnalyticHestonEngineComplexLogFormula.Gatheral, AnalyticHestonEngineComplexLogFormula.AndersenPiterbarg })
            {
                Assert.True(integration.IsAdaptiveIntegration);
                double npv = AnalyticHestonEngine.Calculate(option, context, model, formula, integration, 1e-9).NPV!.Value;
                Assert.True(Math.Abs(npv - expected) <= tol, $"{formula}: {npv}");
            }
        }
    }

    private static HestonModel Model(Date today)
    {
        DayCounter dc = new Actual365Fixed();
        return new HestonModel(new HestonProcess(
            new FlatForward(today, 0.05, dc), new FlatForward(today, 0.075, dc), 100.0, 0.1, 4.0, 0.05, 0.4, -0.75));
    }
}
