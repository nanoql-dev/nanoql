// SPDX-FileCopyrightText: Copyright (C) 2004, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/cliquetoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CliquetOptionTests
{
    [Fact]
    public void TestValues()
    {
        // Testing Cliquet option values...
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();

        Real spot = 60.0;
        Rate qRate = 0.04;
        YieldTermStructure qTS = new FlatForward(today, qRate, dc);
        Rate rRate = 0.08;
        YieldTermStructure rTS = new FlatForward(today, rRate, dc);
        Volatility vol = 0.30;
        BlackVolTermStructure volTS = new BlackConstantVol(today, new NullCalendar(), vol, dc);

        var process = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        Date[] reset = [today + 90];
        Date maturity = today + 360;
        OptionType type = OptionType.Call;
        Real moneyness = 1.1;

        var payoff = new PercentageStrikePayoff(type, moneyness);
        var exercise = new EuropeanExercise(maturity);

        var option = new CliquetOption(payoff, exercise, reset);

        Real calculated = AnalyticCliquetEngine.Calculate(option, context, process).NPV!.Value;
        Real expected = 4.4064; // Haug, p.37
        Real error = Math.Abs(calculated - expected);
        Real tolerance = 1e-4;
        if (error > tolerance)
        {
            ReportFailure("value", payoff, exercise, spot, qRate, rRate, today, vol, expected, calculated, error, tolerance);
        }
    }

    [Fact]
    public void TestGreeks()
    {
        // Testing Cliquet option greeks...
        TestOptionGreeks(AnalyticCliquetEngine.Calculate);
    }

    [Fact]
    public void TestPerformanceGreeks()
    {
        // Testing performance option greeks...
        TestOptionGreeks(AnalyticPerformanceEngine.Calculate);
    }

    [Fact]
    public void TestMcPerformance()
    {
        // Testing Monte Carlo performance engine against analytic results...
        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] moneyness = [0.9, 1.1];
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.04, 0.06];
        Rate[] rRates = [0.01, 0.10];
        Integer[] lengths = [2, 4];
        Frequency[] frequencies = [Frequency.Semiannual, Frequency.Quarterly];
        Volatility[] vols = [0.10, 0.90];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (OptionType type in types)
        {
            foreach (Real moneynes in moneyness)
            {
                foreach (Integer length in lengths)
                {
                    foreach (Frequency frequencie in frequencies)
                    {
                        var tenor = new Period(frequencie);
                        var maturity = new EuropeanExercise(today + (length * tenor));

                        var payoff = new PercentageStrikePayoff(type, moneynes);

                        var reset = new List<Date>();
                        for (Date d = today + tenor; d < maturity.LastDate(); d += tenor)
                        {
                            reset.Add(d);
                        }

                        var option = new CliquetOption(payoff, maturity, reset);

                        foreach (Real u in underlyings)
                        {
                            foreach (Real m in qRates)
                            {
                                foreach (Real n in rRates)
                                {
                                    foreach (Volatility v in vols)
                                    {
                                        Rate q = m, r = n;
                                        GeneralizedBlackScholesProcess process = Process(u, q, r, v, dc, context);

                                        Real refValue = AnalyticPerformanceEngine.Calculate(option, context, process).NPV!.Value;

                                        Func<CliquetOption, EvaluationContext, OneAssetOptionResults> mcEngine =
                                            new MakeMCPerformanceEngine<PseudoRandom, GeneralStatistics>(process, 42)
                                                .WithBrownianBridge()
                                                .WithAbsoluteTolerance(5.0e-3)
                                                .ToEngine();
                                        Real value = mcEngine(option, context).NPV!.Value;

                                        Real error = Math.Abs(refValue - value);
                                        Real tolerance = 1.5e-2;
                                        if (error > tolerance)
                                        {
                                            ReportFailure(
                                                "value", payoff, maturity, u, q, r, today, v, refValue, value, error, tolerance);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // QuantLib's testOptionGreeks<T>, the engine T's Calculate.
    private static void TestOptionGreeks(
        Func<CliquetOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults> engine)
    {
        var tolerance = new Dictionary<string, Real>
        {
            ["delta"] = 1.0e-5,
            ["gamma"] = 1.0e-5,
            ["theta"] = 1.0e-5,
            ["rho"] = 1.0e-5,
            ["divRho"] = 1.0e-5,
            ["vega"] = 1.0e-5,
        };

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] moneyness = [0.9, 1.0, 1.1];
        Real[] underlyings = [100.0];
        Rate[] qRates = [0.04, 0.05, 0.06];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Integer[] lengths = [1, 2];
        Frequency[] frequencies = [Frequency.Semiannual, Frequency.Quarterly];
        Volatility[] vols = [0.11, 0.50, 1.20];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (OptionType type in types)
        {
            foreach (Real moneynes in moneyness)
            {
                foreach (Integer length in lengths)
                {
                    foreach (Frequency frequencie in frequencies)
                    {
                        var maturity = new EuropeanExercise(today + new Period(length, TimeUnit.Years));

                        var payoff = new PercentageStrikePayoff(type, moneynes);

                        var reset = new List<Date>();
                        for (Date d = today + new Period(frequencie); d < maturity.LastDate(); d += new Period(frequencie))
                        {
                            reset.Add(d);
                        }

                        var option = new CliquetOption(payoff, maturity, reset);

                        foreach (Real u in underlyings)
                        {
                            foreach (Real m in qRates)
                            {
                                foreach (Real n in rRates)
                                {
                                    foreach (Volatility v in vols)
                                    {
                                        Rate q = m, r = n;
                                        Real Npv(Real spot, Rate qr, Rate rr, Volatility vv, EvaluationContext c) =>
                                            engine(option, c, Process(spot, qr, rr, vv, dc, c)).NPV!.Value;

                                        OneAssetOptionResults results = engine(option, context, Process(u, q, r, v, dc, context));
                                        Real value = results.NPV!.Value;
                                        var calculated = new Dictionary<string, Real>
                                        {
                                            ["delta"] = results.Delta!.Value,
                                            ["gamma"] = results.Gamma!.Value,
                                            ["theta"] = results.Theta!.Value,
                                            ["rho"] = results.Rho!.Value,
                                            ["divRho"] = results.DividendRho!.Value,
                                            ["vega"] = results.Vega!.Value,
                                        };
                                        var expected = new Dictionary<string, Real>();

                                        if (value > u * 1.0e-5)
                                        {
                                            // perturb spot and get delta and gamma
                                            Real du = u * 1.0e-4;
                                            OneAssetOptionResults up = engine(option, context, Process(u + du, q, r, v, dc, context));
                                            Real valueP = up.NPV!.Value, deltaP = up.Delta!.Value;
                                            OneAssetOptionResults down = engine(option, context, Process(u - du, q, r, v, dc, context));
                                            Real valueM = down.NPV!.Value, deltaM = down.Delta!.Value;
                                            expected["delta"] = (valueP - valueM) / (2 * du);
                                            expected["gamma"] = (deltaP - deltaM) / (2 * du);

                                            // perturb rates and get rho and dividend rho
                                            Spread dr = r * 1.0e-4;
                                            valueP = Npv(u, q, r + dr, v, context);
                                            valueM = Npv(u, q, r - dr, v, context);
                                            expected["rho"] = (valueP - valueM) / (2 * dr);

                                            Spread dq = q * 1.0e-4;
                                            valueP = Npv(u, q + dq, r, v, context);
                                            valueM = Npv(u, q - dq, r, v, context);
                                            expected["divRho"] = (valueP - valueM) / (2 * dq);

                                            // perturb volatility and get vega
                                            Volatility dv = v * 1.0e-4;
                                            valueP = Npv(u, q, r, v + dv, context);
                                            valueM = Npv(u, q, r, v - dv, context);
                                            expected["vega"] = (valueP - valueM) / (2 * dv);

                                            // perturb date and get theta
                                            double dT = dc.YearFraction(today - 1, today + 1);
                                            var yesterday = new EvaluationContext { EvaluationDate = today - 1 };
                                            var tomorrow = new EvaluationContext { EvaluationDate = today + 1 };
                                            valueM = Npv(u, q, r, v, yesterday);
                                            valueP = Npv(u, q, r, v, tomorrow);
                                            expected["theta"] = (valueP - valueM) / dT;

                                            // compare
                                            foreach (string greek in calculated.Keys)
                                            {
                                                Real expct = expected[greek],
                                                    calcl = calculated[greek],
                                                    tol = tolerance[greek];
                                                Real error = RelativeError(expct, calcl, u);
                                                if (error > tol)
                                                {
                                                    ReportFailure(greek, payoff, maturity, u, q, r, today, v, expct, calcl, error, tol);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // The test suite's flatRate(rate, dc) and flatVol(vol, dc), from the evaluation date, in a Black-Scholes-Merton
    // process: QuantLib's quotes set to the loop's values.
    private static GeneralizedBlackScholesProcess Process(
        Real spot, Rate q, Rate r, Volatility v, DayCounter dc, EvaluationContext context) =>
        new BlackScholesMertonProcess(
            spot,
            FlatForward.Create(0, new NullCalendar(), q, dc, context),
            FlatForward.Create(0, new NullCalendar(), r, dc, context),
            BlackConstantVol.Create(0, new NullCalendar(), v, dc, context));

    // The test suite's relativeError: the difference relative to the reference, or absolute for a zero reference.
    private static Real RelativeError(Real x1, Real x2, Real reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    private static void ReportFailure(
        string greekName,
        PercentageStrikePayoff payoff,
        Exercise exercise,
        Real s,
        Rate q,
        Rate r,
        Date today,
        Volatility v,
        Real expected,
        Real calculated,
        Real error,
        Real tolerance) =>
        Assert.Fail(
            $"{payoff.OptionType} option:\n    spot value:       {s}\n    moneyness:        {payoff.Strike}\n"
            + $"    dividend yield:   {q}\n    risk-free rate:   {r}\n    reference date:   {today}\n"
            + $"    maturity:         {exercise.LastDate()}\n    volatility:       {v}\n\n"
            + $"    expected   {greekName}: {expected}\n    calculated {greekName}: {calculated}\n"
            + $"    error:            {error}\n    tolerance:        {tolerance}");
}
