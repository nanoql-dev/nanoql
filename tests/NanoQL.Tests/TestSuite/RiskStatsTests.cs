// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/riskstats.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;
using static NanoQL.Numerics.ComparisonFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class RiskStatsTests
{
    // qldefines.hpp's QL_MAX_REAL and QL_MIN_REAL, which nanoql keeps internal.
    private const double QL_MAX_REAL = double.MaxValue;
    private const double QL_MIN_REAL = -double.MaxValue;

    // Adapted: QuantLib's GenericGaussianStatistics<IncrementalStatistics> and RiskStatistics are IncrementalStatistics
    // and GeneralStatistics, whose Gaussian and risk measures are extension members; and each SobolRsg(1) (line 57),
    // QuantLib's default seed 0, takes seed 1, since nanoql refuses 0 (Divergences/SeedZeroTests.cs): one dimension lies
    // in every table, where the seed isn't read.
    [Fact]
    public void TestResults()
    {
        // Testing risk measures...

        var igs = new IncrementalStatistics();
        var s = new GeneralStatistics();

        Real[] averages = [-100.0, -1.0, 0.0, 1.0, 100.0];
        Real[] sigmas = [0.1, 1.0, 100.0];
        Size i, j, k, N;
        N = (Size)Math.Pow(2.0, 16) - 1;
        Real dataMin, dataMax;
        var data = new Real[N];
        var weights = new Real[N];

        for (i = 0; i < averages.Length; i++)
        {
            for (j = 0; j < sigmas.Length; j++)
            {
                var normal = new NormalDistribution(averages[i], sigmas[j]);
                var cumulative = new CumulativeNormalDistribution(averages[i], sigmas[j]);
                var inverseCum = new InverseCumulativeNormal(averages[i], sigmas[j]);

                var rng = new SobolRsg(1, 1);
                dataMin = QL_MAX_REAL;
                dataMax = QL_MIN_REAL;
                for (k = 0; k < N; k++)
                {
                    data[k] = inverseCum.Evaluate(rng.NextSequence().Value.Span[0]);
                    dataMin = data[k] < dataMin ? data[k] : dataMin;
                    dataMax = dataMax < data[k] ? data[k] : dataMax;
                    weights[k] = 1.0;
                }

                igs.AddSequence(data, weights);
                s.AddSequence(data, weights);

                // checks
                Real calculated, expected;
                Real tolerance;

                if (igs.Samples != N)
                {
                    Fail($"IncrementalGaussianStatistics: wrong number of samples\n    calculated: {igs.Samples}\n    expected:   {N}");
                }

                if (s.Samples != N)
                {
                    Fail($"RiskStatistics: wrong number of samples\n    calculated: {s.Samples}\n    expected:   {N}");
                }

                // weightSum()
                tolerance = 1e-10;
                expected = 0.0;
                foreach (Real w in weights)
                {
                    expected += w;
                }

                calculated = igs.WeightSum();
                Check("IncrementalGaussianStatistics: wrong sum of weights", calculated, expected, tolerance);
                calculated = s.WeightSum();
                Check("RiskStatistics: wrong sum of weights", calculated, expected, tolerance);

                // min
                tolerance = 1e-12;
                expected = dataMin;
                calculated = igs.Min();
                Check("IncrementalGaussianStatistics: wrong minimum value", calculated, expected, tolerance);
                calculated = s.Min();
                Check("RiskStatistics: wrong minimum value", calculated, expected, tolerance);

                // max
                expected = dataMax;
                calculated = igs.Max();
                Check("IncrementalGaussianStatistics: wrong maximum value", calculated, expected, tolerance);
                calculated = s.Max();
                Check("RiskStatistics: wrong maximum value", calculated, expected, tolerance);

                string forN = $" for N({Show(averages[i])}, {Show(sigmas[j])})";

                // mean
                expected = averages[i];
                tolerance = expected == 0.0 ? 1.0e-13 : Math.Abs(expected) * 1.0e-13;
                calculated = igs.Mean();
                Check("IncrementalGaussianStatistics: wrong mean value" + forN, calculated, expected, tolerance);
                calculated = s.Mean();
                Check("RiskStatistics: wrong mean value" + forN, calculated, expected, tolerance);

                // variance
                expected = sigmas[j] * sigmas[j];
                tolerance = expected * 1.0e-1;
                calculated = igs.Variance();
                Check("IncrementalGaussianStatistics: wrong variance" + forN, calculated, expected, tolerance);
                calculated = s.Variance();
                Check("RiskStatistics: wrong variance" + forN, calculated, expected, tolerance);

                // standardDeviation
                expected = sigmas[j];
                tolerance = expected * 1.0e-1;
                calculated = igs.StandardDeviation();
                Check("IncrementalGaussianStatistics: wrong standard deviation" + forN, calculated, expected, tolerance);
                calculated = s.StandardDeviation();
                Check("RiskStatistics: wrong standard deviation" + forN, calculated, expected, tolerance);

                // missing errorEstimate() test

                // skewness
                expected = 0.0;
                tolerance = 1.0e-4;
                calculated = igs.Skewness();
                Check("IncrementalGaussianStatistics: wrong skewness" + forN, calculated, expected, tolerance);
                calculated = s.Skewness();
                Check("RiskStatistics: wrong skewness" + forN, calculated, expected, tolerance);

                // kurtosis
                expected = 0.0;
                tolerance = 1.0e-1;
                calculated = igs.Kurtosis();
                Check("IncrementalGaussianStatistics: wrong kurtosis" + forN, calculated, expected, tolerance);
                calculated = s.Kurtosis();
                Check("RiskStatistics: wrong kurtosis" + forN, calculated, expected, tolerance);

                // percentile
                expected = averages[i];
                tolerance = expected == 0.0 ? 1.0e-3 : Math.Abs(expected * 1.0e-3);
                calculated = igs.GaussianPercentile(0.5);
                Check("IncrementalGaussianStatistics: wrong Gaussian percentile" + forN, calculated, expected, tolerance);
                calculated = s.GaussianPercentile(0.5);
                Check("RiskStatistics: wrong Gaussian percentile" + forN, calculated, expected, tolerance);
                calculated = s.Percentile(0.5);
                Check("RiskStatistics: wrong percentile" + forN, calculated, expected, tolerance);

                // potential upside
                Real upper_tail = averages[i] + (2.0 * sigmas[j]),
                     lower_tail = averages[i] - (2.0 * sigmas[j]);
                Real twoSigma = cumulative.Evaluate(upper_tail);

                expected = upper_tail < 0.0 ? 0.0 : upper_tail;
                tolerance = expected == 0.0 ? 1.0e-3 : Math.Abs(expected * 1.0e-3);
                calculated = igs.GaussianPotentialUpside(twoSigma);
                Check("IncrementalGaussianStatistics: wrong Gaussian potential upside" + forN, calculated, expected, tolerance);
                calculated = s.GaussianPotentialUpside(twoSigma);
                Check("RiskStatistics: wrong Gaussian potential upside" + forN, calculated, expected, tolerance);
                calculated = s.PotentialUpside(twoSigma);
                Check("RiskStatistics: wrong potential upside" + forN, calculated, expected, tolerance);

                // just to check that GaussianStatistics<StatsHolder> does work
                var h = new StatsHolder(s.Mean(), s.StandardDeviation());
                StatsHolder test = h;
                expected = s.GaussianPotentialUpside(twoSigma);
                calculated = test.GaussianPotentialUpside(twoSigma);
                if (!Close(calculated, expected))
                {
                    Fail($"GenericGaussianStatistics<StatsHolder> fails\n  calculated: {Show16(calculated)}\n  expected: {Show16(expected)}");
                }

                // value-at-risk
                expected = -(0.0 < lower_tail ? 0.0 : lower_tail);
                tolerance = expected == 0.0 ? 1.0e-3 : Math.Abs(expected * 1.0e-3);
                calculated = igs.GaussianValueAtRisk(twoSigma);
                Check("IncrementalGaussianStatistics: wrong Gaussian value-at-risk" + forN, calculated, expected, tolerance);
                calculated = s.GaussianValueAtRisk(twoSigma);
                Check("RiskStatistics: wrong Gaussian value-at-risk" + forN, calculated, expected, tolerance);
                calculated = s.ValueAtRisk(twoSigma);
                Check("RiskStatistics: wrong value-at-risk" + forN, calculated, expected, tolerance);

                if (averages[i] > 0.0 && sigmas[j] < averages[i])
                {
                    // no data will miss the targets:
                    // skip the rest of this iteration
                    igs.Reset();
                    s.Reset();
                    continue;
                }

                // expected shortfall
                Real shortfallMean = averages[i] - (sigmas[j] * sigmas[j] * normal.Evaluate(lower_tail) / (1.0 - twoSigma));
                expected = -(0.0 < shortfallMean ? 0.0 : shortfallMean);
                tolerance = expected == 0.0 ? 1.0e-4 : Math.Abs(expected) * 1.0e-2;
                calculated = igs.GaussianExpectedShortfall(twoSigma);
                Check("IncrementalGaussianStatistics: wrong Gaussian expected shortfall" + forN, calculated, expected, tolerance);
                calculated = s.GaussianExpectedShortfall(twoSigma);
                Check("RiskStatistics: wrong Gaussian expected shortfall" + forN, calculated, expected, tolerance);
                calculated = s.ExpectedShortfall(twoSigma);
                Check("RiskStatistics: wrong expected shortfall" + forN, calculated, expected, tolerance);

                // shortfall
                expected = 0.5;
                tolerance = expected == 0.0 ? 1.0e-3 : Math.Abs(expected * 1.0e-3);
                calculated = igs.GaussianShortfall(averages[i]);
                Check("IncrementalGaussianStatistics: wrong Gaussian shortfall" + forN, calculated, expected, tolerance);
                calculated = s.GaussianShortfall(averages[i]);
                Check("RiskStatistics: wrong Gaussian shortfall" + forN, calculated, expected, tolerance);
                calculated = s.Shortfall(averages[i]);
                Check("RiskStatistics: wrong shortfall" + forN, calculated, expected, tolerance);

                // average shortfall
                expected = sigmas[j] / Math.Sqrt(2.0 * Math.PI) * 2.0;
                tolerance = expected * 1.0e-3;
                calculated = igs.GaussianAverageShortfall(averages[i]);
                Check("IncrementalGaussianStatistics: wrong Gaussian average shortfall" + forN, calculated, expected, tolerance);
                calculated = s.GaussianAverageShortfall(averages[i]);
                Check("RiskStatistics: wrong Gaussian average shortfall" + forN, calculated, expected, tolerance);
                calculated = s.AverageShortfall(averages[i]);
                Check("RiskStatistics: wrong average shortfall" + forN, calculated, expected, tolerance);

                // regret
                expected = sigmas[j] * sigmas[j];
                tolerance = expected * 1.0e-1;
                calculated = igs.GaussianRegret(averages[i]);
                Check("IncrementalGaussianStatistics: wrong Gaussian regret" + forN, calculated, expected, tolerance);
                calculated = s.GaussianRegret(averages[i]);
                Check("RiskStatistics: wrong Gaussian regret" + forN, calculated, expected, tolerance);
                calculated = s.Regret(averages[i]);
                Check("RiskStatistics: wrong regret" + forN, calculated, expected, tolerance);

                // downsideVariance
                expected = s.DownsideVariance();
                tolerance = expected == 0.0 ? 1.0e-3 : Math.Abs(expected * 1.0e-3);
                calculated = igs.DownsideVariance();
                Check("IncrementalGaussianStatistics: wrong downside variance" + forN, calculated, expected, tolerance);
                calculated = igs.GaussianDownsideVariance();
                Check("IncrementalGaussianStatistics: wrong Gaussian downside variance" + forN, calculated, expected, tolerance);

                // downsideVariance
                if (averages[i] == 0.0)
                {
                    expected = sigmas[j] * sigmas[j];
                    tolerance = expected * 1.0e-3;
                    calculated = igs.DownsideVariance();
                    Check("IncrementalGaussianStatistics: wrong downside variance" + forN, calculated, expected, tolerance);
                    calculated = igs.GaussianDownsideVariance();
                    Check("IncrementalGaussianStatistics: wrong Gaussian downside variance" + forN, calculated, expected, tolerance);
                    calculated = s.DownsideVariance();
                    Check("RiskStatistics: wrong downside variance" + forN, calculated, expected, tolerance);
                    calculated = s.GaussianDownsideVariance();
                    Check("RiskStatistics: wrong Gaussian downside variance" + forN, calculated, expected, tolerance);
                }

                igs.Reset();
                s.Reset();
            }
        }
    }

    // BOOST_FAIL: the case stops at the first failure, with setprecision(16).
    private static void Check(string what, Real calculated, Real expected, Real tolerance)
    {
        if (Math.Abs(calculated - expected) > tolerance)
        {
            Fail($"{what}\n    calculated: {Show16(calculated)}\n    expected:   {Show16(expected)}\n    tolerance:  {Show16(tolerance)}");
        }
    }

    private static void Fail(string message) => Assert.Fail(message);

    private static string Show(double x) => x.ToString("G6", CultureInfo.InvariantCulture);

    private static string Show16(double x) => x.ToString("G16", CultureInfo.InvariantCulture);
}
