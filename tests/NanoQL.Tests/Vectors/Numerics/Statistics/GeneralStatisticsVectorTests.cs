// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The GeneralStatistics vectors, <c>tests/vectors/math/statistics/generalstatistics.json</c>:
/// <see cref="GeneralStatistics"/>'s members on data sets of 0 to 4 samples, Gaussian samples with unit and random
/// weights, around a large mean, ties, a signed zero, infinities and a NaN, then after <c>Reset</c>; and
/// <c>Add</c>'s refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GeneralStatisticsVectorTests
{
    [Fact]
    public void GeneralStatisticsVector()
    {
        var file = VectorFile.Load("math/statistics/generalstatistics");
        Check<GeneralStatistics>(file);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"math/statistics/generalstatistics: {file.Cases.Count} cases");
    }

    // The accumulator's members on each set, as generalstatistics.py and incrementalstatistics.py read them.
    internal static void Check<TStats>(VectorFile file)
        where TStats : IStatistics, new()
    {
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] data = Numbers(c.Input("data")), weights = Numbers(c.Input("weights"));
            if (c.Expected.TryGetProperty("add", out _))
            {
                check.Output(c, "add", () =>
                {
                    new TStats().Add(data[0], weights[0]);
                    return null;
                });
                continue;
            }

            var s = new TStats();
            for (int i = 0; i < data.Length; i++)
            {
                s.Add(data[i], weights[i]);
            }

            check.Output(c, "samples", s.Samples);
            check.Output(c, "weightSum", () => s.WeightSum());
            check.Output(c, "mean", () => s.Mean());

            // incrementalstatistics.py leaves these out where the leading samples weigh 0 (B1-2).
            if (c.Expected.TryGetProperty("variance", out _))
            {
                check.Output(c, "variance", () => s.Variance());
                check.Output(c, "standardDeviation", () => s.StandardDeviation());
                check.Output(c, "errorEstimate", () => s.ErrorEstimate());
            }

            check.Output(c, "skewness", () => s.Skewness());
            check.Output(c, "kurtosis", () => s.Kurtosis());
            check.Output(c, "min", () => s.Min());
            check.Output(c, "max", () => s.Max());
            s.Reset();
            check.Output(c, "samplesAfterReset", s.Samples);
            check.Output(c, "meanAfterReset", () => s.Mean());
        }

        check.Complete();
    }
}
