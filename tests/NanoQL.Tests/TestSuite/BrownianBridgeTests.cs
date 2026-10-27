// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/brownianbridge.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BrownianBridgeTests
{
    [Fact]
    public void TestVariates()
    {
        // Testing Brownian-bridge variates...

        double[] times = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 2.0, 5.0];

        Size N = times.Length;

        Size samples = 262143;
        long seed = 42;
        var sobol = new SobolRsg(N, seed);
        var generator = new InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>(sobol);

        var bridge = new BrownianBridge(times);

        var stats1 = new SequenceStatistics(N);
        var stats2 = new SequenceStatistics(N);

        var temp = new double[N];

        for (Size i = 0; i < samples; ++i)
        {
            ReadOnlySpan<double> sample = generator.NextSequence().Value.Span;

            bridge.Transform(sample, temp);
            stats1.Add(temp);

            temp[0] = temp[0] * Math.Sqrt(times[0]);
            for (Size j = 1; j < N; ++j)
            {
                temp[j] = temp[j - 1] + (temp[j] * Math.Sqrt(times[j] - times[j - 1]));
            }

            stats2.Add(temp);
        }

        // normalized single variates
        var expectedMean = new double[N];
        var expectedCovariance = new Matrix(N, N, 0.0);
        for (Size i = 0; i < N; i++)
        {
            expectedCovariance[i, i] = 1.0;
        }

        Real meanTolerance = 1.0e-16;
        Real covTolerance = 2.5e-4;

        double[] mean = [.. stats1.Mean()];
        Matrix covariance = stats1.Covariance();

        Real maxMeanError = MaxDiff(mean, expectedMean);
        Real maxCovError = MaxDiff(Elements(covariance), Elements(expectedCovariance));

        if (maxMeanError > meanTolerance)
        {
            Assert.Fail(
                $"failed to reproduce expected mean values\n    calculated: {new Vector(mean)}\n    expected:   {new Vector(expectedMean)}\n    max error:  {Show(maxMeanError)}");
        }

        if (maxCovError > covTolerance)
        {
            Assert.Fail(
                $"failed to reproduce expected covariance\n    calculated:\n{covariance}    expected:\n{expectedCovariance}    max error:  {Show(maxCovError)}");
        }

        // denormalized sums along the path
        expectedMean = new double[N];
        expectedCovariance = new Matrix(N, N);
        for (Size i = 0; i < N; ++i)
        {
            for (Size j = i; j < N; ++j)
            {
                expectedCovariance[i, j] = expectedCovariance[j, i] = times[i];
            }
        }

        covTolerance = 6.0e-4;

        mean = [.. stats2.Mean()];
        covariance = stats2.Covariance();

        maxMeanError = MaxDiff(mean, expectedMean);
        maxCovError = MaxDiff(Elements(covariance), Elements(expectedCovariance));

        if (maxMeanError > meanTolerance)
        {
            Assert.Fail(
                $"failed to reproduce expected mean values\n    calculated: {new Vector(mean)}\n    expected:   {new Vector(expectedMean)}\n    max error:  {Show(maxMeanError)}");
        }

        if (maxCovError > covTolerance)
        {
            Assert.Fail(
                $"failed to reproduce expected covariance\n    calculated:\n{covariance}    expected:\n{expectedCovariance}    max error:  {Show(maxCovError)}");
        }
    }

    [Fact]
    public void TestPathGeneration()
    {
        // Testing Brownian-bridge path generation...

        double[] times = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 2.0, 5.0, 7.0, 9.0, 10.0];

        var grid = new TimeGrid(times);

        Size N = times.Length;

        Size samples = 131071;
        long seed = 42;
        var sobol = new SobolRsg(N, seed);
        var gsg = new InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>(sobol);

        // Adapted: QuantLib reads its evaluation date (line 180), the day its suite runs; the flat curves and
        // volatility give the same process from any reference date, so a fixed one stands in.
        var today = new Date(15, Month.October, 2026);
        Real x0 = 100.0;
        YieldTermStructure r = new FlatForward(today, 0.06, new Actual365Fixed());
        YieldTermStructure q = new FlatForward(today, 0.03, new Actual365Fixed());
        BlackVolTermStructure sigma = new BlackConstantVol(today, new NullCalendar(), 0.20, new Actual365Fixed());

        StochasticProcess1D process = new BlackScholesMertonProcess(x0, q, r, sigma);

        var generator1 = new PathGenerator<InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>>(
            process, grid, gsg, false);
        var generator2 = new PathGenerator<InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>>(
            process, grid, gsg, true);

        var stats1 = new SequenceStatistics(N);
        var stats2 = new SequenceStatistics(N);

        var temp = new double[N];

        for (Size i = 0; i < samples; ++i)
        {
            MonteCarloPath path1 = generator1.Next().Value;
            for (Size j = 1; j < path1.Length; ++j)
            {
                temp[j - 1] = path1[j];
            }

            stats1.Add(temp);

            MonteCarloPath path2 = generator2.Next().Value;
            for (Size j = 1; j < path2.Length; ++j)
            {
                temp[j - 1] = path2[j];
            }

            stats2.Add(temp);
        }

        double[] expectedMean = [.. stats1.Mean()];
        Matrix expectedCovariance = stats1.Covariance();

        double[] mean = [.. stats2.Mean()];
        Matrix covariance = stats2.Covariance();

        Real meanTolerance = 3.0e-5;
        Real covTolerance = 3.0e-3;

        Real maxMeanError = MaxRelDiff(mean, expectedMean);
        Real maxCovError = MaxRelDiff(Elements(covariance), Elements(expectedCovariance));

        if (maxMeanError > meanTolerance)
        {
            Assert.Fail(
                $"failed to reproduce expected mean values\n    calculated: {new Vector(mean)}\n    expected:   {new Vector(expectedMean)}\n    max error:  {Show(maxMeanError)}");
        }

        if (maxCovError > covTolerance)
        {
            Assert.Fail(
                $"failed to reproduce expected covariance\n    calculated:\n{covariance}    expected:\n{expectedCovariance}    max error:  {Show(maxCovError)}");
        }
    }

    private static Real MaxDiff(double[] a, double[] b)
    {
        Real diff = 0.0;
        for (int i = 0; i < a.Length; i++)
        {
            diff = Math.Max(diff, Math.Abs(a[i] - b[i]));
        }

        return diff;
    }

    private static Real MaxRelDiff(double[] a, double[] b)
    {
        Real diff = 0.0;
        for (int i = 0; i < a.Length; i++)
        {
            diff = Math.Max(diff, Math.Abs((a[i] - b[i]) / b[i]));
        }

        return diff;
    }

    // A matrix's elements, row by row, as its begin() to end().
    private static double[] Elements(Matrix m)
    {
        var e = new double[m.Rows * m.Columns];
        for (int i = 0; i < m.Rows; i++)
        {
            for (int j = 0; j < m.Columns; j++)
            {
                e[(i * m.Columns) + j] = m[i, j];
            }
        }

        return e;
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
