// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007, 2008, 2017 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2011 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: Copyright (C) 2014 Bernd Lewerenz
// SPDX-FileCopyrightText: Copyright (C) 2020, 2021 Jack Gillett
// SPDX-FileCopyrightText: Copyright (C) 2021 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/asianoptions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testVecerEngine and testAnalyticContinuousGeometricAveragePriceHeston, which wait for S-xeq's VecerEngine and AnalyticContinuousGeometricAveragePriceAsianHestonEngine
// Moved: testAnalyticDiscreteGeometricAverageStrike line 488 (Divergences/GeometricAverageStrikeForwardStartTests.cs)

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// QuantLib's options hold their engines and its quotes notify them; here each pricing passes the engine's arguments,
// and a quote set anew is a process built at the new value. QuantLib's Monte Carlo builders default the seed to 0, a
// seed from the clock; here a fixed seed (S2R4), which a low-discrepancy generator reads only past Sobol's tabulated
// dimensions, so its sequence is QuantLib's.
[Trait("Oracle", "QuantLib")]
public class AsianOptionsTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, September, 2015) };

    // testAnalyticDiscreteGeometricAverageStrike's moved value: the engine's times from the evaluation date, its first
    // fixing 36 days on (the S-bar plan's BR5 (xxxiii)), QuantLib's 4.97109, which measures them from the first
    // fixing and drops its dividend discount, recorded in Divergences/GeometricAverageStrikeForwardStartTests.cs
    private const double MovedDiscreteGeometricAverageStrike = 4.956201596311162;

    [Fact]
    public void TestAnalyticContinuousGeometricAveragePrice()
    {
        // Testing analytic continuous geometric average-price Asians...

        // data from "Option Pricing Formulas", Haug, pag.96-97

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        GeneralizedBlackScholesProcess stochProcess = Process(today, 80.0, -0.03, 0.05, 0.20, dc);

        AverageType averageType = AverageType.Geometric;
        OptionType type = OptionType.Put;
        double strike = 85.0;
        Date exerciseDate = today + 90;

        var payoff = new PlainVanillaPayoff(type, strike);

        var exercise = new EuropeanExercise(exerciseDate);

        var option = new ContinuousAveragingAsianOption(averageType, payoff, exercise);

        double calculated = AnalyticContinuousGeometricAveragePriceAsianEngine.Calculate(option, RunnerContext, stochProcess).NPV!.Value;
        double expected = 4.6922;
        double tolerance = 1.0e-4;
        Assert.True(Math.Abs(calculated - expected) <= tolerance, $"value: expected {expected}, calculated {calculated}");

        // trying to approximate the continuous version with the discrete version
        double runningAccumulator = 1.0;
        int pastFixings = 0;
        var fixingDates = new Date[exerciseDate - today + 1];
        for (int i = 0; i < fixingDates.Length; i++)
        {
            fixingDates[i] = today + i;
        }

        var option2 = new DiscreteAveragingAsianOption(averageType, runningAccumulator, pastFixings, fixingDates, payoff, exercise);

        calculated = AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(option2, RunnerContext, stochProcess).NPV!.Value;
        tolerance = 3.0e-3;
        Assert.True(Math.Abs(calculated - expected) <= tolerance, $"discrete value: expected {expected}, calculated {calculated}");
    }

    // Adapted: the quotes changed in place become processes built for each value, on curves dated on the evaluation
    // date, so moving the date moves them, as QuantLib's floating curves move.
    [Fact]
    public void TestAnalyticContinuousGeometricAveragePriceGreeks()
    {
        // Testing analytic continuous geometric average-price Asian greeks...

        var tolerance = new Dictionary<string, double>
        {
            ["delta"] = 1.0e-5,
            ["gamma"] = 1.0e-5,
            ["theta"] = 1.0e-5,
            ["rho"] = 1.0e-5,
            ["divRho"] = 1.0e-5,
            ["vega"] = 1.0e-5,
        };

        OptionType[] types = [OptionType.Call, OptionType.Put];
        double[] underlyings = [100.0];
        double[] strikes = [90.0, 100.0, 110.0];
        double[] qRates = [0.04, 0.05, 0.06];
        double[] rRates = [0.01, 0.05, 0.15];
        int[] lengths = [1, 2];
        double[] vols = [0.11, 0.50, 1.20];

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;
        EvaluationContext context = RunnerContext;

        foreach (OptionType type in types)
        {
            foreach (double strike in strikes)
            {
                foreach (int length in lengths)
                {
                    var maturity = new EuropeanExercise(today + new Period(length, TimeUnit.Years));

                    var payoff = new PlainVanillaPayoff(type, strike);

                    var option = new ContinuousAveragingAsianOption(AverageType.Geometric, payoff, maturity);

                    foreach (double u in underlyings)
                    {
                        foreach (double m in qRates)
                        {
                            foreach (double n in rRates)
                            {
                                foreach (double v in vols)
                                {
                                    double q = m, r = n;
                                    OneAssetOptionResults results = Price(option, u, q, r, v, dc, context);
                                    double value = results.NPV!.Value;
                                    var calculated = new Dictionary<string, double>
                                    {
                                        ["delta"] = results.Delta!.Value,
                                        ["gamma"] = results.Gamma!.Value,
                                        ["theta"] = results.Theta!.Value,
                                        ["rho"] = results.Rho!.Value,
                                        ["divRho"] = results.DividendRho!.Value,
                                        ["vega"] = results.Vega!.Value,
                                    };
                                    var expected = new Dictionary<string, double>();

                                    if (value > u * 1.0e-5)
                                    {
                                        // perturb spot and get delta and gamma
                                        double du = u * 1.0e-4;
                                        OneAssetOptionResults p = Price(option, u + du, q, r, v, dc, context);
                                        OneAssetOptionResults mm = Price(option, u - du, q, r, v, dc, context);
                                        expected["delta"] = (p.NPV!.Value - mm.NPV!.Value) / (2 * du);
                                        expected["gamma"] = (p.Delta!.Value - mm.Delta!.Value) / (2 * du);

                                        // perturb rates and get rho and dividend rho
                                        double dr = r * 1.0e-4;
                                        double valueP = Price(option, u, q, r + dr, v, dc, context).NPV!.Value;
                                        double valueM = Price(option, u, q, r - dr, v, dc, context).NPV!.Value;
                                        expected["rho"] = (valueP - valueM) / (2 * dr);

                                        double dq = q * 1.0e-4;
                                        valueP = Price(option, u, q + dq, r, v, dc, context).NPV!.Value;
                                        valueM = Price(option, u, q - dq, r, v, dc, context).NPV!.Value;
                                        expected["divRho"] = (valueP - valueM) / (2 * dq);

                                        // perturb volatility and get vega
                                        double dv = v * 1.0e-4;
                                        valueP = Price(option, u, q, r, v + dv, dc, context).NPV!.Value;
                                        valueM = Price(option, u, q, r, v - dv, dc, context).NPV!.Value;
                                        expected["vega"] = (valueP - valueM) / (2 * dv);

                                        // perturb date and get theta
                                        double dT = dc.YearFraction(today - 1, today + 1);
                                        valueM = Price(option, u, q, r, v, dc, context with { EvaluationDate = today - 1 }).NPV!.Value;
                                        valueP = Price(option, u, q, r, v, dc, context with { EvaluationDate = today + 1 }).NPV!.Value;
                                        expected["theta"] = (valueP - valueM) / dT;

                                        // compare
                                        foreach ((string greek, double calcl) in calculated)
                                        {
                                            double expct = expected[greek], tol = tolerance[greek];
                                            double error = RelativeError(expct, calcl, u);
                                            Assert.True(
                                                error <= tol,
                                                $"{type} {strike} {length}y q {q} r {r} vol {v}: {greek} expected {expct}, calculated {calcl}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        static OneAssetOptionResults Price(
            ContinuousAveragingAsianOption option, double u, double q, double r, double v, DayCounter dc, EvaluationContext context) =>
            AnalyticContinuousGeometricAveragePriceAsianEngine.Calculate(
                option, context, Process(context.EvaluationDate, u, q, r, v, dc));
    }

    [Fact]
    public void TestAnalyticDiscreteGeometricAveragePrice()
    {
        // Testing analytic discrete geometric average-price Asians...

        // data from "Implementing Derivatives Model",
        // Clewlow, Strickland, p.118-123

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        GeneralizedBlackScholesProcess stochProcess = Process(today, 100.0, 0.03, 0.06, 0.20, dc);

        AverageType averageType = AverageType.Geometric;
        double runningAccumulator = 1.0;
        int pastFixings = 0;
        int futureFixings = 10;
        OptionType type = OptionType.Call;
        double strike = 100.0;
        var payoff = new PlainVanillaPayoff(type, strike);

        Date exerciseDate = today + 360;
        var exercise = new EuropeanExercise(exerciseDate);

        Date[] fixingDates = EvenFixings(today, futureFixings);

        var option = new DiscreteAveragingAsianOption(averageType, runningAccumulator, pastFixings, fixingDates, payoff, exercise);

        double calculated = AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(option, RunnerContext, stochProcess).NPV!.Value;
        double expected = 5.3425606635;
        double tolerance = 1e-10;
        Assert.True(Math.Abs(calculated - expected) <= tolerance, $"value: expected {expected}, calculated {calculated}");
    }

    [Fact]
    public void TestAnalyticDiscreteGeometricAverageStrike()
    {
        // Testing analytic discrete geometric average-strike Asians...

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        GeneralizedBlackScholesProcess stochProcess = Process(today, 100.0, 0.03, 0.06, 0.20, dc);

        AverageType averageType = AverageType.Geometric;
        double runningAccumulator = 1.0;
        int pastFixings = 0;
        int futureFixings = 10;
        OptionType type = OptionType.Call;
        double strike = 100.0;
        var payoff = new PlainVanillaPayoff(type, strike);

        Date exerciseDate = today + 360;
        var exercise = new EuropeanExercise(exerciseDate);

        Date[] fixingDates = EvenFixings(today, futureFixings);

        var option = new DiscreteAveragingAsianOption(averageType, runningAccumulator, pastFixings, fixingDates, payoff, exercise);

        double calculated = AnalyticDiscreteGeometricAverageStrikeAsianEngine.Calculate(option, RunnerContext, stochProcess).NPV!.Value;

        // Moved: the check of line 488 against 4.97109, QuantLib's value with its times from the first fixing: the
        // engine's times run from the evaluation date, so the average discounts the dividend to the first fixing,
        // 4.97109 × e^(−0.03 × 36/360) (the S-bar plan's BR5 (xxxiii),
        // Divergences/GeometricAverageStrikeForwardStartTests.cs)
        double expected = MovedDiscreteGeometricAverageStrike;
        double tolerance = 1e-5;
        Assert.True(Math.Abs(calculated - expected) <= tolerance, $"value: expected {expected}, calculated {calculated}");
    }

    [Fact]
    public void TestMCDiscreteGeometricAveragePrice()
    {
        // Testing Monte Carlo discrete geometric average-price Asians...

        // data from "Implementing Derivatives Model",
        // Clewlow, Strickland, p.118-123

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        GeneralizedBlackScholesProcess stochProcess = Process(today, 100.0, 0.03, 0.06, 0.20, dc);

        double tolerance = 4.0e-3;

        Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine =
            new MakeMCDiscreteGeometricAPEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 42)
                .WithSamples(8191)
                .ToEngine();

        AverageType averageType = AverageType.Geometric;
        double runningAccumulator = 1.0;
        int pastFixings = 0;
        int futureFixings = 10;
        OptionType type = OptionType.Call;
        double strike = 100.0;
        var payoff = new PlainVanillaPayoff(type, strike);

        Date exerciseDate = today + 360;
        var exercise = new EuropeanExercise(exerciseDate);

        Date[] fixingDates = EvenFixings(today, futureFixings);

        var option = new DiscreteAveragingAsianOption(averageType, runningAccumulator, pastFixings, fixingDates, payoff, exercise);

        double calculated = engine(option, RunnerContext).NPV!.Value;

        double expected = AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(option, RunnerContext, stochProcess).NPV!.Value;

        Assert.True(Math.Abs(calculated - expected) <= tolerance, $"value: expected {expected}, calculated {calculated}");
    }

    [Fact]
    public void TestMCDiscreteArithmeticAveragePrice()
    {
        // Testing Monte Carlo discrete arithmetic average-price Asians...

        // data from "Asian Option", Levy, 1997
        // in "Exotic Options: The State of the Art",
        // edited by Clewlow, Strickland
        DiscreteAverageData[] cases4 =
        [
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 2, 0.13, true, 1.3942835683),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 4, 0.13, true, 1.5852442983),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 8, 0.13, true, 1.66970673),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 12, 0.13, true, 1.6980019214),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 26, 0.13, true, 1.7255070456),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 52, 0.13, true, 1.7401553533),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 100, 0.13, true, 1.7478303712),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 250, 0.13, true, 1.7490291943),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 500, 0.13, true, 1.7515113291),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 1000, 0.13, true, 1.7537344885),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 2, 0.13, true, 1.8496053697),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 4, 0.13, true, 2.0111495205),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 8, 0.13, true, 2.0852138818),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 12, 0.13, true, 2.1105094397),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 26, 0.13, true, 2.1346526695),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 52, 0.13, true, 2.147489651),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 100, 0.13, true, 2.154728109),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 250, 0.13, true, 2.1564276565),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 500, 0.13, true, 2.1594238588),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 1000, 0.13, true, 2.1595367326),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 2, 0.13, true, 2.63315092584),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 4, 0.13, true, 2.76723962361),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 8, 0.13, true, 2.83124836881),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 12, 0.13, true, 2.84290301412),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 26, 0.13, true, 2.88179560417),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 52, 0.13, true, 2.88447044543),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 100, 0.13, true, 2.89985329603),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 250, 0.13, true, 2.90047296063),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 500, 0.13, true, 2.89813412160),
            new(OptionType.Put, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 1000, 0.13, true, 2.89703362437),
        ];

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        AverageType averageType = AverageType.Arithmetic;
        double runningSum = 0.0;
        int pastFixings = 0;
        foreach (DiscreteAverageData l in cases4)
        {
            var payoff = new PlainVanillaPayoff(l.Type, l.Strike);

            double dt = l.Length / (l.Fixings - 1);
            var timeIncrements = new double[l.Fixings];
            var fixingDates = new Date[l.Fixings];
            timeIncrements[0] = l.First;
            fixingDates[0] = today + TimeToDays(timeIncrements[0]);
            for (int i = 1; i < l.Fixings; i++)
            {
                timeIncrements[i] = (i * dt) + l.First;
                fixingDates[i] = today + TimeToDays(timeIncrements[i]);
            }

            var exercise = new EuropeanExercise(fixingDates[l.Fixings - 1]);

            GeneralizedBlackScholesProcess stochProcess = Process(today, l.Underlying, l.DividendYield, l.RiskFreeRate, l.Volatility, dc);

            Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine =
                new MakeMCDiscreteArithmeticAPEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 42)
                    .WithSamples(2047)
                    .WithControlVariate(l.ControlVariate)
                    .ToEngine();

            var option = new DiscreteAveragingAsianOption(averageType, runningSum, pastFixings, fixingDates, payoff, exercise);

            double calculated = engine(option, RunnerContext).NPV!.Value;
            double expected = l.Result;
            double tolerance = 2.0e-2;
            Assert.True(Math.Abs(calculated - expected) <= tolerance, $"MC, {l.Fixings} fixings: expected {expected}, calculated {calculated}");

            if (l.Fixings < 100)
            {
                calculated = FdBlackScholesAsianEngine.Calculate(option, RunnerContext, stochProcess, 100, 100, 100).NPV!.Value;
                Assert.True(
                    Math.Abs(calculated - expected) <= tolerance,
                    $"The consistency check of the PDE engine failed, {l.Fixings} fixings: expected {expected}, calculated {calculated}");
            }

            calculated = TurnbullWakemanAsianEngine.Calculate(option, RunnerContext, stochProcess).NPV!.Value;
            tolerance = 3.0e-2;
            Assert.True(
                Math.Abs(calculated - expected) <= tolerance,
                $"The consistency check of the analytic approximation engine failed, {l.Fixings} fixings: expected {expected}, calculated {calculated}");

            if (l.Fixings < 100)
            {
                calculated = ChoiAsianEngine.Calculate(option, RunnerContext, stochProcess, 10, 2 << 12).NPV!.Value;
                Assert.True(
                    Math.Abs(calculated - expected) <= tolerance,
                    $"The consistency check of the Choi Asian engine failed, {l.Fixings} fixings: expected {expected}, calculated {calculated}");
            }
        }
    }

    [Fact]
    public void TestMCDiscreteArithmeticAverageStrike()
    {
        // Testing Monte Carlo discrete arithmetic average-strike Asians...

        // data from "Asian Option", Levy, 1997
        // in "Exotic Options: The State of the Art",
        // edited by Clewlow, Strickland
        DiscreteAverageData[] cases5 =
        [
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 2, 0.13, true, 1.51917595129),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 4, 0.13, true, 1.67940165674),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 8, 0.13, true, 1.75371215251),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 12, 0.13, true, 1.77595318693),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 26, 0.13, true, 1.81430536630),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 52, 0.13, true, 1.82269246898),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 100, 0.13, true, 1.83822402464),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 250, 0.13, true, 1.83875059026),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 500, 0.13, true, 1.83750703638),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 0.0, 11.0 / 12.0, 1000, 0.13, true, 1.83887181884),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 2, 0.13, true, 1.51154400089),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 4, 0.13, true, 1.67103508506),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 8, 0.13, true, 1.74529684070),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 12, 0.13, true, 1.76667074564),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 26, 0.13, true, 1.80528400613),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 52, 0.13, true, 1.81400883891),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 100, 0.13, true, 1.82922901451),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 250, 0.13, true, 1.82937111773),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 500, 0.13, true, 1.82826193186),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 1.0 / 12.0, 11.0 / 12.0, 1000, 0.13, true, 1.82967846654),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 2, 0.13, true, 1.49648170891),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 4, 0.13, true, 1.65443100462),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 8, 0.13, true, 1.72817806731),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 12, 0.13, true, 1.74877367895),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 26, 0.13, true, 1.78733801988),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 52, 0.13, true, 1.79624826757),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 100, 0.13, true, 1.81114186876),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 250, 0.13, true, 1.81101152587),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 500, 0.13, true, 1.81002311939),
            new(OptionType.Call, 90.0, 87.0, 0.06, 0.025, 3.0 / 12.0, 11.0 / 12.0, 1000, 0.13, true, 1.81145760308),
        ];

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        AverageType averageType = AverageType.Arithmetic;
        double runningSum = 0.0;
        int pastFixings = 0;
        foreach (DiscreteAverageData l in cases5)
        {
            var payoff = new PlainVanillaPayoff(l.Type, l.Strike);

            double dt = l.Length / (l.Fixings - 1);
            var timeIncrements = new double[l.Fixings];
            var fixingDates = new Date[l.Fixings];
            timeIncrements[0] = l.First;
            fixingDates[0] = today + TimeToDays(timeIncrements[0]);
            for (int i = 1; i < l.Fixings; i++)
            {
                timeIncrements[i] = (i * dt) + l.First;
                fixingDates[i] = today + TimeToDays(timeIncrements[i]);
            }

            var exercise = new EuropeanExercise(fixingDates[l.Fixings - 1]);

            GeneralizedBlackScholesProcess stochProcess = Process(today, l.Underlying, l.DividendYield, l.RiskFreeRate, l.Volatility, dc);

            Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine =
                new MakeMCDiscreteArithmeticASEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 3456789)
                    .WithSamples(1023)
                    .ToEngine();

            var option = new DiscreteAveragingAsianOption(averageType, runningSum, pastFixings, fixingDates, payoff, exercise);

            double calculated = engine(option, RunnerContext).NPV!.Value;
            double expected = l.Result;
            double tolerance = 2.0e-2;
            Assert.True(Math.Abs(calculated - expected) <= tolerance, $"{l.Fixings} fixings: expected {expected}, calculated {calculated}");
        }
    }

    [Fact]
    public void TestMCDiscreteArithmeticAverageStrikeExerciseDate()
    {
        // Testing that MC arithmetic average-strike Asian is sensitive to exercise date...

        // Issue #646: the engine was insensitive to exercise date because
        // timeGrid() did not include it.
        Date today = Date.TodaysDate();
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual360();

        GeneralizedBlackScholesProcess stochProcess = Process(today, 90.0, 0.0, 0.0, 0.20, dc);

        var payoff = new PlainVanillaPayoff(OptionType.Call, 90.0);
        AverageType averageType = AverageType.Arithmetic;
        double runningSum = 0.0;
        int pastFixings = 0;

        // monthly fixings for 6 months
        var fixingDates = new List<Date>();
        for (int i = 0; i <= 6; i++)
        {
            fixingDates.Add(today + new Period(i, TimeUnit.Months));
        }

        Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine =
            new MakeMCDiscreteArithmeticASEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 42)
                .WithSamples(8191)
                .ToEngine();

        // price with exercise at last fixing
        var exercise1 = new EuropeanExercise(fixingDates[^1]);
        var option1 = new DiscreteAveragingAsianOption(averageType, runningSum, pastFixings, fixingDates, payoff, exercise1);
        double price1 = engine(option1, context).NPV!.Value;

        // price with exercise 3 months after last fixing
        var exercise2 = new EuropeanExercise(fixingDates[^1] + new Period(3, TimeUnit.Months));
        var option2 = new DiscreteAveragingAsianOption(averageType, runningSum, pastFixings, fixingDates, payoff, exercise2);
        double price2 = engine(option2, context).NPV!.Value;

        // with r=q=0 and vol>0, a later exercise date must give a higher price
        Assert.True(
            price2 > price1,
            $"average-strike Asian option should be sensitive to exercise date: price with exercise at last fixing = {price1:E16}, price with exercise 3 months later = {price2:E16}");
    }

    // Adapted: the quotes changed in place become processes built for each value, on curves dated on the evaluation
    // date, so moving the date moves them, as QuantLib's floating curves move.
    [Fact]
    public void TestAnalyticDiscreteGeometricAveragePriceGreeks()
    {
        // Testing discrete-averaging geometric Asian greeks...

        var tolerance = new Dictionary<string, double>
        {
            ["delta"] = 1.0e-5,
            ["gamma"] = 1.0e-5,
            ["theta"] = 1.0e-5,
            ["rho"] = 1.0e-5,
            ["divRho"] = 1.0e-5,
            ["vega"] = 1.0e-5,
        };

        OptionType[] types = [OptionType.Call, OptionType.Put];
        double[] underlyings = [100.0];
        double[] strikes = [90.0, 100.0, 110.0];
        double[] qRates = [0.04, 0.05, 0.06];
        double[] rRates = [0.01, 0.05, 0.15];
        int[] lengths = [1, 2];
        double[] vols = [0.11, 0.50, 1.20];

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;
        EvaluationContext context = RunnerContext;

        foreach (OptionType type in types)
        {
            foreach (double strike in strikes)
            {
                foreach (int length in lengths)
                {
                    var maturity = new EuropeanExercise(today + new Period(length, TimeUnit.Years));

                    var payoff = new PlainVanillaPayoff(type, strike);

                    double runningAverage = 120;
                    int pastFixings = 1;

                    var fixingDates = new List<Date>();
                    for (Date d = today + new Period(3, TimeUnit.Months); d <= maturity.LastDate(); d += new Period(3, TimeUnit.Months))
                    {
                        fixingDates.Add(d);
                    }

                    var option = new DiscreteAveragingAsianOption(AverageType.Geometric, runningAverage, pastFixings, fixingDates, payoff, maturity);

                    foreach (double u in underlyings)
                    {
                        foreach (double m in qRates)
                        {
                            foreach (double n in rRates)
                            {
                                foreach (double v in vols)
                                {
                                    double q = m, r = n;
                                    OneAssetOptionResults results = Price(option, u, q, r, v, dc, context);
                                    double value = results.NPV!.Value;
                                    var calculated = new Dictionary<string, double>
                                    {
                                        ["delta"] = results.Delta!.Value,
                                        ["gamma"] = results.Gamma!.Value,
                                        ["theta"] = results.Theta!.Value,
                                        ["rho"] = results.Rho!.Value,
                                        ["divRho"] = results.DividendRho!.Value,
                                        ["vega"] = results.Vega!.Value,
                                    };
                                    var expected = new Dictionary<string, double>();

                                    if (value > u * 1.0e-5)
                                    {
                                        // perturb spot and get delta and gamma
                                        double du = u * 1.0e-4;
                                        OneAssetOptionResults p = Price(option, u + du, q, r, v, dc, context);
                                        OneAssetOptionResults mm = Price(option, u - du, q, r, v, dc, context);
                                        expected["delta"] = (p.NPV!.Value - mm.NPV!.Value) / (2 * du);
                                        expected["gamma"] = (p.Delta!.Value - mm.Delta!.Value) / (2 * du);

                                        // perturb rates and get rho and dividend rho
                                        double dr = r * 1.0e-4;
                                        double valueP = Price(option, u, q, r + dr, v, dc, context).NPV!.Value;
                                        double valueM = Price(option, u, q, r - dr, v, dc, context).NPV!.Value;
                                        expected["rho"] = (valueP - valueM) / (2 * dr);

                                        double dq = q * 1.0e-4;
                                        valueP = Price(option, u, q + dq, r, v, dc, context).NPV!.Value;
                                        valueM = Price(option, u, q - dq, r, v, dc, context).NPV!.Value;
                                        expected["divRho"] = (valueP - valueM) / (2 * dq);

                                        // perturb volatility and get vega
                                        double dv = v * 1.0e-4;
                                        valueP = Price(option, u, q, r, v + dv, dc, context).NPV!.Value;
                                        valueM = Price(option, u, q, r, v - dv, dc, context).NPV!.Value;
                                        expected["vega"] = (valueP - valueM) / (2 * dv);

                                        // perturb date and get theta
                                        double dT = dc.YearFraction(today - 1, today + 1);
                                        valueM = Price(option, u, q, r, v, dc, context with { EvaluationDate = today - 1 }).NPV!.Value;
                                        valueP = Price(option, u, q, r, v, dc, context with { EvaluationDate = today + 1 }).NPV!.Value;
                                        expected["theta"] = (valueP - valueM) / dT;

                                        // compare
                                        foreach ((string greek, double calcl) in calculated)
                                        {
                                            double expct = expected[greek], tol = tolerance[greek];
                                            double error = RelativeError(expct, calcl, u);
                                            Assert.True(
                                                error <= tol,
                                                $"{type} {strike} {length}y q {q} r {r} vol {v}: {greek} expected {expct}, calculated {calcl}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        static OneAssetOptionResults Price(
            DiscreteAveragingAsianOption option, double u, double q, double r, double v, DayCounter dc, EvaluationContext context) =>
            AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(
                option, context, Process(context.EvaluationDate, u, q, r, v, dc));
    }

    [Fact]
    public void TestPastFixings()
    {
        // Testing use of past fixings in Asian options...

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        double spot = 100.0;
        var payoff = new PlainVanillaPayoff(OptionType.Put, 100.0);
        var exercise = new EuropeanExercise(today + new Period(1, TimeUnit.Years));

        GeneralizedBlackScholesProcess stochProcess = Process(today, spot, 0.03, 0.06, 0.20, dc);

        // MC arithmetic average-price
        double runningSum = 0.0;
        int pastFixings = 0;
        var fixingDates1 = new List<Date>();
        for (int i = 0; i <= 12; ++i)
        {
            fixingDates1.Add(today + new Period(i, TimeUnit.Months));
        }

        var option1 = new DiscreteAveragingAsianOption(AverageType.Arithmetic, runningSum, pastFixings, fixingDates1, payoff, exercise);

        pastFixings = 2;
        runningSum = pastFixings * spot * 0.8;
        var fixingDates2 = new List<Date>();
        for (int i = -2; i <= 12; ++i)
        {
            fixingDates2.Add(today + new Period(i, TimeUnit.Months));
        }

        var option2 = new DiscreteAveragingAsianOption(AverageType.Arithmetic, runningSum, pastFixings, fixingDates2, payoff, exercise);

        Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine =
            new MakeMCDiscreteArithmeticAPEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 42)
                .WithSamples(2047)
                .ToEngine();

        double price1 = engine(option1, RunnerContext).NPV!.Value;
        double price2 = engine(option2, RunnerContext).NPV!.Value;

        Assert.False(
            ComparisonFunctions.Close(price1, price2),
            $"past fixings had no effect on arithmetic average-price option\n  without fixings: {price1}\n  with fixings:    {price2}");

        // Test past-fixings-as-a-vector interface
        double[] allPastFixings = [spot * 0.8, spot * 0.8];

        var option1a = new DiscreteAveragingAsianOption(AverageType.Arithmetic, fixingDates1, payoff, exercise);
        var option2a = new DiscreteAveragingAsianOption(AverageType.Arithmetic, fixingDates2, payoff, exercise, allPastFixings);

        double price1a = engine(option1a, RunnerContext).NPV!.Value;
        double price2a = engine(option2a, RunnerContext).NPV!.Value;

        Assert.True(
            Math.Abs(price1 - price1a) <= 1e-8,
            $"Unseasoned option prices do not match in old and new interface\n  Old Interface: {price1}\n  New Interface: {price1a}");
        Assert.True(
            Math.Abs(price2 - price2a) <= 1e-8,
            $"Seasoned option prices do not match in old and new interface\n  Old Interface: {price2}\n  New Interface: {price2a}");

        // MC arithmetic average-strike
        engine = new MakeMCDiscreteArithmeticASEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 42)
            .WithSamples(2047)
            .ToEngine();

        price1 = engine(option1, RunnerContext).NPV!.Value;
        price2 = engine(option2, RunnerContext).NPV!.Value;

        Assert.False(
            ComparisonFunctions.Close(price1, price2),
            $"past fixings had no effect on arithmetic average-strike option\n  without fixings: {price1}\n  with fixings:    {price2}");

        // analytic geometric average-price
        double runningProduct = 1.0;
        pastFixings = 0;
        var option3 = new DiscreteAveragingAsianOption(AverageType.Geometric, runningProduct, pastFixings, fixingDates1, payoff, exercise);

        pastFixings = 2;
        runningProduct = spot * spot;
        var option4 = new DiscreteAveragingAsianOption(AverageType.Geometric, runningProduct, pastFixings, fixingDates2, payoff, exercise);

        double price3 = AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(option3, RunnerContext, stochProcess).NPV!.Value;
        double price4 = AnalyticDiscreteGeometricAveragePriceAsianEngine.Calculate(option4, RunnerContext, stochProcess).NPV!.Value;

        Assert.False(
            ComparisonFunctions.Close(price3, price4),
            $"past fixings had no effect on geometric average-price option\n  without fixings: {price3}\n  with fixings:    {price4}");

        // MC geometric average-price
        engine = new MakeMCDiscreteGeometricAPEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 42)
            .WithSamples(2047)
            .ToEngine();

        price3 = engine(option3, RunnerContext).NPV!.Value;
        price4 = engine(option4, RunnerContext).NPV!.Value;

        Assert.False(
            ComparisonFunctions.Close(price3, price4),
            $"past fixings had no effect on geometric average-price option\n  without fixings: {price3}\n  with fixings:    {price4}");
    }

    [Fact]
    public void TestPastFixingsModelDependency()
    {
        // Testing use of past fixings in Asian options where model dependency is flagged...

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        double spot = 100.0;
        var qTS = new FlatForward(today, 0.03, dc);
        var rTS = new FlatForward(today, 0.06, dc);
        var volTS = new BlackConstantVol(today, new NullCalendar(), 0.20, dc);

        var callPayoff = new PlainVanillaPayoff(OptionType.Call, 20.0);
        var putPayoff = new PlainVanillaPayoff(OptionType.Put, 20.0);

        Date[] fixingDates =
        [
            today - new Period(6, TimeUnit.Weeks),
            today - new Period(2, TimeUnit.Weeks),
            today + new Period(2, TimeUnit.Weeks),
            today + new Period(6, TimeUnit.Weeks),
        ];
        var exercise = new EuropeanExercise(today + new Period(6, TimeUnit.Weeks));

        var stochProcess = new BlackScholesMertonProcess(spot, qTS, rTS, volTS);

        // Test guaranteed exercise (calls) and permanent OTMness (puts), with the average price TW
        // engine
        double[] allPastFixings = [spot, spot];
        var callOption = new DiscreteAveragingAsianOption(AverageType.Arithmetic, fixingDates, callPayoff, exercise, allPastFixings);
        var putOption = new DiscreteAveragingAsianOption(AverageType.Arithmetic, fixingDates, putPayoff, exercise, allPastFixings);

        // The expected call NPV is equal to that of an averaging forward over the same fixing dates,
        // since exercise is guaranteed
        double expectedCallOptionNpv =
            rTS.Discount(exercise.LastDate())
            * (((100.0 + 100.0 + (100.0 * qTS.Discount(fixingDates[2]) / rTS.Discount(fixingDates[2]))
                + (100.0 * qTS.Discount(fixingDates[3]) / rTS.Discount(fixingDates[3]))) / fixingDates.Length)
               - callPayoff.Strike);

        OneAssetOptionResults call = TurnbullWakemanAsianEngine.Calculate(callOption, RunnerContext, stochProcess);
        OneAssetOptionResults put = TurnbullWakemanAsianEngine.Calculate(putOption, RunnerContext, stochProcess);
        Assert.Equal(expectedCallOptionNpv, call.NPV!.Value);
        Assert.Equal(0.0, put.NPV!.Value);

        // Compare greeks to numerical greeks
        double dS = 0.001;
        double callPrice = call.NPV!.Value;
        double putPrice = put.NPV!.Value;
        double callDelta = call.Delta!.Value;
        double callGamma = call.Gamma!.Value;
        double putDelta = put.Delta!.Value;
        double putGamma = put.Gamma!.Value;

        var stochProcessUp = new BlackScholesMertonProcess(100.0 + dS, qTS, rTS, volTS);
        var stochProcessDown = new BlackScholesMertonProcess(100.0 - dS, qTS, rTS, volTS);

        double callCalculatedUp = TurnbullWakemanAsianEngine.Calculate(callOption, RunnerContext, stochProcessUp).NPV!.Value;
        double putCalculatedUp = TurnbullWakemanAsianEngine.Calculate(putOption, RunnerContext, stochProcessUp).NPV!.Value;
        double callCalculatedDown = TurnbullWakemanAsianEngine.Calculate(callOption, RunnerContext, stochProcessDown).NPV!.Value;
        double putCalculatedDown = TurnbullWakemanAsianEngine.Calculate(putOption, RunnerContext, stochProcessDown).NPV!.Value;

        double callDeltaBump = (callCalculatedUp - callCalculatedDown) / (2 * dS);
        double callGammaBump = (callCalculatedUp + callCalculatedDown - (2 * callPrice)) / (dS * dS);
        double putDeltaBump = (putCalculatedUp - putCalculatedDown) / (2 * dS);
        double putGammaBump = (putCalculatedUp + putCalculatedDown - (2 * putPrice)) / (dS * dS);

        double tolerance = 1.0e-8;
        Assert.True(
            Math.Abs(callDeltaBump - callDelta) <= tolerance,
            $"Seasoned analytic call delta did not match numerical delta: analytic {callDelta}, bump {callDeltaBump}");
        Assert.True(
            Math.Abs(callGammaBump - callGamma) <= tolerance,
            $"Seasoned analytic call gamma did not match numerical gamma: analytic {callGamma}, bump {callGammaBump}");
        Assert.True(
            Math.Abs(putDeltaBump - putDelta) <= tolerance,
            $"Seasoned analytic put delta did not match numerical delta: analytic {putDelta}, bump {putDeltaBump}");
        Assert.True(
            Math.Abs(putGammaBump - putGamma) <= tolerance,
            $"Seasoned analytic put gamma did not match numerical gamma: analytic {putGamma}, bump {putGammaBump}");
    }

    // Adapted: QuantLib catches detail::PastFixingsOnly, internal to nanoql; the refusal is a NanoQLException with its
    // text (the S-bar plan's SD14). The curves float with the evaluation date, as QuantLib's do.
    [Fact]
    public void TestAllFixingsInThePast()
    {
        // Testing Asian options with all fixing dates in the past...

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        double spot = 100.0;

        Date exerciseDate = today + new Period(2, TimeUnit.Weeks);
        Date startDate = exerciseDate - new Period(1, TimeUnit.Years);
        var fixingDates = new List<Date>(12);
        for (int i = 0; i < 12; ++i)
        {
            fixingDates.Add(startDate + new Period(i, TimeUnit.Months));
        }

        int pastFixings = 12;

        var payoff = new PlainVanillaPayoff(OptionType.Put, 100.0);
        var exercise = new EuropeanExercise(exerciseDate);

        // MC arithmetic average-price
        double runningSum = pastFixings * spot;
        var option1 = new DiscreteAveragingAsianOption(AverageType.Arithmetic, runningSum, pastFixings, fixingDates, payoff, exercise);

        // MC arithmetic average-strike
        var option2 = new DiscreteAveragingAsianOption(AverageType.Arithmetic, runningSum, pastFixings, fixingDates, payoff, exercise);

        // MC geometric average-price
        double runningProduct = Math.Pow(spot, pastFixings);
        var option3 = new DiscreteAveragingAsianOption(AverageType.Geometric, runningProduct, pastFixings, fixingDates, payoff, exercise);

        var option4 = new DiscreteAveragingAsianOption(AverageType.Arithmetic, runningSum, pastFixings, fixingDates, payoff, exercise);

        // Check that NPV raises a specific exception instead of crashing.
        // (It used to do that.)
        CheckRefusals(RunnerContext);

        // also check with the evaluation date on last fixing
        CheckRefusals(RunnerContext with { EvaluationDate = fixingDates[^1] });

        void CheckRefusals(EvaluationContext context)
        {
            GeneralizedBlackScholesProcess stochProcess = Process(context.EvaluationDate, spot, 0.005, 0.01, 0.20, dc);
            Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults>[] engines =
            [
                new MakeMCDiscreteArithmeticAPEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 42).WithSamples(2047).ToEngine(),
                new MakeMCDiscreteArithmeticASEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 42).WithSamples(2047).ToEngine(),
                new MakeMCDiscreteGeometricAPEngine<LowDiscrepancy, GeneralStatistics>(stochProcess, 42).WithSamples(2047).ToEngine(),
            ];
            DiscreteAveragingAsianOption[] options = [option1, option2, option3];
            for (int i = 0; i < engines.Length; ++i)
            {
                NanoQLException e = Assert.Throws<NanoQLException>(() => engines[i](options[i], context));
                Assert.Equal("all fixings are in the past", e.Message);
            }

            Assert.Throws<NanoQLException>(() => ChoiAsianEngine.Calculate(option4, context, stochProcess));
        }
    }

    [Fact]
    public void TestMCDiscreteGeometricAveragePriceHeston()
    {
        // Testing MC discrete geometric average-price Asians under Heston...

        // 30-day options need wider tolerance due to uncertainty around what "weekly
        // fixing" dates mean over a 30-day month!
        double[] tol =
        [
            4.0e-2, 2.0e-2, 2.0e-2, 4.0e-2, 8.0e-2, 2.0e-1,
            1.0e-1, 4.0e-2, 3.0e-2, 2.0e-2, 9.0e-2, 2.0e-1,
            2.0e-2, 1.0e-2, 2.0e-2, 2.0e-2, 7.0e-2, 2.0e-1,
        ];

        DayCounter dc = new Actual365Fixed();
        Date today = RunnerContext.EvaluationDate;

        HestonProcess hestonProcess = KimHestonProcess(today, dc);

        Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine =
            new MakeMCDiscreteGeometricAPHestonEngine<LowDiscrepancy, GeneralStatistics>(hestonProcess, 43)
                .WithSamples(8191)
                .ToEngine();

        TestDiscreteGeometricAveragePriceHeston(engine, tol);
    }

    // QuantLib's testDiscreteGeometricAveragePriceHeston: Kim, Kim, Kim and Wee's weekly-fixed calls under Heston
    private static void TestDiscreteGeometricAveragePriceHeston(
        Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine, double[] tol)
    {
        // data from "A Recursive Method for Discretely Monitored Geometric Asian Option
        // Prices", Kim, Kim, Kim & Wee, Bull. Korean Math. Soc. 53, 733-749, 2016
        int[] days =
        [
            30, 91, 182, 365, 730, 1095,
            30, 91, 182, 365, 730, 1095,
            30, 91, 182, 365, 730, 1095,
        ];
        double[] strikes =
        [
            90, 90, 90, 90, 90, 90,
            100, 100, 100, 100, 100, 100,
            110, 110, 110, 110, 110, 110,
        ];

        // Prices from Tables 1, 2 and 3
        double[] prices =
        [
            10.2732, 10.9554, 11.9916, 13.6950, 16.1773, 18.0146,
            2.4389, 3.7881, 5.2132, 7.2243, 9.9948, 12.0639,
            0.1012, 0.5949, 1.4444, 2.9479, 5.3531, 7.3315,
        ];

        Date today = RunnerContext.EvaluationDate;

        OptionType type = OptionType.Call;
        AverageType averageType = AverageType.Geometric;

        double runningAccumulator = 1.0;
        int pastFixings = 0;

        for (int i = 0; i < strikes.Length; i++)
        {
            double strike = strikes[i];
            int day = days[i];
            double expected = prices[i];
            double tolerance = tol[i];

            int futureFixings = (int)Math.Floor(day / 7.0);
            var fixingDates = new Date[futureFixings];

            Date expiryDate = today + new Period(day, TimeUnit.Days);

            // I suppose "weekly fixings" roughly means this?
            for (int j = futureFixings - 1; j >= 0; j--)
            {
                fixingDates[j] = expiryDate - (j * 7);
            }

            var europeanExercise = new EuropeanExercise(expiryDate);
            var payoff = new PlainVanillaPayoff(type, strike);

            var option = new DiscreteAveragingAsianOption(averageType, runningAccumulator, pastFixings, fixingDates, payoff, europeanExercise);

            double calculated = engine(option, RunnerContext).NPV!.Value;

            Assert.True(
                Math.Abs(calculated - expected) <= tolerance,
                $"{day} days, strike {strike}: expected {expected}, calculated {calculated}, tolerance {tolerance}");
        }
    }

    // the Heston process of Kim et al.'s tables: spot 100, r 5%, q 0, v0 0.09, kappa 1.15, theta 0.0348, sigma 0.39,
    // rho -0.64
    private static HestonProcess KimHestonProcess(Date today, DayCounter dc) =>
        new(new FlatForward(today, 0.05, dc), new FlatForward(today, 0.0, dc), 100.0, 0.09, 1.15, 0.0348, 0.39, -0.64);

    [Fact]
    public void TestTurnbullWakemanAsianEngine()
    {
        // Testing Turnbull-Wakeman engine for discrete-time arithmetic average-rate Asians options with term structure support...

        // Data from Haug, "Option Pricing Formulas", Table 4-28, p.201
        // Type, underlying, strike, b, rfRate, t1, expiry, fixings, base vol, slope, expected result
        DiscreteAverageDataTermStructure[] cases =
        [
            new(OptionType.Call, 100, 80, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "flat", 19.5152),
            new(OptionType.Call, 100, 80, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "up", 19.5063),
            new(OptionType.Call, 100, 80, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "down", 19.5885),
            new(OptionType.Put, 100, 80, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "flat", 0.0090),
            new(OptionType.Put, 100, 80, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "up", 0.0001),
            new(OptionType.Put, 100, 80, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "down", 0.0823),
            new(OptionType.Call, 100, 90, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "flat", 10.1437),
            new(OptionType.Call, 100, 90, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "up", 9.8313),
            new(OptionType.Call, 100, 90, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "down", 10.7062),
            new(OptionType.Put, 100, 90, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "flat", 0.3906),
            new(OptionType.Put, 100, 90, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "up", 0.0782),
            new(OptionType.Put, 100, 90, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "down", 0.9531),
            new(OptionType.Call, 100, 100, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "flat", 3.2700),
            new(OptionType.Call, 100, 100, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "up", 2.2819),
            new(OptionType.Call, 100, 100, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "down", 4.3370),
            new(OptionType.Put, 100, 100, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "flat", 3.2700),
            new(OptionType.Put, 100, 100, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "up", 2.2819),
            new(OptionType.Put, 100, 100, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "down", 4.3370),
            new(OptionType.Call, 100, 110, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "flat", 0.5515),
            new(OptionType.Call, 100, 110, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "up", 0.1314),
            new(OptionType.Call, 100, 110, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "down", 1.2429),
            new(OptionType.Put, 100, 110, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "flat", 10.3046),
            new(OptionType.Put, 100, 110, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "up", 9.8845),
            new(OptionType.Put, 100, 110, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "down", 10.9960),
            new(OptionType.Call, 100, 120, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "flat", 0.0479),
            new(OptionType.Call, 100, 120, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "up", 0.0016),
            new(OptionType.Call, 100, 120, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "down", 0.2547),
            new(OptionType.Put, 100, 120, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "flat", 19.5541),
            new(OptionType.Put, 100, 120, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "up", 19.5078),
            new(OptionType.Put, 100, 120, 0, 0.05, 1.0 / 52, 0.5, 26, 0.2, "down", 19.7609),
        ];

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        foreach (DiscreteAverageDataTermStructure l in cases)
        {
            double dt = (l.Expiry - l.First) / (l.Fixings - 1);
            var fixingDates = new Date[l.Fixings];
            fixingDates[0] = today + TimeToDays(l.First, 360);

            for (int i = 1; i < l.Fixings; i++)
            {
                fixingDates[i] = today + TimeToDays((i * dt) + l.First, 360);
            }

            // Set up market data
            var qTS = new FlatForward(today, l.B + l.RiskFreeRate, dc);
            var rTS = new FlatForward(today, l.RiskFreeRate, dc);
            BlackVolTermStructure volTS;
            double volSlope = 0.005;
            if (l.Slope == "flat")
            {
                volTS = new BlackConstantVol(today, new NullCalendar(), l.Volatility, dc);
            }
            else if (l.Slope == "up")
            {
                var volatilities = new double[l.Fixings];
                for (int i = 0; i < l.Fixings; ++i)
                {
                    // Loop to fill a vector of vols from 7.5 % to 20 %
                    volatilities[i] = l.Volatility - ((l.Fixings - 1) * volSlope) + (i * volSlope);
                }

                volTS = new BlackVarianceCurve(today, fixingDates, volatilities, dc, true);
            }
            else if (l.Slope == "down")
            {
                var volatilities = new double[l.Fixings];
                for (int i = 0; i < l.Fixings; ++i)
                {
                    // Loop to fill a vector of vols from 32.5 % to 20 %
                    volatilities[i] = l.Volatility + ((l.Fixings - 1) * volSlope) - (i * volSlope);
                }

                volTS = new BlackVarianceCurve(today, fixingDates, volatilities, dc, false);
            }
            else
            {
                throw new NanoQLException("unexpected slope type in engine test case");
            }

            AverageType averageType = AverageType.Arithmetic;
            var payoff = new PlainVanillaPayoff(l.Type, l.Strike);

            Date maturity = today + TimeToDays(l.Expiry, 360);
            var exercise = new EuropeanExercise(maturity);

            var stochProcess = new BlackScholesMertonProcess(l.Underlying, qTS, rTS, volTS);

            var option = new DiscreteAveragingAsianOption(averageType, 0, 0, fixingDates, payoff, exercise);
            OneAssetOptionResults results = TurnbullWakemanAsianEngine.Calculate(option, RunnerContext, stochProcess);

            double calculated = results.NPV!.Value;
            double expected = l.Result;
            double tolerance = 2.5e-3;
            double error = Math.Abs(expected - calculated);
            Assert.True(
                error <= tolerance,
                $"Failed to reproduce expected NPV: {l.Type} strike {l.Strike} slope {l.Slope}: expected {expected}, calculated {calculated}");

            // Compare greeks to numerical greeks
            double dS = 0.001;
            double delta = results.Delta!.Value;
            double gamma = results.Gamma!.Value;

            var stochProcessUp = new BlackScholesMertonProcess(l.Underlying + dS, qTS, rTS, volTS);
            var stochProcessDown = new BlackScholesMertonProcess(l.Underlying - dS, qTS, rTS, volTS);

            double calculatedUp = TurnbullWakemanAsianEngine.Calculate(option, RunnerContext, stochProcessUp).NPV!.Value;
            double calculatedDown = TurnbullWakemanAsianEngine.Calculate(option, RunnerContext, stochProcessDown).NPV!.Value;
            double deltaBump = (calculatedUp - calculatedDown) / (2 * dS);
            double gammaBump = (calculatedUp + calculatedDown - (2 * calculated)) / (dS * dS);

            tolerance = 1.0e-6;
            double deltaError = Math.Abs(deltaBump - delta);
            Assert.True(
                deltaError <= tolerance,
                $"Analytical delta failed to match bump delta: {l.Type} strike {l.Strike} slope {l.Slope}: analytic {delta}, bump {deltaBump}");

            double gammaError = Math.Abs(gammaBump - gamma);
            Assert.True(
                gammaError <= tolerance,
                $"Analytical gamma failed to match bump gamma: {l.Type} strike {l.Strike} slope {l.Slope}: analytic {gamma}, bump {gammaBump}");
        }
    }

    [Fact]
    public void TestLevyEngine()
    {
        // Testing Levy engine for Asians options...

        // data from Haug, "Option Pricing Formulas", p.99-100
        ContinuousAverageData[] cases =
        [
            new(OptionType.Call, 6.80, 6.80, 6.90, 0.09, 0.07, 0.14, 180, 0, 0.0944),
            new(OptionType.Put, 6.80, 6.80, 6.90, 0.09, 0.07, 0.14, 180, 0, 0.2237),
            new(OptionType.Call, 100.0, 100.0, 95.0, 0.05, 0.1, 0.15, 270, 0, 7.0544),
            new(OptionType.Call, 100.0, 100.0, 95.0, 0.05, 0.1, 0.15, 270, 90, 5.6731),
            new(OptionType.Call, 100.0, 100.0, 95.0, 0.05, 0.1, 0.15, 270, 180, 5.0806),
            new(OptionType.Call, 100.0, 100.0, 95.0, 0.05, 0.1, 0.35, 270, 0, 10.1213),
            new(OptionType.Call, 100.0, 100.0, 95.0, 0.05, 0.1, 0.35, 270, 90, 6.9705),
            new(OptionType.Call, 100.0, 100.0, 95.0, 0.05, 0.1, 0.35, 270, 180, 5.1411),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.05, 0.1, 0.15, 270, 0, 3.7845),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.05, 0.1, 0.15, 270, 90, 1.9964),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.05, 0.1, 0.15, 270, 180, 0.6722),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.05, 0.1, 0.35, 270, 0, 7.5038),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.05, 0.1, 0.35, 270, 90, 4.0687),
            new(OptionType.Call, 100.0, 100.0, 100.0, 0.05, 0.1, 0.35, 270, 180, 1.4222),
            new(OptionType.Call, 100.0, 100.0, 105.0, 0.05, 0.1, 0.15, 270, 0, 1.6729),
            new(OptionType.Call, 100.0, 100.0, 105.0, 0.05, 0.1, 0.15, 270, 90, 0.3565),
            new(OptionType.Call, 100.0, 100.0, 105.0, 0.05, 0.1, 0.15, 270, 180, 0.0004),
            new(OptionType.Call, 100.0, 100.0, 105.0, 0.05, 0.1, 0.35, 270, 0, 5.4071),
            new(OptionType.Call, 100.0, 100.0, 105.0, 0.05, 0.1, 0.35, 270, 90, 2.1359),
            new(OptionType.Call, 100.0, 100.0, 105.0, 0.05, 0.1, 0.35, 270, 180, 0.1552),
        ];

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        foreach (ContinuousAverageData l in cases)
        {
            GeneralizedBlackScholesProcess stochProcess = Process(today, l.Spot, l.DividendYield, l.RiskFreeRate, l.Volatility, dc);

            AverageType averageType = AverageType.Arithmetic;
            var payoff = new PlainVanillaPayoff(l.Type, l.Strike);

            Date startDate = today - l.Elapsed;
            Date maturity = startDate + l.Length;

            var exercise = new EuropeanExercise(maturity);

            var option = new ContinuousAveragingAsianOption(averageType, startDate, payoff, exercise);

            double calculated = ContinuousArithmeticAsianLevyEngine.Calculate(option, RunnerContext, stochProcess, l.CurrentAverage).NPV!.Value;
            double expected = l.Result;
            double tolerance = 1.0e-4;
            double error = Math.Abs(expected - calculated);
            Assert.True(
                error <= tolerance,
                $"Asian option with Levy engine: spot {l.Spot}, strike {l.Strike}, vol {l.Volatility}, elapsed {l.Elapsed}: expected {expected}, calculated {calculated}");
        }
    }

    [Fact]
    public void TestChoiAsianEngineVsMC()
    {
        // Testing ChoiAsianEngine against Monte-Carlo pricer...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(5, January, 2025);
        Date maturity = today + new Period(13, TimeUnit.Months);

        var fixingDates = new List<Date> { today + new Period(1, TimeUnit.Months) };
        while (fixingDates[^1] < maturity - new Period(1, TimeUnit.Months))
        {
            fixingDates.Add(fixingDates[^1] + new Period(1, TimeUnit.Months));
        }

        const int pastFixingsCount = 2;
        const double runningAccumulator = pastFixingsCount * 97.0;

        var payoff = new PlainVanillaPayoff(OptionType.Call, 110);
        var exercise = new EuropeanExercise(maturity);

        var option = new DiscreteAveragingAsianOption(
            AverageType.Arithmetic, runningAccumulator, pastFixingsCount, fixingDates, payoff, exercise);

        // market data is constant in time
        GeneralizedBlackScholesProcess process = Process(today, 100, 0.035, 0.1, 0.5, dc);

        double expected = new MakeMCDiscreteArithmeticAPEngine<LowDiscrepancy, GeneralStatistics>(process, 43)
            .WithSamples(32000)
            .ToEngine()(option, RunnerContext).NPV!.Value;

        double calculated = ChoiAsianEngine.Calculate(option, RunnerContext, process, 20, 2 << 12).NPV!.Value;
        double diff = Math.Abs(calculated - expected);
        double tol = 0.01;
        Assert.True(diff <= tol, $"constant market: expected {expected}, calculated {calculated}");

        // market data is not constant in time
        process = new BlackScholesMertonProcess(
            100,
            new ZeroCurve(
                [today, today + new Period(3, TimeUnit.Months), today + new Period(13, TimeUnit.Months)],
                [0.1, 0.0, 0.15],
                dc),
            new ZeroCurve(
                [today, today + new Period(3, TimeUnit.Months), today + new Period(13, TimeUnit.Months)],
                [0.1, 0.2, 0.05],
                dc),
            new BlackVarianceCurve(
                today,
                [today + new Period(1, TimeUnit.Days), today + new Period(100, TimeUnit.Days), today + new Period(13, TimeUnit.Months)],
                [0.25, 0.5, 0.4],
                dc));

        expected = new MakeMCDiscreteArithmeticAPEngine<LowDiscrepancy, GeneralStatistics>(process, 43)
            .WithSamples(32000)
            .ToEngine()(option, RunnerContext).NPV!.Value;

        calculated = ChoiAsianEngine.Calculate(option, RunnerContext, process, 20, 2 << 12).NPV!.Value;
        diff = Math.Abs(calculated - expected);
        tol = 0.01;
        Assert.True(diff <= tol, $"term structures: expected {expected}, calculated {calculated}");
    }

    [Fact]
    public void TestChoiAsianEngineSpecialCases()
    {
        // Testing special cases for ChoiAsianEngine...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(5, January, 2025);
        Date maturity = today + new Period(1, TimeUnit.Years);

        const int pastFixingsCount = 2;
        const double runningAccumulator = pastFixingsCount * 97.0;

        var fixingDates = new List<Date> { today, today + new Period(3, TimeUnit.Weeks) };

        var rTS = new FlatForward(today, 0.2, dc);
        var qTS = new FlatForward(today, 0.075, dc);
        var vTS = new BlackConstantVol(today, new NullCalendar(), 0.5, dc);

        var process = new BlackScholesMertonProcess(100, qTS, rTS, vTS);

        var payoff = new PlainVanillaPayoff(OptionType.Put, 103);
        var exercise = new EuropeanExercise(maturity);

        var asianOption = new DiscreteAveragingAsianOption(
            AverageType.Arithmetic, runningAccumulator, pastFixingsCount, fixingDates, payoff, exercise);

        double calculated = ChoiAsianEngine.Calculate(asianOption, RunnerContext, process).NPV!.Value;
        double expected = BlackFormulaFunctions.BlackFormula(
            payoff.OptionType,
            payoff.Strike - ((runningAccumulator + process.X0) / (pastFixingsCount + 2)),
            100.0 / (pastFixingsCount + 2) * qTS.Discount(fixingDates[^1]) / rTS.Discount(fixingDates[^1]),
            Math.Sqrt(vTS.BlackVariance(fixingDates[^1], payoff.Strike)),
            rTS.Discount(maturity));

        double diff = Math.Abs(calculated - expected);
        const double tol = 1000 * QL_EPSILON;
        Assert.True(diff <= tol, $"two future fixings, one today: expected {expected}, calculated {calculated}");

        fixingDates = [today];
        asianOption = new DiscreteAveragingAsianOption(
            AverageType.Arithmetic, runningAccumulator, pastFixingsCount, fixingDates, payoff, exercise);

        calculated = ChoiAsianEngine.Calculate(asianOption, RunnerContext, process).NPV!.Value;
        expected = rTS.Discount(maturity) * payoff.Evaluate((runningAccumulator + process.X0) / (pastFixingsCount + 1));
        diff = Math.Abs(calculated - expected);
        Assert.True(diff <= tol, $"one fixing today: expected {expected}, calculated {calculated}");

        fixingDates.Clear();
        asianOption = new DiscreteAveragingAsianOption(
            AverageType.Arithmetic, runningAccumulator, pastFixingsCount, fixingDates, payoff, exercise);

        calculated = ChoiAsianEngine.Calculate(asianOption, RunnerContext, process).NPV!.Value;
        expected = rTS.Discount(maturity) * payoff.Evaluate(runningAccumulator / pastFixingsCount);
        diff = Math.Abs(calculated - expected);
        Assert.True(diff <= tol, $"no fixing to come: expected {expected}, calculated {calculated}");
    }

    [Fact]
    public void TestContinuousSeasonedAsianOptions()
    {
        // Testing seasoned continuous averaging Asian options...

        DayCounter dc = new Actual365Fixed();
        var today = new Date(15, November, 2025);
        var settlementDate = new Date(17, November, 2025);
        var context = new EvaluationContext { EvaluationDate = today };

        // Market data
        double spot = 100.0;
        double dividendYield = 0.03;
        double riskFreeRate = 0.06;
        double volatility = 0.20;
        var maturity = new Date(17, November, 2026);
        var startDate = new Date(17, August, 2025); // Started 3 months ago

        var bsmProcess = new BlackScholesMertonProcess(
            spot,
            new FlatForward(settlementDate, dividendYield, dc),
            new FlatForward(settlementDate, riskFreeRate, dc),
            new BlackConstantVol(settlementDate, new TARGET(), volatility, dc));

        double strike = 100.0;
        var payoff = new PlainVanillaPayoff(OptionType.Put, strike);
        var europeanExercise = new EuropeanExercise(maturity);

        // Test 1: Fresh (unseasoned) continuous arithmetic option
        var freshOption = new ContinuousAveragingAsianOption(AverageType.Arithmetic, settlementDate, payoff, europeanExercise);
        double freshNPV = ContinuousArithmeticAsianLevyEngine.Calculate(freshOption, context, bsmProcess, 0.0).NPV!.Value;

        // Test 2: Seasoned continuous arithmetic option
        // startDate goes in the option (contract term), currentAverage goes in the engine (market data)
        double currentAverage = 98.5; // Running average below spot
        var seasonedOption = new ContinuousAveragingAsianOption(AverageType.Arithmetic, startDate, payoff, europeanExercise);
        double seasonedNPV = ContinuousArithmeticAsianLevyEngine.Calculate(seasonedOption, context, bsmProcess, currentAverage).NPV!.Value;

        // Seasoned option should be worth less than fresh option for a put
        // when current average is below strike
        Assert.True(
            seasonedNPV < freshNPV,
            $"Seasoned Asian put option NPV ({seasonedNPV}) should be less than fresh option NPV ({freshNPV}) when current average ({currentAverage}) is below strike ({strike})");

        // Test 3: Seasoned option with high current average
        double highAverage = 102.0; // Running average above strike
        var seasonedHighOption = new ContinuousAveragingAsianOption(AverageType.Arithmetic, startDate, payoff, europeanExercise);
        double seasonedHighNPV = ContinuousArithmeticAsianLevyEngine.Calculate(seasonedHighOption, context, bsmProcess, highAverage).NPV!.Value;

        // Seasoned option with higher average should be worth even less
        Assert.True(
            seasonedHighNPV < seasonedNPV,
            $"Seasoned Asian put with higher average ({highAverage}) should have lower NPV ({seasonedHighNPV}) than seasoned option with lower average ({currentAverage}, NPV={seasonedNPV})");

        // Test 4: Continuous geometric should fail for seasoned options (not yet implemented)
        var seasonedGeometric = new ContinuousAveragingAsianOption(AverageType.Geometric, startDate, payoff, europeanExercise);
        Assert.Throws<NanoQLException>(() => AnalyticContinuousGeometricAveragePriceAsianEngine.Calculate(seasonedGeometric, context, bsmProcess));
    }

    [Fact]
    public void TestAnalyticDiscreteGeometricAveragePriceHeston()
    {
        // Testing analytic discrete geometric average-price Asians under Heston...

        // 30-day options need wider tolerance due to uncertainty around what "weekly
        // fixing" dates mean over a 30-day month!
        double[] tol =
        [
            3.0e-2, 2.0e-2, 2.0e-2, 2.0e-2, 3.0e-2, 4.0e-2, 8.0e-2, 1.0e-2,
            2.0e-2, 3.0e-2, 3.0e-2, 4.0e-2, 2.0e-2, 1.0e-2, 1.0e-2, 2.0e-2,
            3.0e-2, 4.0e-2,
        ];

        DayCounter dc = new Actual365Fixed();
        Date today = RunnerContext.EvaluationDate;

        HestonProcess hestonProcess = KimHestonProcess(today, dc);

        TestDiscreteGeometricAveragePriceHeston(
            (option, context) => AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(option, context, hestonProcess), tol);
    }

    [Fact]
    public void TestDiscreteGeometricAveragePriceHestonPastFixings()
    {
        // Testing Analytic vs MC for seasoned discrete geometric Asians under Heston...

        // 30-day options need wider tolerance due to uncertainty around what "weekly
        // fixing" dates mean over a 30-day month!
        int[] days = [30, 90, 180, 360, 720];
        double[] strikes = [90, 100, 110];

        double[,,] tol =
        {
            { { 0.04, 0.04 }, { 0.04, 0.04 }, { 0.04, 0.04 }, { 0.05, 0.04 }, { 0.04, 0.04 } },
            { { 0.04, 0.04 }, { 0.04, 0.04 }, { 0.04, 0.04 }, { 0.06, 0.06 }, { 0.06, 0.05 } },
            { { 0.04, 0.04 }, { 0.04, 0.04 }, { 0.04, 0.04 }, { 0.05, 0.04 }, { 0.06, 0.05 } },
        };

        DayCounter dc = new Actual365Fixed();
        Date today = RunnerContext.EvaluationDate;

        HestonProcess hestonProcess = KimHestonProcess(today, dc);

        Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> mcEngine =
            new MakeMCDiscreteGeometricAPHestonEngine<LowDiscrepancy, GeneralStatistics>(hestonProcess, 43)
                .WithSamples(8191)
                .ToEngine();

        OptionType type = OptionType.Call;
        AverageType averageType = AverageType.Geometric;

        for (int strikeIndex = 0; strikeIndex < strikes.Length; strikeIndex++)
        {
            for (int dayIndex = 0; dayIndex < days.Length; dayIndex++)
            {
                for (int k = 0; k < 2; k++)
                {
                    int futureFixings = (int)Math.Floor(days[dayIndex] / 30.0);
                    var fixingDates = new Date[futureFixings];
                    Date expiryDate = today + new Period(days[dayIndex], TimeUnit.Days);

                    for (int i = futureFixings - 1; i >= 0; i--)
                    {
                        fixingDates[i] = expiryDate - (i * 30);
                    }

                    var europeanExercise = new EuropeanExercise(expiryDate);
                    var payoff = new PlainVanillaPayoff(type, strikes[strikeIndex]);

                    double runningAccumulator;
                    int pastFixingsCount;
                    if (k == 0)
                    {
                        runningAccumulator = 100.0;
                        pastFixingsCount = 1;
                    }
                    else
                    {
                        runningAccumulator = 95.0 * 100.0 * 105.0;
                        pastFixingsCount = 3;
                    }

                    var option = new DiscreteAveragingAsianOption(
                        averageType, runningAccumulator, pastFixingsCount, fixingDates, payoff, europeanExercise);

                    double analyticPrice = AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(option, RunnerContext, hestonProcess).NPV!.Value;
                    double mcPrice = mcEngine(option, RunnerContext).NPV!.Value;

                    double tolerance = tol[strikeIndex, dayIndex, k];
                    Assert.True(
                        Math.Abs(analyticPrice - mcPrice) <= tolerance,
                        $"strike {strikes[strikeIndex]}, {days[dayIndex]} days, k {k}: analytic {analyticPrice}, MC {mcPrice}, tolerance {tolerance}");
                }
            }
        }
    }

    [Fact]
    public void TestMCDiscreteArithmeticAveragePriceHeston()
    {
        // Testing Monte Carlo discrete arithmetic average-price Asians in Heston model...

        // data from "A numerical method to price exotic path-dependent
        // options on an underlying described by the Heston stochastic
        // volatility model", Ballestra, Pacelli and Zirilli, Journal
        // of Banking & Finance, 2007 (section 4 - Numerical Results)

        // nb. for Heston, the volatility param below is ignored
        DiscreteAverageData[] cases =
        [
            new(OptionType.Call, 120.0, 100.0, 0.0, 0.05, 1.0 / 12.0, 11.0 / 12.0, 12, 0.1, false, 22.50),
        ];

        double vol = 0.3;
        double v0 = vol * vol;
        double kappa = 11.35;
        double theta = 0.022;
        double sigma = 0.618;
        double rho = -0.5;

        DayCounter dc = new Actual360();
        Date today = RunnerContext.EvaluationDate;

        AverageType averageType = AverageType.Arithmetic;
        double runningSum = 0.0;
        int pastFixings = 0;
        foreach (DiscreteAverageData l in cases)
        {
            var payoff = new PlainVanillaPayoff(l.Type, l.Strike);

            double dt = l.Length / (l.Fixings - 1);
            var timeIncrements = new double[l.Fixings];
            var fixingDates = new Date[l.Fixings];
            timeIncrements[0] = l.First;
            fixingDates[0] = today + (int)(timeIncrements[0] * 365.25);
            for (int i = 1; i < l.Fixings; i++)
            {
                timeIncrements[i] = (i * dt) + l.First;
                fixingDates[i] = today + (int)(timeIncrements[i] * 365.25);
            }

            var exercise = new EuropeanExercise(fixingDates[l.Fixings - 1]);

            var hestonProcess = new HestonProcess(
                new FlatForward(today, l.RiskFreeRate, dc), new FlatForward(today, l.DividendYield, dc), l.Underlying, v0, kappa, theta, sigma, rho);

            Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine =
                new MakeMCDiscreteArithmeticAPHestonEngine<LowDiscrepancy, GeneralStatistics>(hestonProcess, 42)
                    .WithSamples(4095)
                    .ToEngine();

            var option = new DiscreteAveragingAsianOption(averageType, runningSum, pastFixings, fixingDates, payoff, exercise);

            double calculated = engine(option, RunnerContext).NPV!.Value;
            double expected = l.Result;

            // Bounds given in paper, "22.48 to 22.52"
            double tolerance = 5.0e-2;
            Assert.True(Math.Abs(calculated - expected) <= tolerance, $"expected {expected}, calculated {calculated}");

            // Also test the control variate version of the pricer
            Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine2 =
                new MakeMCDiscreteArithmeticAPHestonEngine<LowDiscrepancy, GeneralStatistics>(hestonProcess, 42)
                    .WithSteps(48)
                    .WithSamples(4095)
                    .WithControlVariate(true)
                    .ToEngine();

            double calculatedCV = engine2(option, RunnerContext).NPV!.Value;
            double expectedCV = l.Result;
            tolerance = 3.0e-2;
            Assert.True(Math.Abs(calculatedCV - expectedCV) <= tolerance, $"control variate: expected {expectedCV}, calculated {calculatedCV}");
        }

        // An additional dataset using the Heston parameters coming from "General lower
        // bounds for arithmetic Asian option prices", Applied Mathematical Finance 15(2)
        // 123-149 (2008), by Albrecher, H., Mayer, P., and Schoutens, W. The numerical
        // accuracy of prices given in Table 6 is low, but higher accuracy prices for the
        // same parameters and options are reported by in "Pricing bounds and approximations
        // for discrete arithmetic Asian options under time-changed Levy processes" by Zeng,
        // P.P., and Kwok Y.K. (2013) in Table 4.
        double[] strikes = [60.0, 80.0, 100.0, 120.0, 140.0];
        double[] prices = [42.5990, 29.3698, 18.2360, 10.0565, 4.9609];

        double v02 = 0.0175;
        double kappa2 = 1.5768;
        double theta2 = 0.0398;
        double sigma2 = 0.5751;
        double rho2 = -0.5711;

        DayCounter dc2 = new Actual365Fixed();

        var hestonProcess2 = new HestonProcess(
            new FlatForward(today, 0.03, dc2), new FlatForward(today, 0.0, dc2), 100.0, v02, kappa2, theta2, sigma2, rho2);

        Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine3 =
            new MakeMCDiscreteArithmeticAPHestonEngine<LowDiscrepancy, GeneralStatistics>(hestonProcess2, 42)
                .WithSteps(180)
                .WithSamples(8191)
                .ToEngine();

        Func<DiscreteAveragingAsianOption, EvaluationContext, OneAssetOptionResults> engine4 =
            new MakeMCDiscreteArithmeticAPHestonEngine<LowDiscrepancy, GeneralStatistics>(hestonProcess2, 42)
                .WithSteps(180)
                .WithSamples(8191)
                .WithControlVariate(true)
                .ToEngine();

        var fixingDates2 = new Date[120];
        for (int i = 1; i <= 120; i++)
        {
            fixingDates2[i - 1] = today + new Period(i, TimeUnit.Months);
        }

        var exercise2 = new EuropeanExercise(fixingDates2[119]);

        for (int i = 0; i < prices.Length; i++)
        {
            double strike = strikes[i];
            double expected = prices[i];

            var payoff = new PlainVanillaPayoff(OptionType.Call, strike);

            var option = new DiscreteAveragingAsianOption(averageType, runningSum, pastFixings, fixingDates2, payoff, exercise2);

            double calculated = engine3(option, RunnerContext).NPV!.Value;
            double tolerance = 9.0e-2;
            Assert.True(Math.Abs(calculated - expected) <= tolerance, $"strike {strike}: expected {expected}, calculated {calculated}");

            calculated = engine4(option, RunnerContext).NPV!.Value;
            tolerance = 3.0e-2;
            Assert.True(Math.Abs(calculated - expected) <= tolerance, $"strike {strike}, control variate: expected {expected}, calculated {calculated}");
        }
    }

    // QuantLib's flatRate and flatVol on quotes; here the process at the quotes' values
    private static GeneralizedBlackScholesProcess Process(Date today, double spot, double q, double r, double vol, DayCounter dc) =>
        new BlackScholesMertonProcess(
            spot,
            new FlatForward(today, q, dc),
            new FlatForward(today, r, dc),
            new BlackConstantVol(today, new NullCalendar(), vol, dc));

    // the cases' fixings: futureFixings dates lround(360 / futureFixings) days apart, the first that far from today
    private static Date[] EvenFixings(Date today, int futureFixings)
    {
        var fixingDates = new Date[futureFixings];
        int dt = (int)Math.Round(360.0 / futureFixings, MidpointRounding.AwayFromZero);
        fixingDates[0] = today + dt;
        for (int j = 1; j < futureFixings; j++)
        {
            fixingDates[j] = fixingDates[j - 1] + dt;
        }

        return fixingDates;
    }

    // QuantLib's test utility relativeError
    private static double RelativeError(double x1, double x2, double reference) =>
        reference != 0.0 ? Math.Abs(x1 - x2) / reference : Math.Abs(x1 - x2);

    // QuantLib's test utility: days from a year fraction at a day count of a year
    private static int TimeToDays(double t, int daysPerYear = 360) =>
        (int)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private sealed record ContinuousAverageData(
        OptionType Type,
        double Spot,
        double CurrentAverage,
        double Strike,
        double DividendYield,
        double RiskFreeRate,
        double Volatility,
        int Length,
        int Elapsed,
        double Result);

    private sealed record DiscreteAverageDataTermStructure(
        OptionType Type,
        double Underlying,
        double Strike,
        double B,
        double RiskFreeRate,
        double First,
        double Expiry,
        int Fixings,
        double Volatility,
        string Slope,
        double Result);

    private sealed record DiscreteAverageData(
        OptionType Type,
        double Underlying,
        double Strike,
        double DividendYield,
        double RiskFreeRate,
        double First,
        double Length,
        int Fixings,
        double Volatility,
        bool ControlVariate,
        double Result);
}
