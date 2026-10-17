// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/brownianbridge.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testPathGeneration, which waits for S-eq's Black-Scholes process

using System.Globalization;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class BrownianBridgeTests
{
    // Adapted: QuantLib's SequenceStatistics (lines 76-77, 85, 90, 106-107 and 140-141), which S2 doesn't port, is
    // replaced by SequenceMoments below, which computes the mean and the covariance as QuantLib's GeneralStatistics
    // and GenericSequenceStatistics do, in the same order.
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

        var stats1 = new SequenceMoments(N);
        var stats2 = new SequenceMoments(N);

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

        double[] mean = stats1.Mean();
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

        mean = stats2.Mean();
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

    private static Real MaxDiff(double[] a, double[] b)
    {
        Real diff = 0.0;
        for (int i = 0; i < a.Length; i++)
        {
            diff = Math.Max(diff, Math.Abs(a[i] - b[i]));
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

    // The mean and covariance of equally weighted sequences, as QuantLib's SequenceStatistics computes them: each
    // dimension's mean is GeneralStatistics' sum of x times its weight over the sum of the weights, in the order the
    // samples came; the covariance is GenericSequenceStatistics' quadratic sum (each sample's outer product with
    // itself, times its weight) over the weights, less the outer product of the means, times n / (n - 1).
    private sealed class SequenceMoments
    {
        private readonly int dimension;
        private readonly List<double>[] samples;
        private readonly Matrix quadraticSum;

        public SequenceMoments(int dimension)
        {
            this.dimension = dimension;
            samples = [.. Enumerable.Range(0, dimension).Select(_ => new List<double>())];
            quadraticSum = new Matrix(dimension, dimension, 0.0);
        }

        public void Add(ReadOnlySpan<double> sample)
        {
            const double weight = 1.0;
            for (int i = 0; i < dimension; i++)
            {
                for (int j = 0; j < dimension; j++)
                {
                    quadraticSum[i, j] += weight * (sample[i] * sample[j]);
                }
            }

            for (int i = 0; i < dimension; i++)
            {
                samples[i].Add(sample[i]);
            }
        }

        public double[] Mean()
        {
            var mean = new double[dimension];
            for (int i = 0; i < dimension; i++)
            {
                double num = 0.0, den = 0.0;
                foreach (double x in samples[i])
                {
                    num += x * 1.0;
                    den += 1.0;
                }

                mean[i] = num / den;
            }

            return mean;
        }

        public Matrix Covariance()
        {
            double sampleWeight = 0.0;
            for (int k = 0; k < samples[0].Count; k++)
            {
                sampleWeight += 1.0;
            }

            double sampleNumber = samples[0].Count;
            double[] m = Mean();
            double inv = 1.0 / sampleWeight;
            var result = new Matrix(dimension, dimension);
            for (int i = 0; i < dimension; i++)
            {
                for (int j = 0; j < dimension; j++)
                {
                    result[i, j] = ((inv * quadraticSum[i, j]) - (m[i] * m[j])) * (sampleNumber / (sampleNumber - 1.0));
                }
            }

            return result;
        }
    }
}
