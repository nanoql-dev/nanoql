// SPDX-FileCopyrightText: Copyright (C) 2010 Adrian O' Neill
// SPDX-FileCopyrightText: Copyright (C) 2018 Roy Zywina
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/variancegamma.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class VarianceGammaTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(28, Month.October, 2026) };

    [Fact]
    public void TestVarianceGamma()
    {
        // Testing variance-gamma model for European options...
        VarianceGammaProcessData[] processes =
        [
            // spot, q, r, sigma, nu, theta
            new(6000, 0.00, 0.05, 0.20, 0.05, -0.50),
            new(6000, 0.02, 0.05, 0.15, 0.01, -0.50),
        ];

        VarianceGammaOptionData[] options =
        [
            // type, strike, t
            new(OptionType.Call, 5550, 1.0),
            new(OptionType.Call, 5600, 1.0),
            new(OptionType.Call, 5650, 1.0),
            new(OptionType.Call, 5700, 1.0),
            new(OptionType.Call, 5750, 1.0),
            new(OptionType.Call, 5800, 1.0),
            new(OptionType.Call, 5850, 1.0),
            new(OptionType.Call, 5900, 1.0),
            new(OptionType.Call, 5950, 1.0),
            new(OptionType.Call, 6000, 1.0),
            new(OptionType.Call, 6050, 1.0),
            new(OptionType.Call, 6100, 1.0),
            new(OptionType.Call, 6150, 1.0),
            new(OptionType.Call, 6200, 1.0),
            new(OptionType.Call, 6250, 1.0),
            new(OptionType.Call, 6300, 1.0),
            new(OptionType.Call, 6350, 1.0),
            new(OptionType.Call, 6400, 1.0),
            new(OptionType.Call, 6450, 1.0),
            new(OptionType.Call, 6500, 1.0),
            new(OptionType.Call, 6550, 1.0),
            new(OptionType.Put, 5550, 1.0),
        ];

        double[][] results =
        [
            [
                955.1637, 922.7529, 890.9872, 859.8739, 829.4197, 799.6303, 770.5104, 742.0640, 714.2943, 687.2032,
                660.7921, 635.0613, 610.0103, 585.6379, 561.9416, 538.9186, 516.5649, 494.8760, 473.8464, 453.4700,
                433.7400, 234.4870,
            ],
            [
                732.8705, 698.5542, 665.1404, 632.6498, 601.1002, 570.5068, 540.8824, 512.2367, 484.5766, 457.9064,
                432.2273, 407.5381, 383.8346, 361.1102, 339.3559, 318.5599, 298.7087, 279.7864, 261.7751, 244.6552,
                228.4057, 130.9974,
            ],
        ];

        double tol = 0.01;
        DayCounter dc = new Actual360();
        Date today = Context.EvaluationDate;

        for (int i = 0; i < processes.Length; i++)
        {
            YieldTermStructure qTS = new FlatForward(today, processes[i].Q, dc);
            YieldTermStructure rTS = new FlatForward(today, processes[i].R, dc);
            var stochProcess = new VarianceGammaProcess(
                processes[i].S, qTS, rTS, processes[i].Sigma, processes[i].Nu, processes[i].Theta);

            // which requires a list of options
            var optionList = new List<VanillaOption>();
            for (int j = 0; j < options.Length; j++)
            {
                Date exDate = today + TimeToDays(options[j].T);
                var exercise = new EuropeanExercise(exDate);
                var payoff = new PlainVanillaPayoff(options[j].Type, options[j].Strike);

                // Test analytic engine
                var option = new EuropeanOption(payoff, exercise);
                double calculated = VarianceGammaEngine.Calculate(option, Context, stochProcess).NPV!.Value;
                double expected = results[i][j];
                double error = Math.Abs(calculated - expected);
                Assert.False(error > tol, Failure("analytic value", payoff, processes[i], expected, calculated, error, tol));

                optionList.Add(option);
            }

            // Test FFT engine
            // FFT engine is extremely efficient when sent a list of options to calculate first
            ImmutableArray<OneAssetOptionResults> fft = FFTVarianceGammaEngine.Calculate(optionList, Context, stochProcess);
            for (int j = 0; j < options.Length; j++)
            {
                double calculated = fft[j].NPV!.Value;
                double expected = results[i][j];
                double error = Math.Abs(calculated - expected);
                Assert.False(
                    error > tol,
                    Failure("fft value", (StrikedTypePayoff)optionList[j].Payoff!, processes[i], expected, calculated, error, tol));
            }
        }
    }

    [Fact]
    public void TestSingularityAtZero()
    {
        // Testing variance-gamma model integration around zero...
        double stock = 100;
        double strike = 98;
        double sigma = 0.12;
        double mu = -0.14;
        double kappa = 0.2;

        var valuation = new Date(1, Month.January, 2017);
        var maturity = new Date(10, Month.January, 2017);
        DayCounter discountCounter = new Thirty360(Thirty360Convention.BondBasis);
        var context = new EvaluationContext { EvaluationDate = valuation };

        var exercise = new EuropeanExercise(maturity);
        var payoff = new PlainVanillaPayoff(OptionType.Call, strike);
        var option = new VanillaOption(payoff, exercise);

        var dividend = new FlatForward(valuation, 0.0, discountCounter);
        var disc = new FlatForward(valuation, 0.05, discountCounter);
        var process = new VarianceGammaProcess(stock, dividend, disc, sigma, kappa, mu);

        // without the fix, the call below goes into an infinite loop,
        // which is hard to test for.  We're just happy to see the test
        // case finish, hence the lack of an assertion.
        _ = VarianceGammaEngine.Calculate(option, context, process);
    }

    private static string Failure(
        string greekName,
        StrikedTypePayoff payoff,
        VarianceGammaProcessData p,
        double expected,
        double calculated,
        double error,
        double tolerance) =>
        $"{payoff.OptionType} option with {payoff.Name} payoff:\n    spot value:       {p.S}\n    strike:           "
        + $"{payoff.Strike}\n    dividend yield:   {p.Q}\n    risk-free rate:   {p.R}\n    sigma:            {p.Sigma}\n"
        + $"    nu:               {p.Nu}\n    theta:            {p.Theta}\n\n    expected   {greekName}: {expected}\n"
        + $"    calculated {greekName}: {calculated}\n    error:            {error}\n    tolerance:        {tolerance}";

    // The test suite's timeToDays: Integer(std::lround(t * daysPerYear)).
    private static int TimeToDays(double t, int daysPerYear = 360) =>
        (int)Math.Round(t * daysPerYear, MidpointRounding.AwayFromZero);

    private sealed record VarianceGammaProcessData(double S, double Q, double R, double Sigma, double Nu, double Theta);

    private sealed record VarianceGammaOptionData(OptionType Type, double Strike, double T);
}
