// SPDX-FileCopyrightText: Copyright (C) 2010 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/margrabeoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Moved: testEuroExchangeTwoAssets line 250 (Divergences/MargrabeGreeksQuantityTests.cs); testEuroExchangeTwoAssets line 259 (Divergences/MargrabeGreeksQuantityTests.cs); testEuroExchangeTwoAssets line 268 (Divergences/MargrabeGreeksQuantityTests.cs)

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class MargrabeOptionTests
{
    // The test runner's evaluation date (quantlibglobalfixture.cpp): the cases set none of their own.
    private static readonly Date Today = new(16, Month.September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TestEuroExchangeTwoAssets()
    {
        // Exchange-One-Asset-for-Another European Options
        MargrabeOptionTwoData[] values =
        [
            // s1,  s2,  Q1,  Q2,  q1,  q2,  r,  t,  v1,  v2,  rho,  result,
            // delta1,  delta2,  gamma1,  gamma2,  theta, rho, tol
            // data from "given article p.52"
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.15, -0.50, 2.125, 0.841, -0.818, 0.112, 0.135, -2.043, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.20, -0.50, 2.199, 0.813, -0.784, 0.109, 0.132, -2.723, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.25, -0.50, 2.283, 0.788, -0.753, 0.105, 0.126, -3.419, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.15, 0.00, 2.045, 0.883, -0.870, 0.108, 0.131, -1.168, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.20, 0.00, 2.091, 0.857, -0.838, 0.112, 0.135, -1.698, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.25, 0.00, 2.152, 0.830, -0.805, 0.111, 0.134, -2.302, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.15, 0.50, 1.974, 0.946, -0.942, 0.079, 0.096, -0.126, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.20, 0.50, 1.989, 0.929, -0.922, 0.092, 0.111, -0.398, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.25, 0.50, 2.019, 0.902, -0.891, 0.104, 0.125, -0.838, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.15, -0.50, 2.762, 0.672, -0.602, 0.072, 0.087, -1.207, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.20, -0.50, 2.989, 0.661, -0.578, 0.064, 0.078, -1.457, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.25, -0.50, 3.228, 0.653, -0.557, 0.058, 0.070, -1.712, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.15, 0.00, 2.479, 0.695, -0.640, 0.085, 0.102, -0.874, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.20, 0.00, 2.650, 0.680, -0.616, 0.077, 0.093, -1.078, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.25, 0.00, 2.847, 0.668, -0.592, 0.069, 0.083, -1.302, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.15, 0.50, 2.138, 0.746, -0.713, 0.106, 0.128, -0.416, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.20, 0.50, 2.231, 0.728, -0.689, 0.099, 0.120, -0.550, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.25, 0.50, 2.374, 0.707, -0.659, 0.090, 0.109, -0.741, 0.0, 1.0e-3),
            // Quantity tests from Excel calcuations
            new MargrabeOptionTwoData(22.0, 10.0, 1, 2, 0.06, 0.04, 0.10, 0.50, 0.20, 0.15, 0.50, 2.138, 0.746, -1.426, 0.106, 0.255, -0.987, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(11.0, 20.0, 2, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.20, 0.50, 2.231, 1.455, -0.689, 0.198, 0.120, 0.410, 0.0, 1.0e-3),
            new MargrabeOptionTwoData(11.0, 10.0, 2, 2, 0.06, 0.04, 0.10, 0.50, 0.20, 0.25, 0.50, 2.374, 1.413, -1.317, 0.181, 0.219, -0.336, 0.0, 1.0e-3),
        ];

        DayCounter dc = new Actual360();

        foreach (MargrabeOptionTwoData value in values)
        {
            Date exDate = Today + TimeToDays(value.T);
            var exercise = new EuropeanExercise(exDate);

            YieldTermStructure rTS = new FlatForward(Today, value.R, dc);
            var stochProcess1 = new BlackScholesMertonProcess(
                value.S1, new FlatForward(Today, value.Q1Rate, dc), rTS, Vol(Today, value.V1, dc));
            var stochProcess2 = new BlackScholesMertonProcess(
                value.S2, new FlatForward(Today, value.Q2Rate, dc), rTS, Vol(Today, value.V2, dc));

            var margrabeOption = new MargrabeOption(value.Q1, value.Q2, exercise);

            // analytic engine
            MargrabeOptionResults results = AnalyticEuropeanMargrabeEngine.Calculate(
                margrabeOption, Context, stochProcess1, stochProcess2, value.Rho);

            Real tolerance = value.Tol;
            Check("value", value, value.Result, results.NPV!.Value, tolerance);
            Check("delta1", value, value.Delta1, results.Delta1!.Value, tolerance);
            Check("delta2", value, value.Delta2, results.Delta2!.Value, tolerance);

            // Moved: the checks of lines 250, 259 and 268 where a quantity isn't 1: QuantLib's gammas drop the
            // quantities and its theta takes them squared, which the spreadsheet's values reproduce; nanoql's take
            // each once (Divergences/MargrabeGreeksQuantityTests.cs)
            if (value.Q1 == 1)
            {
                Check("gamma1", value, value.Gamma1, results.Gamma1!.Value, tolerance);
            }

            if (value.Q2 == 1)
            {
                Check("gamma2", value, value.Gamma2, results.Gamma2!.Value, tolerance);
            }

            if (value.Q1 == 1 && value.Q2 == 1)
            {
                Check("theta", value, value.Theta, results.Theta!.Value, tolerance);
            }

            Check("rho_greek", value, value.RhoGreek, results.Rho!.Value, tolerance);
        }
    }

    [Fact]
    public void TestGreeks()
    {
        Dictionary<string, Real> calculated = [], expected = [];
        Dictionary<string, Real> tolerance = new()
        {
            ["delta1"] = 1.0e-5,
            ["delta2"] = 1.0e-5,
            ["gamma1"] = 1.0e-5,
            ["gamma2"] = 1.0e-5,
            ["theta"] = 1.0e-5,
            ["rho"] = 1.0e-5,
        };

        Real[] underlyings1 = [22.0];
        Real[] underlyings2 = [20.0];
        Rate[] qRates1 = [0.06, 0.16, 0.04];
        Rate[] qRates2 = [0.04, 0.14, 0.02];
        Rate[] rRates = [0.1, 0.2, 0.08];
        double[] residualTimes = [0.1, 0.5];
        Volatility[] vols1 = [0.20];
        Volatility[] vols2 = [0.15, 0.20, 0.25];

        DayCounter dc = new Actual360();
        Date today = Today;

        foreach (double residualTime in residualTimes)
        {
            Date exDate = today + TimeToDays(residualTime);
            var exercise = new EuropeanExercise(exDate);

            // The correlation -0.5 can be different real between -1 and 1 for more tests
            Real correlation = -0.5;

            // QuantLib's loop over the correlation matrix's diagonal runs the test body once per entry
            for (int j = 0; j < 2; j++)
            {
                // The quantities of S1 and S2 can be different from 1 & 1 for more tests
                var margrabeOption = new MargrabeOption(1, 1, exercise);

                for (int l = 0; l < underlyings1.Length; l++)
                {
                    for (int m = 0; m < qRates1.Length; m++)
                    {
                        foreach (Real n in rRates)
                        {
                            for (int p = 0; p < vols1.Length; p++)
                            {
                                Real u1 = underlyings1[l], u2 = underlyings2[l], u;
                                Rate q1 = qRates1[m], q2 = qRates2[m], r = n;
                                Volatility v1 = vols1[p], v2 = vols2[p];

                                // the curves move with the evaluation date, as QuantLib's flatRate(quote, dc)
                                MargrabeOptionResults Price(Real s1, Real s2, Rate rate, Date evaluationDate)
                                {
                                    var context = new EvaluationContext { EvaluationDate = evaluationDate };
                                    YieldTermStructure rTS = new FlatForward(evaluationDate, rate, dc);
                                    var p1 = new BlackScholesMertonProcess(
                                        s1, new FlatForward(evaluationDate, q1, dc), rTS, Vol(evaluationDate, v1, dc));
                                    var p2 = new BlackScholesMertonProcess(
                                        s2, new FlatForward(evaluationDate, q2, dc), rTS, Vol(evaluationDate, v2, dc));
                                    return AnalyticEuropeanMargrabeEngine.Calculate(
                                        margrabeOption, context, p1, p2, correlation);
                                }

                                MargrabeOptionResults results = Price(u1, u2, r, today);
                                Real value = results.NPV!.Value;
                                calculated["delta1"] = results.Delta1!.Value;
                                calculated["delta2"] = results.Delta2!.Value;
                                calculated["gamma1"] = results.Gamma1!.Value;
                                calculated["gamma2"] = results.Gamma2!.Value;
                                calculated["theta"] = results.Theta!.Value;
                                calculated["rho"] = results.Rho!.Value;

                                if (value > u1 * 1.0e-5)
                                {
                                    // perturb spot and get delta1 and gamma
                                    u = u1;
                                    Real du = u * 1.0e-4;
                                    MargrabeOptionResults up = Price(u + du, u2, r, today);
                                    MargrabeOptionResults down = Price(u - du, u2, r, today);
                                    expected["delta1"] = (up.NPV!.Value - down.NPV!.Value) / (2 * du);
                                    expected["gamma1"] = (up.Delta1!.Value - down.Delta1!.Value) / (2 * du);

                                    u = u2;
                                    up = Price(u1, u + du, r, today);
                                    down = Price(u1, u - du, r, today);
                                    expected["delta2"] = (up.NPV!.Value - down.NPV!.Value) / (2 * du);
                                    expected["gamma2"] = (up.Delta2!.Value - down.Delta2!.Value) / (2 * du);

                                    // perturb rates and get rho
                                    Real dr = r * 1.0e-4;
                                    Real valueP = Price(u1, u2, r + dr, today).NPV!.Value;
                                    Real valueM = Price(u1, u2, r - dr, today).NPV!.Value;
                                    expected["rho"] = (valueP - valueM) / (2 * dr);

                                    // perturb date and get theta
                                    double dT = dc.YearFraction(today - 1, today + 1);
                                    valueM = Price(u1, u2, r, today - 1).NPV!.Value;
                                    valueP = Price(u1, u2, r, today + 1).NPV!.Value;
                                    expected["theta"] = (valueP - valueM) / dT;

                                    // compare
                                    foreach ((string greek, Real calcl) in calculated)
                                    {
                                        Real expct = expected[greek], tol = tolerance[greek];
                                        Real error = RelativeError(expct, calcl, u1);
                                        Assert.True(
                                            error <= tol,
                                            $"European option with null pay off: spot1 {u1}, spot2 {u2}, q1 {q1}, q2 {q2}, "
                                            + $"r {r}, maturity {exDate}, v1 {v1}, v2 {v2}\n    expected {greek}:   {expct}\n"
                                            + $"    calculated {greek}: {calcl}\n    error:            {error}\n"
                                            + $"    tolerance:        {tol}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TestAmericanExchangeTwoAssets()
    {
        MargrabeAmericanOptionTwoData[] values =
        [
            // Simplification : we assume that the option always exchanges S2 for S1
            // s1, s2, Q1, Q2, q1, q2, r, t, v1, v2, rho, result, tol
            // data from Haug
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.15, -0.50, 2.1357, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.20, -0.50, 2.2074, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.25, -0.50, 2.2902, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.15, 0.00, 2.0592, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.20, 0.00, 2.1032, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.25, 0.00, 2.1618, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.15, 0.50, 2.0001, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.20, 0.50, 2.0110, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.10, 0.20, 0.25, 0.50, 2.0359, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.15, -0.50, 2.8051, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.20, -0.50, 3.0288, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.25, -0.50, 3.2664, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.15, 0.00, 2.5282, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.20, 0.00, 2.6945, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.25, 0.00, 2.8893, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.15, 0.50, 2.2053, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.20, 0.50, 2.2906, 1.0e-3),
            new MargrabeAmericanOptionTwoData(22.0, 20.0, 1, 1, 0.06, 0.04, 0.10, 0.50, 0.20, 0.25, 0.50, 2.4261, 1.0e-3),
        ];

        DayCounter dc = new Actual360();

        foreach (MargrabeAmericanOptionTwoData value in values)
        {
            Date exDate = Today + TimeToDays(value.T);
            var exercise = new AmericanExercise(Today, exDate);

            YieldTermStructure rTS = new FlatForward(Today, value.R, dc);
            var stochProcess1 = new BlackScholesMertonProcess(
                value.S1, new FlatForward(Today, value.Q1Rate, dc), rTS, Vol(Today, value.V1, dc));
            var stochProcess2 = new BlackScholesMertonProcess(
                value.S2, new FlatForward(Today, value.Q2Rate, dc), rTS, Vol(Today, value.V2, dc));

            var margrabeOption = new MargrabeOption(value.Q1, value.Q2, exercise);

            // analytic engine
            Real calculated = AnalyticAmericanMargrabeEngine.Calculate(
                margrabeOption, Context, stochProcess1, stochProcess2, value.Rho).NPV!.Value;
            Real expected = value.Result;
            Real error = Math.Abs(calculated - expected);
            Real tolerance = value.Tol;
            Assert.True(
                error <= tolerance,
                $"American call option on exchange asset s2 for asset s1: s1 {value.S1}, s2 {value.S2}, "
                + $"Q1 {value.Q1}, Q2 {value.Q2}, rho {value.Rho}\n    expected   value: {expected}\n"
                + $"    calculated value: {calculated}\n    error:            {error}\n    tolerance:        {tolerance}");
        }
    }

    private static BlackVolTermStructure Vol(Date today, Volatility v, DayCounter dc) =>
        new BlackConstantVol(today, new NullCalendar(), v, dc);

    // The test suite's relativeError: the difference relative to the reference, or absolute for a zero reference.
    private static Real RelativeError(Real x1, Real x2, Real reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    // QuantLib's REPORT_FAILURE.
    private static void Check(string greekName, MargrabeOptionTwoData value, Real expected, Real calculated, Real tolerance)
    {
        Real error = Math.Abs(calculated - expected);
        Assert.True(
            error <= tolerance,
            $"European call option on exchange asset s2 for asset s1 with null payoff: s1 {value.S1}, s2 {value.S2}, "
            + $"Q1 {value.Q1}, Q2 {value.Q2}, q1 {value.Q1Rate}, q2 {value.Q2Rate}, r {value.R}, t {value.T}, "
            + $"v1 {value.V1}, v2 {value.V2}, rho {value.Rho}\n    expected   {greekName}: {expected}\n"
            + $"    calculated {greekName}: {calculated}\n    error:            {error}\n    tolerance:        {tolerance}");
    }

    private readonly record struct MargrabeOptionTwoData(
        Real S1,
        Real S2,
        Integer Q1,
        Integer Q2,
        Rate Q1Rate,
        Rate Q2Rate,
        Rate R,
        double T,
        Volatility V1,
        Volatility V2,
        Real Rho,
        Real Result,
        Real Delta1,
        Real Delta2,
        Real Gamma1,
        Real Gamma2,
        Real Theta,
        Real RhoGreek,
        Real Tol);

    private readonly record struct MargrabeAmericanOptionTwoData(
        Real S1,
        Real S2,
        Integer Q1,
        Integer Q2,
        Rate Q1Rate,
        Rate Q2Rate,
        Rate R,
        double T,
        Volatility V1,
        Volatility V2,
        Real Rho,
        Real Result,
        Real Tol);
}
