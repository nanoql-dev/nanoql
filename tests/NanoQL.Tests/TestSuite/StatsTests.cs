// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/stats.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testSequenceStatistics, testConvergenceStatistics (S-mmp's sequence and convergence statistics)

using System.Globalization;
using NanoQL.Numerics;
using static NanoQL.Numerics.ComparisonFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class StatsTests
{
    private static readonly Real[] Data = [3.0, 4.0, 5.0, 2.0, 3.0, 4.0, 5.0, 6.0, 4.0, 7.0];
    private static readonly Real[] Weights = [1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0];

    // BOOST_ERROR records a failure and goes on; the case fails at its end.
    private readonly List<string> errors = [];

    [Fact]
    public void TestStatistics()
    {
        // Testing statistics...

        Check<IncrementalStatistics>("IncrementalStatistics");
        Check<GeneralStatistics>("Statistics");
    }

    [Fact]
    public void TestIncrementalStatistics()
    {
        // Testing incremental statistics...

        // With QuantLib 1.7 IncrementalStatistics was changed to
        // a wrapper to the boost accumulator library. This is
        // a test of the new implementation against results from
        // the old one.

        var mt = new MersenneTwisterUniformRng(42);

        var stat = new IncrementalStatistics();

        for (Size i = 0; i < 500000; ++i)
        {
            Real x = 2.0 * (mt.NextReal() - 0.5) * 1234.0;
            Real w = mt.NextReal();
            stat.Add(x, w);
        }

        if (stat.Samples != 500000)
        {
            errors.Add($"stat.samples()  ({stat.Samples}) can not be reproduced against cached result ({500000})");
        }

        TestIncStat("stat.weightSum()", stat.WeightSum(), 2.5003623600676749e+05);
        TestIncStat("stat.mean()", stat.Mean(), 4.9122325964293845e-01);
        TestIncStat("stat.variance()", stat.Variance(), 5.0706503959683329e+05);
        TestIncStat("stat.standardDeviation()", stat.StandardDeviation(), 7.1208499464378076e+02);
        TestIncStat("stat.errorEstimate()", stat.ErrorEstimate(), 1.0070402569876076e+00);
        TestIncStat("stat.skewness()", stat.Skewness(), -1.7360169326720038e-03);
        TestIncStat("stat.kurtosis()", stat.Kurtosis(), -1.1990742562085395e+00);
        TestIncStat("stat.min()", stat.Min(), -1.2339945045639761e+03);
        TestIncStat("stat.max()", stat.Max(), 1.2339958308008499e+03);
        TestIncStat("stat.downsideVariance()", stat.DownsideVariance(), 5.0786776146975247e+05);
        TestIncStat("stat.downsideDeviation()", stat.DownsideDeviation(), 7.1264841364431061e+02);

        // This is a test for numerical stability,
        // where the old implementation fails

        var normal_gen = new InverseCumulativeRng<MersenneTwisterUniformRng, InverseCumulativeNormal>(mt);

        var stat2 = new IncrementalStatistics();

        for (Size i = 0; i < 500000; ++i)
        {
            Real x = (normal_gen.Next().Value * 1E-1) + 1E8;
            Real w = 1.0;
            stat2.Add(x, w);
        }

        Real tol = 1E-5;

        if (Math.Abs(stat2.Variance() - 1E-2) > tol)
        {
            errors.Add($"variance ({stat2.Variance()}) out of expected range {1E-2} +- {tol}");
        }

        Assert.Empty(errors);
    }

    private static void Check<S>(string name)
        where S : IStatistics, new()
    {
        var s = new S();
        for (Size i = 0; i < Data.Length; i++)
        {
            s.Add(Data[i], Weights[i]);
        }

        Real calculated, expected;
        Real tolerance;

        if (s.Samples != Data.Length)
        {
            Assert.Fail(
                $"{name}: wrong number of samples\n    calculated: {s.Samples}\n    expected:   {Data.Length}");
        }

        // std::accumulate, in order
        expected = 0.0;
        foreach (Real w in Weights)
        {
            expected += w;
        }

        calculated = s.WeightSum();
        if (calculated != expected)
        {
            Assert.Fail($"{name}: wrong sum of weights\n    calculated: {calculated}\n    expected:   {expected}");
        }

        expected = Data.Min();
        calculated = s.Min();
        if (calculated != expected)
        {
            Assert.Fail($"{name}: wrong minimum value\n    calculated: {calculated}\n    expected:   {expected}");
        }

        expected = Data.Max();
        calculated = s.Max();
        if (calculated != expected)
        {
            Assert.Fail($"{name}: wrong maximum value\n    calculated: {calculated}\n    expected:   {expected}");
        }

        expected = 4.3;
        tolerance = 1.0e-9;
        calculated = s.Mean();
        if (Math.Abs(calculated - expected) > tolerance)
        {
            Assert.Fail($"{name}: wrong mean value\n    calculated: {calculated}\n    expected:   {expected}");
        }

        expected = 2.23333333333;
        calculated = s.Variance();
        if (Math.Abs(calculated - expected) > tolerance)
        {
            Assert.Fail($"{name}: wrong variance\n    calculated: {calculated}\n    expected:   {expected}");
        }

        expected = 1.4944341181;
        calculated = s.StandardDeviation();
        if (Math.Abs(calculated - expected) > tolerance)
        {
            Assert.Fail($"{name}: wrong standard deviation\n    calculated: {calculated}\n    expected:   {expected}");
        }

        expected = 0.359543071407;
        calculated = s.Skewness();
        if (Math.Abs(calculated - expected) > tolerance)
        {
            Assert.Fail($"{name}: wrong skewness\n    calculated: {calculated}\n    expected:   {expected}");
        }

        expected = -0.151799637209;
        calculated = s.Kurtosis();
        if (Math.Abs(calculated - expected) > tolerance)
        {
            Assert.Fail($"{name}: wrong kurtosis\n    calculated: {calculated}\n    expected:   {expected}");
        }
    }

    // TEST_INC_STAT: close_enough to the cached result, reported with setprecision(16) and std::scientific.
    private void TestIncStat(string expr, Real value, Real expected)
    {
        if (!CloseEnough(value, expected))
        {
            errors.Add(
                $"{expr} ({value.ToString("E15", CultureInfo.InvariantCulture)}) can not be reproduced against cached "
                + $"result ({expected.ToString("E15", CultureInfo.InvariantCulture)})");
        }
    }
}
