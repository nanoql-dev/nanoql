// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/pathgenerator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testMultiPathGenerator, which waits for MultiPathGenerator (S-sr)

using System.Globalization;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using RsgType = NanoQL.Numerics.InverseCumulativeRsg<
    NanoQL.Numerics.RandomSequenceGenerator<NanoQL.Numerics.MersenneTwisterUniformRng>,
    NanoQL.Numerics.InverseCumulativeNormal>;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class PathGeneratorTests
{
    // Adapted: only the Ornstein-Uhlenbeck line (lines 169-171) is kept. The Black-Scholes, geometric Brownian and
    // square-root lines (155-162, 164-167 and 173-175) need processes S2 doesn't port, so they are dropped, with the
    // quote and term structures only they read (150-153) and the evaluation date (148), which the Ornstein-Uhlenbeck
    // process doesn't read.
    [Fact]
    public void TestPathGenerator()
    {
        // Testing 1-D path generation against cached values...

        TestSingle(new OrnsteinUhlenbeckProcess(0.1, 0.20), "Ornstein-Uhlenbeck", false, -0.8372003433557, 0.8372003433557);
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

    // std::setprecision(13)
    private static string Show(double x) => x.ToString("G13", CultureInfo.InvariantCulture);
}
