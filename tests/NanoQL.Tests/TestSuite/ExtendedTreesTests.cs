// SPDX-FileCopyrightText: Copyright (C) 2003, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/extendedtrees.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ExtendedTreesTests
{
    // Adapted: QuantLib's cases read the system clock for their reference date; these read QuantLib's runner's date
    // (playbook section 9.2), and the quotes changed in place become a process built for each value.
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, Month.September, 2015) };

    private enum EngineType
    {
        Analytic,
        JR,
        CRR,
        EQP,
        TGEO,
        TIAN,
        LR,
        JOSHI,
    }

    [Fact]
    public void TestJRBinomialEngines()
    {
        // Testing time-dependent JR binomial European engines against analytic results...
        TestEngineConsistency(EngineType.JR, 251, Tolerance(0.002));
    }

    [Fact]
    public void TestCRRBinomialEngines()
    {
        // Testing time-dependent CRR binomial European engines against analytic results...
        TestEngineConsistency(EngineType.CRR, 501, Tolerance(0.02));
    }

    [Fact]
    public void TestEQPBinomialEngines()
    {
        // Testing time-dependent EQP binomial European engines against analytic results...
        TestEngineConsistency(EngineType.EQP, 501, Tolerance(0.02));
    }

    [Fact]
    public void TestTGEOBinomialEngines()
    {
        // Testing time-dependent TGEO binomial European engines against analytic results...
        TestEngineConsistency(EngineType.TGEO, 251, Tolerance(0.002));
    }

    [Fact]
    public void TestTIANBinomialEngines()
    {
        // Testing time-dependent TIAN binomial European engines against analytic results...
        TestEngineConsistency(EngineType.TIAN, 251, Tolerance(0.002));
    }

    [Fact]
    public void TestLRBinomialEngines()
    {
        // Testing time-dependent LR binomial European engines against analytic results...
        TestEngineConsistency(EngineType.LR, 251, Tolerance(1.0e-6));
    }

    [Fact]
    public void TestJOSHIBinomialEngines()
    {
        // Testing time-dependent Joshi binomial European engines against analytic results...
        TestEngineConsistency(EngineType.JOSHI, 251, Tolerance(1.0e-7));
    }

    // Each case's relative tolerances: its value's, and the greeks' common ones.
    private static Dictionary<string, double> Tolerance(double value) => new()
    {
        ["value"] = value,
        ["delta"] = 1.0e-3,
        ["gamma"] = 1.0e-4,
        ["theta"] = 0.03,
    };

    private static void TestEngineConsistency(EngineType engine, int binomialSteps, Dictionary<string, double> tolerance)
    {
        Dictionary<string, double> calculated = [], expected = [];

        // test options
        OptionType[] types = [OptionType.Call, OptionType.Put];
        double[] strikes = [75.0, 100.0, 125.0];
        int[] lengths = [1];

        // test data
        double[] underlyings = [100.0];
        double[] qRates = [0.00, 0.05];
        double[] rRates = [0.01, 0.05, 0.15];
        double[] vols = [0.11, 0.50, 1.20];

        DayCounter dc = new Actual360();
        EvaluationContext context = RunnerContext;
        Date today = context.EvaluationDate;

        foreach (OptionType type in types)
        {
            foreach (double strike in strikes)
            {
                foreach (int length in lengths)
                {
                    Date exDate = today + (length * 360);
                    Exercise exercise = new EuropeanExercise(exDate);
                    StrikedTypePayoff payoff = new PlainVanillaPayoff(type, strike);
                    var option = new EuropeanOption(payoff, exercise);

                    foreach (double u in underlyings)
                    {
                        foreach (double m in qRates)
                        {
                            foreach (double n in rRates)
                            {
                                foreach (double v in vols)
                                {
                                    double q = m, r = n;
                                    GeneralizedBlackScholesProcess process = new BlackScholesMertonProcess(
                                        u,
                                        new FlatForward(today, q, dc),
                                        new FlatForward(today, r, dc),
                                        new BlackConstantVol(today, new NullCalendar(), v, dc));

                                    // reference option
                                    OneAssetOptionResults refResults = Price(EngineType.Analytic, option, context, process, binomialSteps);

                                    // option to check
                                    OneAssetOptionResults results = Price(engine, option, context, process, binomialSteps);

                                    expected.Clear();
                                    calculated.Clear();

                                    expected["value"] = refResults.NPV!.Value;
                                    calculated["value"] = results.NPV!.Value;

                                    if (results.NPV!.Value > u * 1.0e-5)
                                    {
                                        expected["delta"] = refResults.Delta!.Value;
                                        expected["gamma"] = refResults.Gamma!.Value;
                                        expected["theta"] = refResults.Theta!.Value;
                                        calculated["delta"] = results.Delta!.Value;
                                        calculated["gamma"] = results.Gamma!.Value;
                                        calculated["theta"] = results.Theta!.Value;
                                    }

                                    foreach ((string greek, double calcl) in calculated)
                                    {
                                        double expct = expected[greek], tol = tolerance[greek];
                                        double error = RelativeError(expct, calcl, u);
                                        if (error > tol)
                                        {
                                            Assert.Fail(
                                                $"European {type} option: spot {u}, strike {strike}, q {q}, r {r}, "
                                                + $"vol {v}: expected {greek} {expct}, calculated {calcl}, "
                                                + $"error {error}, tolerance {tol}");
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

    // QuantLib's makeOption's engine: the analytic engine, or the binomial engine on an extended tree.
    private static OneAssetOptionResults Price(
        EngineType engineType,
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess stochProcess,
        int binomialSteps) =>
        engineType switch
        {
            EngineType.Analytic => AnalyticEuropeanEngine.Calculate(option, context, stochProcess),
            EngineType.JR => BinomialVanillaEngine.Calculate<ExtendedJarrowRudd>(option, context, stochProcess, binomialSteps),
            EngineType.CRR =>
                BinomialVanillaEngine.Calculate<ExtendedCoxRossRubinstein>(option, context, stochProcess, binomialSteps),
            EngineType.EQP =>
                BinomialVanillaEngine.Calculate<ExtendedAdditiveEQPBinomialTree>(option, context, stochProcess, binomialSteps),
            EngineType.TGEO => BinomialVanillaEngine.Calculate<ExtendedTrigeorgis>(option, context, stochProcess, binomialSteps),
            EngineType.TIAN => BinomialVanillaEngine.Calculate<ExtendedTian>(option, context, stochProcess, binomialSteps),
            EngineType.LR => BinomialVanillaEngine.Calculate<ExtendedLeisenReimer>(option, context, stochProcess, binomialSteps),
            EngineType.JOSHI => BinomialVanillaEngine.Calculate<ExtendedJoshi4>(option, context, stochProcess, binomialSteps),
            _ => throw new InvalidOperationException("unknown engine type"),
        };

    // The test suite's relativeError: the difference relative to the reference, or absolute for a zero reference.
    private static double RelativeError(double x1, double x2, double reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);
}
