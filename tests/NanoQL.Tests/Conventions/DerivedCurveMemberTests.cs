// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The derived curves' and fitting methods' public members that no vector or QuantLib case reaches, each against a form
/// the vectors check: the settlement days the relative curves read from their original, the implied curve from
/// settlement days, which QuantLib-Python doesn't bind, and the cubic B-spline method's basis functions.
/// </summary>
public class DerivedCurveMemberTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheRelativeCurvesReadTheirOriginalsSettlementDays()
    {
        YieldTermStructure original = FlatForward.Create(2, new TARGET(), 0.03, new Actual365Fixed(), Context);
        Assert.Equal(2, new ForwardSpreadedTermStructure(original, 0.01).SettlementDays);
        Assert.Equal(2, new CompositeZeroYieldStructure(original, original, (a, b) => a + b).SettlementDays);
        Assert.Equal(2, new UltimateForwardTermStructure(original, 0.03, 0.04, new Period(20, TimeUnit.Years), 0.1).SettlementDays);
    }

    [Fact]
    public void AnImpliedCurveFromSettlementDaysIsTheCurveAtThatDate()
    {
        YieldTermStructure original = new FlatForward(Today, 0.03, new Actual365Fixed());
        var calendar = new TARGET();
        ImpliedTermStructure implied = ImpliedTermStructure.Create(original, 2, calendar, Context);
        Date settlement = calendar.Advance(Today, 2, TimeUnit.Days);
        Assert.Equal(settlement, implied.ReferenceDate);
        Assert.Equal(2, implied.SettlementDays);
        Assert.Same(calendar, implied.Calendar);
        var atDate = new ImpliedTermStructure(original, settlement);
        foreach (int years in new[] { 1, 5, 10 })
        {
            Date d = settlement + new Period(years, TimeUnit.Years);
            Assert.Equal(atDate.Discount(d), implied.Discount(d));
        }
    }

    [Fact]
    public void TheCubicBSplineMethodsBasisFunctionsAreTheBSplines()
    {
        double[] knots = [-30.0, -20.0, 0.0, 5.0, 10.0, 15.0, 20.0, 25.0, 30.0, 40.0];
        var method = new CubicBSplinesFitting(knots);
        var splines = new BSpline(3, knots.Length - 5, knots);
        for (int i = 0; i <= knots.Length - 5; ++i)
        {
            foreach (double t in new[] { 0.0, 2.5, 7.0, 13.0, 22.0, 29.0 })
            {
                Assert.Equal(splines.Evaluate(i, t), method.BasisFunction(i, t));
            }
        }
    }
}
