// SPDX-FileCopyrightText: Copyright (C) 2009 Dimitri Reiswich
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/compoundoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class CompoundOptionTests
{
    [Fact]
    public void TestPutCallParity()
    {
        // Testing compound-option put-call parity...

        // Test Put Call Parity for compound options.
        // Formula taken from: "Foreign Exchange Risk", Wystup, Risk 2002
        // Page 81, Equation 9.5
        CompoundOptionData[] values =
        [
            // type Mother, typeDaughter, strike Mother, strike Daughter,  spot,    q,    r,    t Mother, t Daughter,  vol
            new(OptionType.Put, OptionType.Call, 50.0, 520.0, 500.0, 0.03, 0.08, 0.25, 0.5, 0.35),
            new(OptionType.Call, OptionType.Call, 50.0, 520.0, 500.0, 0.03, 0.08, 0.25, 0.5, 0.35),
            new(OptionType.Call, OptionType.Put, 50.0, 520.0, 500.0, 0.03, 0.08, 0.25, 0.5, 0.35),
            new(OptionType.Call, OptionType.Call, 0.05, 1.14, 1.20, 0.0, 0.01, 0.5, 2.0, 0.11),
            new(OptionType.Call, OptionType.Put, 0.05, 1.14, 1.20, 0.0, 0.01, 0.5, 2.0, 0.11),
            new(OptionType.Call, OptionType.Call, 10.0, 122.0, 120.0, 0.06, 0.02, 0.1, 0.7, 0.22),
            new(OptionType.Call, OptionType.Put, 10.0, 122.0, 120.0, 0.06, 0.02, 0.1, 0.7, 0.22),
            new(OptionType.Call, OptionType.Call, 0.4, 8.2, 8.0, 0.05, 0.00, 2.0, 3.0, 0.08),
            new(OptionType.Call, OptionType.Put, 0.4, 8.2, 8.0, 0.05, 0.00, 2.0, 3.0, 0.08),
            new(OptionType.Call, OptionType.Call, 0.02, 1.6, 1.6, 0.013, 0.022, 0.45, 0.5, 0.17),
            new(OptionType.Call, OptionType.Put, 0.02, 1.6, 1.6, 0.013, 0.022, 0.45, 0.5, 0.17),
        ];

        DayCounter dc = new Actual360();
        Date todaysDate = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        foreach (CompoundOptionData value in values)
        {
            var payoffMotherCall = new PlainVanillaPayoff(OptionType.Call, value.StrikeMother);
            var payoffMotherPut = new PlainVanillaPayoff(OptionType.Put, value.StrikeMother);
            var payoffDaughter = new PlainVanillaPayoff(value.TypeDaughter, value.StrikeDaughter);

            Date matDateMom = todaysDate + TimeToDays(value.TMother);
            Date matDateDaughter = todaysDate + TimeToDays(value.TDaughter);

            var exerciseCompound = new EuropeanExercise(matDateMom);
            var exerciseDaughter = new EuropeanExercise(matDateDaughter);

            YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), value.R, dc, context);
            YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), value.Q, dc, context);
            BlackVolTermStructure volTS = new BlackConstantVol(todaysDate, new NullCalendar(), value.V, dc);

            var compoundOptionCall = new CompoundOption(payoffMotherCall, exerciseCompound, payoffDaughter, exerciseDaughter);
            var compoundOptionPut = new CompoundOption(payoffMotherPut, exerciseCompound, payoffDaughter, exerciseDaughter);
            var vanillaOption = new VanillaOption(payoffDaughter, exerciseDaughter);

            var stochProcess = new BlackScholesMertonProcess(value.S, qTS, rTS, volTS);

            Real discFact = rTS.Discount(matDateMom);
            Real discStrike = value.StrikeMother * discFact;

            Real calculated = AnalyticCompoundOptionEngine.Calculate(compoundOptionCall, context, stochProcess).NPV!.Value
                + discStrike
                - AnalyticCompoundOptionEngine.Calculate(compoundOptionPut, context, stochProcess).NPV!.Value
                - AnalyticEuropeanEngine.Calculate(vanillaOption, context, stochProcess).NPV!.Value;

            Real expected = 0.0;
            Real error = Math.Abs(calculated - expected);
            Real tolerance = 1.0e-8;

            if (error > tolerance)
            {
                ReportFailure(
                    "put call parity", payoffMotherCall, payoffDaughter, exerciseCompound, exerciseDaughter, value,
                    todaysDate, value.Delta, calculated, error, tolerance);
            }
        }
    }

    [Fact]
    public void TestValues()
    {
        // Testing compound-option values and greeks...
        CompoundOptionData[] values =
        [
            // type Mother, typeDaughter, strike Mother, strike Daughter,  spot,    q,    r,    t Mother, t Daughter,  vol,   value,    tol, delta, gamma, vega, theta
            // Tolerance is taken to be pretty high with 1.0e-3, since the price/theta is very sensitive with respect to
            // the implementation of the bivariate normal - which differs in the various implementations.
            // Option Value Taken from Haug 2007, Greeks from www.sitmo.com
            new(OptionType.Put, OptionType.Call, 50.0, 520.0, 500.0, 0.03, 0.08, 0.25, 0.5, 0.35, 21.1965, 1.0e-3, -0.1966, 0.0007, -32.1241, -3.3837),

            // *********************************************************
            // Option Values and Greeks taken from www.sitmo.com
            new(OptionType.Call, OptionType.Call, 50.0, 520.0, 500.0, 0.03, 0.08, 0.25, 0.5, 0.35, 17.5945, 1.0e-3, 0.3219, 0.0038, 106.5185, -65.1614),
            new(OptionType.Call, OptionType.Put, 50.0, 520.0, 500.0, 0.03, 0.08, 0.25, 0.5, 0.35, 18.7128, 1.0e-3, -0.2906, 0.0036, 103.3856, -46.6982),
            new(OptionType.Put, OptionType.Put, 50.0, 520.0, 500.0, 0.03, 0.08, 0.25, 0.5, 0.35, 15.2601, 1.0e-3, 0.1760, 0.0005, -35.2570, -10.1126),

            // type Mother, typeDaughter, strike Mother, strike Daughter,  spot,    q,    r,    t Mother, t Daughter,  vol,   value,    tol, delta, gamma, vega, theta
            new(OptionType.Call, OptionType.Call, 0.05, 1.14, 1.20, 0.0, 0.01, 0.5, 2.0, 0.11, 0.0729, 1.0e-3, 0.6614, 2.5762, 0.5812, -0.0297),
            new(OptionType.Call, OptionType.Put, 0.05, 1.14, 1.20, 0.0, 0.01, 0.5, 2.0, 0.11, 0.0074, 1.0e-3, -0.1334, 1.9681, 0.2933, -0.0155),
            new(OptionType.Put, OptionType.Call, 0.05, 1.14, 1.20, 0.0, 0.01, 0.5, 2.0, 0.11, 0.0021, 1.0e-3, -0.0426, 0.7252, -0.0052, -0.0058),
            new(OptionType.Put, OptionType.Put, 0.05, 1.14, 1.20, 0.0, 0.01, 0.5, 2.0, 0.11, 0.0192, 1.0e-3, 0.1626, 0.1171, -0.2931, -0.0028),

            // type Mother, typeDaughter, strike Mother, strike Daughter,  spot,    q,    r,    t Mother, t Daughter,  vol,   value,    tol, delta, gamma, vega, theta
            new(OptionType.Call, OptionType.Call, 10.0, 122.0, 120.0, 0.06, 0.02, 0.1, 0.7, 0.22, 0.4419, 1.0e-3, 0.1049, 0.0195, 11.3368, -6.2871),
            new(OptionType.Call, OptionType.Put, 10.0, 122.0, 120.0, 0.06, 0.02, 0.1, 0.7, 0.22, 2.6112, 1.0e-3, -0.3618, 0.0337, 28.4843, -13.4124),
            new(OptionType.Put, OptionType.Call, 10.0, 122.0, 120.0, 0.06, 0.02, 0.1, 0.7, 0.22, 4.1616, 1.0e-3, -0.3174, 0.0024, -26.6403, -2.2720),
            new(OptionType.Put, OptionType.Put, 10.0, 122.0, 120.0, 0.06, 0.02, 0.1, 0.7, 0.22, 1.0914, 1.0e-3, 0.1748, 0.0165, -9.4928, -4.8995),

            // *********************************************************
            // *********************************************************
            // Option Values and Greeks taken from mathfinance VBA implementation
            // type Mother, typeDaughter, strike Mother, strike Daughter,  spot,    q,    r,    t Mother, t Daughter,  vol,   value,    tol, delta, gamma, vega, theta
            new(OptionType.Call, OptionType.Call, 0.4, 8.2, 8.0, 0.05, 0.00, 2.0, 3.0, 0.08, 0.0099, 1.0e-3, 0.0285, 0.0688, 0.7764, -0.0027),
            new(OptionType.Call, OptionType.Put, 0.4, 8.2, 8.0, 0.05, 0.00, 2.0, 3.0, 0.08, 0.9826, 1.0e-3, -0.7224, 0.2158, 2.7279, -0.3332),
            new(OptionType.Put, OptionType.Call, 0.4, 8.2, 8.0, 0.05, 0.00, 2.0, 3.0, 0.08, 0.3585, 1.0e-3, -0.0720, -0.0835, -1.5633, -0.0117),
            new(OptionType.Put, OptionType.Put, 0.4, 8.2, 8.0, 0.05, 0.00, 2.0, 3.0, 0.08, 0.0168, 1.0e-3, 0.0378, 0.0635, 0.3882, 0.0021),

            // type Mother, typeDaughter, strike Mother, strike Daughter,  spot,    q,    r,    t Mother, t Daughter,  vol,   value,    tol, delta, gamma, vega, theta
            new(OptionType.Call, OptionType.Call, 0.02, 1.6, 1.6, 0.013, 0.022, 0.45, 0.5, 0.17, 0.0680, 1.0e-3, 0.4937, 2.1271, 0.4418, -0.0843),
            new(OptionType.Call, OptionType.Put, 0.02, 1.6, 1.6, 0.013, 0.022, 0.45, 0.5, 0.17, 0.0605, 1.0e-3, -0.4169, 2.0836, 0.4330, -0.0697),
            new(OptionType.Put, OptionType.Call, 0.02, 1.6, 1.6, 0.013, 0.022, 0.45, 0.5, 0.17, 0.0081, 1.0e-3, -0.0417, 0.0761, -0.0045, -0.0020),
            new(OptionType.Put, OptionType.Put, 0.02, 1.6, 1.6, 0.013, 0.022, 0.45, 0.5, 0.17, 0.0078, 1.0e-3, 0.0413, 0.0326, -0.0133, -0.0016),
        ];

        DayCounter dc = new Actual360();
        Date todaysDate = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = todaysDate };

        foreach (CompoundOptionData value in values)
        {
            var payoffMother = new PlainVanillaPayoff(value.TypeMother, value.StrikeMother);
            var payoffDaughter = new PlainVanillaPayoff(value.TypeDaughter, value.StrikeDaughter);

            Date matDateMom = todaysDate + TimeToDays(value.TMother);
            Date matDateDaughter = todaysDate + TimeToDays(value.TDaughter);

            var exerciseMother = new EuropeanExercise(matDateMom);
            var exerciseDaughter = new EuropeanExercise(matDateDaughter);

            YieldTermStructure rTS = FlatForward.Create(0, new NullCalendar(), value.R, dc, context);
            YieldTermStructure qTS = FlatForward.Create(0, new NullCalendar(), value.Q, dc, context);
            BlackVolTermStructure volTS = new BlackConstantVol(todaysDate, new NullCalendar(), value.V, dc);

            var compoundOption = new CompoundOption(payoffMother, exerciseMother, payoffDaughter, exerciseDaughter);

            var stochProcess = new BlackScholesMertonProcess(value.S, qTS, rTS, volTS);

            OneAssetOptionResults results = AnalyticCompoundOptionEngine.Calculate(compoundOption, context, stochProcess);

            (string Name, Real Expected, Real Calculated)[] checks =
            [
                ("value", value.Npv, results.NPV!.Value),
                ("delta", value.Delta, results.Delta!.Value),
                ("gamma", value.Gamma, results.Gamma!.Value),
                ("vega", value.Vega, results.Vega!.Value),
                ("theta", value.Theta, results.Theta!.Value),
            ];
            foreach ((string name, Real expected, Real calculated) in checks)
            {
                Real error = Math.Abs(calculated - expected);
                Real tolerance = value.Tol;
                if (error > tolerance)
                {
                    ReportFailure(
                        name, payoffMother, payoffDaughter, exerciseMother, exerciseDaughter, value, todaysDate,
                        expected, calculated, error, tolerance);
                }
            }
        }
    }

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static Integer TimeToDays(double t, Integer daysPerYear = 360) =>
        (Integer)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private static void ReportFailure(
        string greekName,
        PlainVanillaPayoff payoffM,
        PlainVanillaPayoff payoffD,
        Exercise exerciseM,
        Exercise exerciseD,
        CompoundOptionData value,
        Date today,
        Real expected,
        Real calculated,
        Real error,
        Real tolerance) =>
        Assert.Fail(
            $"\nmother option type:   {payoffM.OptionType}\ndaughter option type: {payoffD.OptionType}"
            + $"\nspot value:           {value.S}\nstrike mother:        {payoffM.Strike}"
            + $"\nstrike daughter:      {payoffD.Strike}\ndividend yield:       {value.Q}"
            + $"\nrisk-free rate:       {value.R}\nreference date:       {today}"
            + $"\nmaturity mother:      {exerciseM.LastDate()}\nmaturity daughter:    {exerciseD.LastDate()}"
            + $"\nvolatility:           {value.V}\n  expected {greekName}: {expected}"
            + $"\ncalculated {greekName}: {calculated}\nerror:                {error}\ntolerance:            {tolerance}");

    private readonly record struct CompoundOptionData(
        OptionType TypeMother,
        OptionType TypeDaughter,
        Real StrikeMother,
        Real StrikeDaughter,
        Real S,
        Rate Q,
        Rate R,
        double TMother,
        double TDaughter,
        Volatility V,
        Real Npv = 0.0,
        Real Tol = 0.0,
        Real Delta = 0.0,
        Real Gamma = 0.0,
        Real Vega = 0.0,
        Real Theta = 0.0);
}
