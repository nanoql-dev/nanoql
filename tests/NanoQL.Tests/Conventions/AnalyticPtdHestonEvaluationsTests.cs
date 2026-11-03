// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The analytic time-dependent Heston engine's evaluation count comes back with each price as the additional result
/// "numberOfEvaluations", where QuantLib keeps the last call's count in a mutable member
/// (<c>analyticptdhestonengine.cpp:298</c>) that it resets at each call: a count is the call's own, Gatheral's form
/// counting its two integrations and Andersen and Piterbarg's its one, and one engine serves many calls.
/// </summary>
public class AnalyticPtdHestonEvaluationsTests
{
    [Fact]
    public void EachPriceReturnsItsOwnCount()
    {
        var today = new Date(5, Month.July, 2017);
        var context = new EvaluationContext { EvaluationDate = today };
        var rTS = new FlatForward(today, 0.05, new Actual365Fixed());
        var qTS = new FlatForward(today, 0.08, new Actual365Fixed());
        var model = new PiecewiseTimeDependentHestonModel(
            rTS,
            qTS,
            100.0,
            0.1,
            new ConstantParameter(0.1, new PositiveConstraint()),
            new ConstantParameter(1.0, new PositiveConstraint()),
            new PiecewiseConstantParameter([0.25, 0.75], new PositiveConstraint()).SetParam(0, 0.3).SetParam(1, 0.15).SetParam(2, 1.25),
            new ConstantParameter(-0.75, new BoundaryConstraint(-1.0, 1.0)),
            new TimeGrid([0.25, 0.75, 10.0]));

        VanillaOption Option(double strike, int months) =>
            new(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(today + new Period(months, TimeUnit.Months)));

        // Gauss-Laguerre: each of the two probabilities' integrals reads the order's nodes
        Assert.Equal(2 * 64, AnalyticPTDHestonEngine.Calculate(Option(100, 12), context, model, 64).Result<int>("numberOfEvaluations"));

        // Andersen and Piterbarg's form on the discrete trapezoid: one integral
        Assert.Equal(
            128,
            AnalyticPTDHestonEngine.Calculate(
                Option(100, 12), context, model, AnalyticPTDHestonEngineComplexLogFormula.AndersenPiterbarg,
                FourierIntegration.DiscreteTrapezoid(128)).Result<int>("numberOfEvaluations"));

        // Gauss-Lobatto: the adaptive count depends on the option, and each price's is its own
        var engine = new AnalyticPTDHestonEngine(model, 1e-8, 100000);
        int first = engine.Calculate(Option(100, 12), context).Result<int>("numberOfEvaluations");
        int[] others =
        [
            engine.Calculate(Option(140, 36), context).Result<int>("numberOfEvaluations"),
            engine.Calculate(Option(60, 1), context).Result<int>("numberOfEvaluations"),
        ];
        Assert.Contains(others, n => n != first);
        Assert.Equal(first, engine.Calculate(Option(100, 12), context).Result<int>("numberOfEvaluations"));
        Assert.Equal(first, new AnalyticPTDHestonEngine(model, 1e-8, 100000).Calculate(Option(100, 12), context).Result<int>("numberOfEvaluations"));
    }
}
