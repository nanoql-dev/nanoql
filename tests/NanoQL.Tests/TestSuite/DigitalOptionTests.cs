// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 Neil Firth
// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/digitaloption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

/// <summary>
/// QuantLib's digital option cases, with QuantLib's master's <c>testCashAtExpiryOrNothingAmericanLowVolatility</c>
/// (#2855, taken as a fix).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DigitalOptionTests
{
    // The test runner's evaluation date.
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, Month.September, 2015) };

    [Fact]
    public void TestCashOrNothingEuropeanValues()
    {
        // Testing European cash-or-nothing digital option...

        // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 - pag 88
        //        type, strike,  spot,    q,    r,    t,  vol,  value, tol
        CheckValue(new CashOrNothingPayoff(OptionType.Put, 80.00, 10.0), 100.0, 0.06, 0.06, 0.75, 0.35, 2.6710, 1e-4);
    }

    [Fact]
    public void TestAssetOrNothingEuropeanValues()
    {
        // Testing European asset-or-nothing digital option...

        // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 - pag 90
        //        type, strike, spot,    q,    r,    t,  vol,   value, tol
        CheckValue(new AssetOrNothingPayoff(OptionType.Put, 65.00), 70.0, 0.05, 0.07, 0.50, 0.27, 20.2069, 1e-4);
    }

    [Fact]
    public void TestGapEuropeanValues()
    {
        // Testing European gap digital option...

        // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 - pag 88
        //        type, strike, spot,    q,    r,    t,  vol,   value, tol
        CheckValue(new GapPayoff(OptionType.Call, 50.00, 57.00), 50.0, 0.00, 0.09, 0.50, 0.20, -0.0053, 1e-4);
    }

    [Fact]
    public void TestCashAtHitOrNothingAmericanValues()
    {
        // Testing American cash-(at-hit)-or-nothing digital option...

        DigitalOptionData[] values =
        [
            //        type, strike,   spot,    q,    r,   t,  vol,   value, tol
            // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 - pag 95, case 1,2
            new(OptionType.Put, 100.00, 105.00, 0.00, 0.10, 0.5, 0.20, 9.7264, 1e-4, true),
            new(OptionType.Call, 100.00, 95.00, 0.00, 0.10, 0.5, 0.20, 11.6553, 1e-4, true),

            // the following cases are not taken from a reference paper or book
            // in the money options (guaranteed immediate payoff)
            new(OptionType.Call, 100.00, 105.00, 0.00, 0.10, 0.5, 0.20, 15.0000, 1e-16, true),
            new(OptionType.Put, 100.00, 95.00, 0.00, 0.10, 0.5, 0.20, 15.0000, 1e-16, true),

            // non null dividend (cross-tested with MC simulation)
            new(OptionType.Put, 100.00, 105.00, 0.20, 0.10, 0.5, 0.20, 12.2715, 1e-4, true),
            new(OptionType.Call, 100.00, 95.00, 0.20, 0.10, 0.5, 0.20, 8.9109, 1e-4, true),
            new(OptionType.Call, 100.00, 105.00, 0.20, 0.10, 0.5, 0.20, 15.0000, 1e-16, true),
            new(OptionType.Put, 100.00, 95.00, 0.20, 0.10, 0.5, 0.20, 15.0000, 1e-16, true),
        ];

        foreach (DigitalOptionData value in values)
        {
            var payoff = new CashOrNothingPayoff(value.Type, value.Strike, 15.00);
            CheckAmericanValue(payoff, value, payoffAtExpiry: false);
        }
    }

    [Fact]
    public void TestAssetAtHitOrNothingAmericanValues()
    {
        // Testing American asset-(at-hit)-or-nothing digital option...

        DigitalOptionData[] values =
        [
            //        type, strike,   spot,    q,    r,   t,  vol,   value, tol
            // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 - pag 95, case 3,4
            new(OptionType.Put, 100.00, 105.00, 0.00, 0.10, 0.5, 0.20, 64.8426, 1e-04, true), // Haug value is wrong here, Haug VBA code is right
            new(OptionType.Call, 100.00, 95.00, 0.00, 0.10, 0.5, 0.20, 77.7017, 1e-04, true), // Haug value is wrong here, Haug VBA code is right

            // data from Haug VBA code results
            new(OptionType.Put, 100.00, 105.00, 0.01, 0.10, 0.5, 0.20, 65.7811, 1e-04, true),
            new(OptionType.Call, 100.00, 95.00, 0.01, 0.10, 0.5, 0.20, 76.8858, 1e-04, true),

            // in the money options  (guaranteed immediate payoff = spot)
            new(OptionType.Call, 100.00, 105.00, 0.00, 0.10, 0.5, 0.20, 105.0000, 1e-16, true),
            new(OptionType.Put, 100.00, 95.00, 0.00, 0.10, 0.5, 0.20, 95.0000, 1e-16, true),
            new(OptionType.Call, 100.00, 105.00, 0.01, 0.10, 0.5, 0.20, 105.0000, 1e-16, true),
            new(OptionType.Put, 100.00, 95.00, 0.01, 0.10, 0.5, 0.20, 95.0000, 1e-16, true),
        ];

        foreach (DigitalOptionData value in values)
        {
            var payoff = new AssetOrNothingPayoff(value.Type, value.Strike);
            CheckAmericanValue(payoff, value, payoffAtExpiry: false);
        }
    }

    [Fact]
    public void TestCashAtExpiryOrNothingAmericanValues()
    {
        // Testing American cash-(at-expiry)-or-nothing digital option...

        DigitalOptionData[] values =
        [
            //        type, strike,   spot,    q,    r,   t,  vol,   value, tol
            // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 - pag 95, case 5,6,9,10
            new(OptionType.Put, 100.00, 105.00, 0.00, 0.10, 0.5, 0.20, 9.3604, 1e-4, true),
            new(OptionType.Call, 100.00, 95.00, 0.00, 0.10, 0.5, 0.20, 11.2223, 1e-4, true),
            new(OptionType.Put, 100.00, 105.00, 0.00, 0.10, 0.5, 0.20, 4.9081, 1e-4, false),
            new(OptionType.Call, 100.00, 95.00, 0.00, 0.10, 0.5, 0.20, 3.0461, 1e-4, false),

            // in the money options (guaranteed discounted payoff)
            new(OptionType.Call, 100.00, 105.00, 0.00, 0.10, 0.5, 0.20, 15.0000 * Math.Exp(-0.05), 1e-12, true),
            new(OptionType.Put, 100.00, 95.00, 0.00, 0.10, 0.5, 0.20, 15.0000 * Math.Exp(-0.05), 1e-12, true),

            // out of bonds case
            new(OptionType.Call, 2.37, 2.33, 0.07, 0.43, 0.19, 0.005, 0.0000, 1e-4, false),
        ];

        foreach (DigitalOptionData value in values)
        {
            var payoff = new CashOrNothingPayoff(value.Type, value.Strike, 15.0);
            CheckAmericanValue(payoff, value, payoffAtExpiry: true);
        }
    }

    [Fact]
    public void TestCashAtExpiryOrNothingAmericanLowVolatility()
    {
        // Testing American cash-(at-expiry)-or-nothing digital option at low volatility...

        // expected values are the closed form evaluated in 50-digit arithmetic
        DigitalOptionData[] values =
        [
            //        type, strike,   spot,    q,     r,   t,   vol,              value,  tol
            new(OptionType.Call, 105.00, 100.00, 0.03, 0.08, 1.0, 0.001, 8.2035184454191468, 1e-8, true),
            new(OptionType.Put, 95.00, 100.00, 0.03, -0.02, 1.0, 0.001, 1.0176365525852901, 1e-8, true),
            new(OptionType.Call, 115.00, 100.00, 0.00, 0.03, 5.0, 0.003, 8.0820190229249992, 1e-8, true),
            new(OptionType.Put, 85.00, 100.00, 0.03, 0.00, 5.0, 0.003, 0.32750723018931787, 1e-8, true),
        ];

        foreach (DigitalOptionData value in values)
        {
            var payoff = new CashOrNothingPayoff(value.Type, value.Strike, 10.0);
            CheckAmericanValue(payoff, value, payoffAtExpiry: true);
        }
    }

    [Fact]
    public void TestAssetAtExpiryOrNothingAmericanValues()
    {
        // Testing American asset-(at-expiry)-or-nothing digital option...

        DigitalOptionData[] values =
        [
            //        type, strike,   spot,    q,    r,   t,  vol,   value, tol
            // "Option pricing formulas", E.G. Haug, McGraw-Hill 1998 - pag 95, case 7,8,11,12
            new(OptionType.Put, 100.00, 105.00, 0.00, 0.10, 0.5, 0.20, 64.8426, 1e-04, true),
            new(OptionType.Call, 100.00, 95.00, 0.00, 0.10, 0.5, 0.20, 77.7017, 1e-04, true),
            new(OptionType.Put, 100.00, 105.00, 0.00, 0.10, 0.5, 0.20, 40.1574, 1e-04, false),
            new(OptionType.Call, 100.00, 95.00, 0.00, 0.10, 0.5, 0.20, 17.2983, 1e-04, false),

            // data from Haug VBA code results
            new(OptionType.Put, 100.00, 105.00, 0.01, 0.10, 0.5, 0.20, 65.5291, 1e-04, true),
            new(OptionType.Call, 100.00, 95.00, 0.01, 0.10, 0.5, 0.20, 76.5951, 1e-04, true),

            // in the money options (guaranteed discounted payoff = forward * riskFreeDiscount
            //                                                    = spot * dividendDiscount)
            new(OptionType.Call, 100.00, 105.00, 0.00, 0.10, 0.5, 0.20, 105.0000, 1e-12, true),
            new(OptionType.Put, 100.00, 95.00, 0.00, 0.10, 0.5, 0.20, 95.0000, 1e-12, true),
            new(OptionType.Call, 100.00, 105.00, 0.01, 0.10, 0.5, 0.20, 105.0000 * Math.Exp(-0.005), 1e-12, true),
            new(OptionType.Put, 100.00, 95.00, 0.01, 0.10, 0.5, 0.20, 95.0000 * Math.Exp(-0.005), 1e-12, true),
        ];

        foreach (DigitalOptionData value in values)
        {
            var payoff = new AssetOrNothingPayoff(value.Type, value.Strike);
            CheckAmericanValue(payoff, value, payoffAtExpiry: true);
        }
    }

    [Fact]
    public void TestCashAtHitOrNothingAmericanGreeks()
    {
        // Testing American cash-(at-hit)-or-nothing digital option greeks...

        Dictionary<string, Real> calculated = [], expected = [], tolerance = [];
        tolerance["delta"] = 5.0e-5;
        tolerance["gamma"] = 5.0e-5;
        tolerance["rho"] = 5.0e-5;

        OptionType[] types = [OptionType.Call, OptionType.Put];
        Real[] strikes = [50.0, 99.5, 100.5, 150.0];
        Real cashPayoff = 100.0;
        Real[] underlyings = [100];
        Rate[] qRates = [0.04, 0.05, 0.06];
        Rate[] rRates = [0.01, 0.05, 0.15];
        Volatility[] vols = [0.11, 0.5, 1.2];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        // there is no cycling on different residual times
        Date exDate = today + 360;
        Exercise exercise = new EuropeanExercise(exDate);
        Exercise amExercise = new AmericanExercise(today, exDate, false);
        Exercise[] exercises = [exercise, amExercise];

        Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults>[] engines =
        [
            AnalyticEuropeanEngine.Calculate,
            AnalyticDigitalAmericanEngine.Calculate,
        ];

        bool knockin = true;
        for (Size j = 0; j < engines.Length; j++)
        {
            foreach (OptionType type in types)
            {
                foreach (Real strike in strikes)
                {
                    var payoff = new CashOrNothingPayoff(type, strike, cashPayoff);

                    var opt = new VanillaOption(payoff, exercises[j]);

                    foreach (Real u in underlyings)
                    {
                        foreach (Real q in qRates)
                        {
                            foreach (Real r in rRates)
                            {
                                foreach (Real v in vols)
                                {
                                    // test data
                                    OneAssetOptionResults Price(Real spot, Rate rate) => engines[j](
                                        opt,
                                        context,
                                        new BlackScholesMertonProcess(
                                            spot, new FlatForward(today, q, dc), new FlatForward(today, rate, dc),
                                            new BlackConstantVol(today, new NullCalendar(), v, dc)));

                                    // theta, dividend rho and vega are not available for
                                    // digital option with american exercise. Greeks of
                                    // digital options with european payoff are tested
                                    // in the europeanoption.cpp test
                                    OneAssetOptionResults results = Price(u, r);
                                    Real value = results.NPV!.Value;
                                    calculated["delta"] = results.Delta!.Value;
                                    calculated["gamma"] = results.Gamma!.Value;
                                    calculated["rho"] = results.Rho!.Value;

                                    if (value > 1.0e-6)
                                    {
                                        // perturb spot and get delta and gamma
                                        Real du = u * 1.0e-4;
                                        OneAssetOptionResults p = Price(u + du, r), m = Price(u - du, r);
                                        expected["delta"] = (p.NPV!.Value - m.NPV!.Value) / (2 * du);
                                        expected["gamma"] = (p.Delta!.Value - m.Delta!.Value) / (2 * du);

                                        // perturb rates and get rho and dividend rho
                                        Spread dr = r * 1.0e-4;
                                        Real valueP = Price(u, r + dr).NPV!.Value;
                                        Real valueM = Price(u, r - dr).NPV!.Value;
                                        expected["rho"] = (valueP - valueM) / (2 * dr);

                                        // check
                                        foreach ((string greek, Real calcl) in calculated)
                                        {
                                            Real expct = expected[greek], tol = tolerance[greek];
                                            Real error = RelativeError(expct, calcl, value);
                                            if (error > tol)
                                            {
                                                Assert.Fail(
                                                    $"{exercises[j].Type} {type} option with {payoff.Name} payoff: spot {u}, "
                                                    + $"strike {strike}, q {q}, r {r}, vol {v}: expected {greek} {expct}, "
                                                    + $"calculated {calcl}, error {error}, tolerance {tol}, knock_in {knockin}");
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

    [Fact]
    public void TestCashAtHitOrNothingAmericanLowVolatility()
    {
        // Testing American cash-(at-hit)-or-nothing digital option at low volatility...

        // expected values are the closed form evaluated in 50-digit arithmetic
        DigitalOptionData[] values =
        [
            //        type,  strike,  spot,    q,     r,   t,    vol,            value,  tol
            new(OptionType.Call, 105.00, 100.00, 0.03, 0.08, 1.0, 0.01, 5.47651520145713, 1e-8, true),
            new(OptionType.Call, 105.00, 100.00, 0.03, 0.08, 1.0, 0.003, 6.18748832699530, 1e-8, true),
            new(OptionType.Call, 100.00 * Math.Exp(0.03), 100.00, 0.00, 0.03, 1.0, 0.001, 4.91867026469375, 1e-8, true),
            new(OptionType.Put, 99.00, 100.00, 0.03, -0.02, 1.0, 0.0001, 10.0402822567873, 1e-8, true),
        ];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };

        foreach (DigitalOptionData value in values)
        {
            var payoff = new CashOrNothingPayoff(value.Type, value.Strike, 10.0);
            Date exDate = today + TimeToDays(value.T);
            Exercise amExercise = new AmericanExercise(today, exDate);

            var opt = new VanillaOption(payoff, amExercise);
            OneAssetOptionResults Price(Real spot, Rate rate) => AnalyticDigitalAmericanEngine.Calculate(
                opt,
                context,
                new BlackScholesMertonProcess(
                    spot, new FlatForward(today, value.Q, dc), new FlatForward(today, rate, dc),
                    new BlackConstantVol(today, new NullCalendar(), value.V, dc)));

            OneAssetOptionResults results = Price(value.S, value.R);
            Real calculated = results.NPV!.Value;
            Real error = Math.Abs(calculated - value.Result);
            if (!(error <= value.Tol))
            {
                Assert.Fail(
                    $"American {value.Type} option with {payoff.Name} payoff: expected value {value.Result}, "
                    + $"calculated {calculated}, error {error}, tolerance {value.Tol}");
            }

            Real ds = 1.0e-4, dr = 1.0e-7;
            Real up = Price(value.S + ds, value.R).NPV!.Value;
            Real down = Price(value.S - ds, value.R).NPV!.Value;
            Real expectedDelta = (up - down) / (2.0 * ds);
            up = Price(value.S, value.R + dr).NPV!.Value;
            down = Price(value.S, value.R - dr).NPV!.Value;
            Real expectedRho = (up - down) / (2.0 * dr);

            Real delta = results.Delta!.Value;
            error = Math.Abs(delta - expectedDelta);
            if (!(error <= 1.0e-4 * Math.Max(1.0, Math.Abs(expectedDelta))))
            {
                Assert.Fail(
                    $"American {value.Type} option with {payoff.Name} payoff: expected delta {expectedDelta}, "
                    + $"calculated {delta}, error {error}, tolerance 1e-4");
            }

            Real rho = results.Rho!.Value;
            error = Math.Abs(rho - expectedRho);
            if (!(error <= 1.0e-4 * Math.Max(1.0, Math.Abs(expectedRho))))
            {
                Assert.Fail(
                    $"American {value.Type} option with {payoff.Name} payoff: expected rho {expectedRho}, "
                    + $"calculated {rho}, error {error}, tolerance 1e-4");
            }
        }
    }

    [Fact]
    public void TestMCCashAtHit()
    {
        // Testing Monte Carlo cash-(at-hit)-or-nothing American engine...

        DigitalOptionData[] values =
        [
            //        type, strike,   spot,    q,    r,   t,  vol,   value, tol
            new(OptionType.Put, 100.00, 105.00, 0.20, 0.10, 0.5, 0.20, 12.2715, 1e-2, true),
            new(OptionType.Call, 100.00, 95.00, 0.20, 0.10, 0.5, 0.20, 8.9109, 1e-2, true),
        ];

        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        Size timeStepsPerYear = 90;
        Size maxSamples = 1000000;
        long seed = 1;

        foreach (DigitalOptionData value in values)
        {
            var payoff = new CashOrNothingPayoff(value.Type, value.Strike, 15.0);
            Date exDate = today + TimeToDays(value.T);
            Exercise amExercise = new AmericanExercise(today, exDate);

            var stochProcess = new BlackScholesMertonProcess(
                value.S, new FlatForward(today, value.Q, dc), new FlatForward(today, value.R, dc),
                new BlackConstantVol(today, new NullCalendar(), value.V, dc));

            Size requiredSamples = (Size)(Math.Pow(2.0, 14) - 1);
            Func<VanillaOption, EvaluationContext, OneAssetOptionResults> mcldEngine =
                new MakeMCDigitalEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, seed)
                    .WithStepsPerYear(timeStepsPerYear)
                    .WithBrownianBridge()
                    .WithSamples(requiredSamples)
                    .WithMaxSamples(maxSamples)
                    .ToEngine();

            var opt = new VanillaOption(payoff, amExercise);

            Real calculated = mcldEngine(opt, RunnerContext).NPV!.Value;
            Real error = Math.Abs(calculated - value.Result);
            if (error > value.Tol)
            {
                Assert.Fail(
                    $"American {value.Type} option with {payoff.Name} payoff: expected value {value.Result}, "
                    + $"calculated {calculated}, error {error}, tolerance {value.Tol}");
            }
        }
    }

    // The body each European case runs on its one value.
    private static void CheckValue(
        StrikedTypePayoff payoff, Real s, Rate q, Rate r, double t, Volatility v, Real result, Real tol)
    {
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        Date exDate = today + TimeToDays(t);
        Exercise exercise = new EuropeanExercise(exDate);

        var stochProcess = new BlackScholesMertonProcess(
            s, new FlatForward(today, q, dc), new FlatForward(today, r, dc), new BlackConstantVol(today, new NullCalendar(), v, dc));

        var opt = new VanillaOption(payoff, exercise);

        Real calculated = AnalyticEuropeanEngine.Calculate(opt, RunnerContext, stochProcess).NPV!.Value;
        Real error = Math.Abs(calculated - result);
        if (error > tol)
        {
            Assert.Fail(
                $"European {payoff.OptionType} option with {payoff.Name} payoff: expected value {result}, calculated "
                + $"{calculated}, error {error}, tolerance {tol}");
        }
    }

    // The body each American value case runs: the knock-in engine, or the knock-out one where the data says so; a
    // payoff at expiry where the case's exercise pays then.
    private static void CheckAmericanValue(StrikedTypePayoff payoff, DigitalOptionData value, bool payoffAtExpiry)
    {
        DayCounter dc = new Actual360();
        Date today = Date.TodaysDate();

        Date exDate = today + TimeToDays(value.T);
        Exercise amExercise = new AmericanExercise(today, exDate, payoffAtExpiry);

        var stochProcess = new BlackScholesMertonProcess(
            value.S, new FlatForward(today, value.Q, dc), new FlatForward(today, value.R, dc),
            new BlackConstantVol(today, new NullCalendar(), value.V, dc));

        var opt = new VanillaOption(payoff, amExercise);

        OneAssetOptionResults results = value.Knockin
            ? AnalyticDigitalAmericanEngine.Calculate(opt, RunnerContext, stochProcess)
            : AnalyticDigitalAmericanKOEngine.Calculate(opt, RunnerContext, stochProcess);
        Real calculated = results.NPV!.Value;
        Real error = Math.Abs(calculated - value.Result);
        if (!(error <= value.Tol))
        {
            Assert.Fail(
                $"American {payoff.OptionType} option with {payoff.Name} payoff: expected value {value.Result}, "
                + $"calculated {calculated}, error {error}, tolerance {value.Tol}, knock_in {value.Knockin}");
        }
    }

    private static Real RelativeError(Real x1, Real x2, Real reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private readonly record struct DigitalOptionData(
        OptionType Type, Real Strike, Real S, Rate Q, Rate R, double T, Volatility V, Real Result, Real Tol, bool Knockin);
}
