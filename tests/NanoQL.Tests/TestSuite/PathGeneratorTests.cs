// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/pathgenerator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Utilities.DataFormattersFunctions;
using RsgType = NanoQL.Numerics.InverseCumulativeRsg<
    NanoQL.Numerics.RandomSequenceGenerator<NanoQL.Numerics.MersenneTwisterUniformRng>,
    NanoQL.Numerics.InverseCumulativeNormal>;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class PathGeneratorTests
{
    [Fact]
    public void TestPathGenerator()
    {
        // Testing 1-D path generation against cached values...

        var context = new EvaluationContext { EvaluationDate = new Date(26, Month.April, 2005) };

        Real x0 = 100.0;
        YieldTermStructure r = FlatRate(0.05, new Actual360(), context);
        YieldTermStructure q = FlatRate(0.02, new Actual360(), context);
        BlackVolTermStructure sigma = FlatVol(0.20, new Actual360(), context);
        // commented values must be used when Halley's correction is enabled
        TestSingle(new BlackScholesMertonProcess(x0, q, r, sigma), "Black-Scholes", false, 26.13784357783, 467.2928561411);
        // 26.13784357783, 467.2928562519);
        TestSingle(new BlackScholesMertonProcess(x0, q, r, sigma), "Black-Scholes", true, 60.28215549393, 202.6143139999);
        // 60.28215551021, 202.6143139437);

        TestSingle(new GeometricBrownianMotionProcess(100.0, 0.03, 0.20), "geometric Brownian", false, 27.62223714065, 483.6026514084);
        // 27.62223714065, 483.602651493);

        TestSingle(new OrnsteinUhlenbeckProcess(0.1, 0.20), "Ornstein-Uhlenbeck", false, -0.8372003433557, 0.8372003433557);

        TestSingle(new SquareRootProcess(0.1, 0.1, 0.20, 10.0), "square-root", false, 1.70608664108, 6.024200546031);
    }

    [Fact]
    public void TestMultiPathGenerator()
    {
        // Testing n-D path generation against cached values...

        var context = new EvaluationContext { EvaluationDate = new Date(26, Month.April, 2005) };

        Real x0 = 100.0;
        YieldTermStructure r = FlatRate(0.05, new Actual360(), context);
        YieldTermStructure q = FlatRate(0.02, new Actual360(), context);
        BlackVolTermStructure sigma = FlatVol(0.20, new Actual360(), context);

        var correlation = new Matrix(3, 3);
        correlation[0, 0] = 1.0; correlation[0, 1] = 0.9; correlation[0, 2] = 0.7;
        correlation[1, 0] = 0.9; correlation[1, 1] = 1.0; correlation[1, 2] = 0.4;
        correlation[2, 0] = 0.7; correlation[2, 1] = 0.4; correlation[2, 2] = 1.0;

        var processes = new StochasticProcess1D[3];
        StochasticProcess process;

        processes[0] = new BlackScholesMertonProcess(x0, q, r, sigma);
        processes[1] = new BlackScholesMertonProcess(x0, q, r, sigma);
        processes[2] = new BlackScholesMertonProcess(x0, q, r, sigma);
        process = new StochasticProcessArray([.. processes], correlation);
        // commented values must be used when Halley's correction is enabled
        Real[] result1 =
        [
            188.2235868185,
            270.6713069569,
            113.0431145652,
        ];
        // Real result1[] = {
        //     188.2235869273,
        //     270.6713071508,
        //     113.0431145652 };
        Real[] result1a =
        [
            64.89105742957,
            45.12494404804,
            108.0475146914,
        ];
        // Real result1a[] = {
        //     64.89105739157,
        //     45.12494401537,
        //     108.0475146914 };
        TestMultiple(process, "Black-Scholes", result1, result1a);

        processes[0] = new GeometricBrownianMotionProcess(100.0, 0.03, 0.20);
        processes[1] = new GeometricBrownianMotionProcess(100.0, 0.03, 0.20);
        processes[2] = new GeometricBrownianMotionProcess(100.0, 0.03, 0.20);
        process = new StochasticProcessArray([.. processes], correlation);
        Real[] result2 =
        [
            174.8266131680,
            237.2692443633,
            119.1168555440,
        ];
        // Real result2[] = {
        //     174.8266132344,
        //     237.2692444869,
        //     119.1168555605 };
        Real[] result2a =
        [
            57.69082393020,
            38.50016862915,
            116.4056510107,
        ];
        // Real result2a[] = {
        //     57.69082387657,
        //     38.50016858691,
        //     116.4056510107 };
        TestMultiple(process, "geometric Brownian", result2, result2a);

        processes[0] = new OrnsteinUhlenbeckProcess(0.1, 0.20);
        processes[1] = new OrnsteinUhlenbeckProcess(0.1, 0.20);
        processes[2] = new OrnsteinUhlenbeckProcess(0.1, 0.20);
        process = new StochasticProcessArray([.. processes], correlation);
        Real[] result3 =
        [
            0.2942058437284,
            0.5525006418386,
            0.02650931054575,
        ];
        Real[] result3a =
        [
            -0.2942058437284,
            -0.5525006418386,
            -0.02650931054575,
        ];
        TestMultiple(process, "Ornstein-Uhlenbeck", result3, result3a);

        processes[0] = new SquareRootProcess(0.1, 0.1, 0.20, 10.0);
        processes[1] = new SquareRootProcess(0.1, 0.1, 0.20, 10.0);
        processes[2] = new SquareRootProcess(0.1, 0.1, 0.20, 10.0);
        process = new StochasticProcessArray([.. processes], correlation);
        Real[] result4 =
        [
            4.279510844897,
            4.943783503533,
            3.590930385958,
        ];
        Real[] result4a =
        [
            2.763967737724,
            2.226487196647,
            3.503859264341,
        ];
        TestMultiple(process, "square-root", result4, result4a);
    }

    private static void TestSingle(StochasticProcess1D process, string tag, bool brownianBridge, Real expected, Real antithetic)
    {
        long seed = 42;
        double length = 10;
        Size timeSteps = 12;
        RsgType rsg = PseudoRandom.MakeSequenceGenerator(timeSteps, seed);
        var generator = new PathGenerator<RsgType>(process, length, timeSteps, rsg, brownianBridge);
        Size i;
        for (i = 0; i < 100; i++)
        {
            generator.Next();
        }

        Sample<MonteCarloPath> sample = generator.Next();
        Real calculated = sample.Value.Back();
        Real error = Math.Abs(calculated - expected);
        Real tolerance = 2.0e-8;
        if (error > tolerance)
        {
            Assert.Fail(
                $"using {tag} process {(brownianBridge ? "with " : "without ")}brownian bridge:\n"
                + $"    calculated: {Show(calculated)}\n"
                + $"    expected:   {Show(expected)}\n"
                + $"    error:      {Show(error)}\n"
                + $"    tolerance:  {Show(tolerance)}");
        }

        sample = generator.Antithetic();
        calculated = sample.Value.Back();
        error = Math.Abs(calculated - antithetic);
        tolerance = 2.0e-7;
        if (error > tolerance)
        {
            Assert.Fail(
                $"using {tag} process {(brownianBridge ? "with " : "without ")}brownian bridge:\n"
                + "antithetic sample:\n"
                + $"    calculated: {Show(calculated)}\n"
                + $"    expected:   {Show(antithetic)}\n"
                + $"    error:      {Show(error)}\n"
                + $"    tolerance:  {Show(tolerance)}");
        }
    }

    private static void TestMultiple(StochasticProcess process, string tag, Real[] expected, Real[] antithetic)
    {
        long seed = 42;
        double length = 10;
        Size timeSteps = 12;
        Size assets = process.Size;
        RsgType rsg = PseudoRandom.MakeSequenceGenerator(timeSteps * assets, seed);
        var generator = new MultiPathGenerator<RsgType>(process, new TimeGrid(length, timeSteps), rsg, false);
        Size i, j;
        for (i = 0; i < 100; i++)
        {
            generator.Next();
        }

        Sample<MultiPath> sample = generator.Next();
        var calculated = new Vector(assets);
        Real error, tolerance = 2.0e-7;
        for (j = 0; j < assets; j++)
        {
            calculated[j] = sample.Value[j].Back();
        }

        for (j = 0; j < assets; j++)
        {
            error = Math.Abs(calculated[j] - expected[j]);
            if (error > tolerance)
            {
                Assert.Fail(
                    $"using {tag} process ({Ordinal(j + 1)} asset:)\n"
                    + $"    calculated: {Show(calculated[j])}\n"
                    + $"    expected:   {Show(expected[j])}\n"
                    + $"    error:      {Show(error)}\n"
                    + $"    tolerance:  {Show(tolerance)}");
            }
        }

        sample = generator.Antithetic();
        for (j = 0; j < assets; j++)
        {
            calculated[j] = sample.Value[j].Back();
        }

        for (j = 0; j < assets; j++)
        {
            error = Math.Abs(calculated[j] - antithetic[j]);
            if (error > tolerance)
            {
                Assert.Fail(
                    $"using {tag} process ({Ordinal(j + 1)} asset:)\n"
                    + "antithetic sample:\n"
                    + $"    calculated: {Show(calculated[j])}\n"
                    + $"    expected:   {Show(antithetic[j])}\n"
                    + $"    error:      {Show(error)}\n"
                    + $"    tolerance:  {Show(tolerance)}");
            }
        }
    }

    // The test suite's flatRate(forward, dc): a flat forward from the evaluation date.
    private static YieldTermStructure FlatRate(Rate forward, DayCounter dc, EvaluationContext context) =>
        FlatForward.Create(0, new NullCalendar(), forward, dc, context);

    // The test suite's flatVol(vol, dc): a constant volatility from the evaluation date.
    private static BlackVolTermStructure FlatVol(Volatility vol, DayCounter dc, EvaluationContext context) =>
        BlackConstantVol.Create(0, new NullCalendar(), vol, dc, context);

    // std::setprecision(13)
    private static string Show(double x) => x.ToString("G13", CultureInfo.InvariantCulture);
}
