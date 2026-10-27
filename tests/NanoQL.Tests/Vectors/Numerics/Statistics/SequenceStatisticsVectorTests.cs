// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The sequence statistics vectors, <c>tests/vectors/math/statistics/sequencestatistics.json</c>:
/// <see cref="SequenceStatistics"/> and <see cref="SequenceStatisticsInc"/> on weighted samples of 1 to 4 components,
/// each lifted member one value per component, the covariance and correlation matrices, the reset's dimension and
/// samples, and <c>Add</c>'s refusal of a sample of another size.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SequenceStatisticsVectorTests
{
    [Fact]
    public void SequenceStatisticsVector()
    {
        var file = VectorFile.Load("math/statistics/sequencestatistics");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.String("class") == "SequenceStatistics")
            {
                Check(check, c, d => new SequenceStatistics(d));
            }
            else
            {
                Check(check, c, d => new SequenceStatisticsInc(d));
            }
        }

        check.Complete();
    }

    private static void Check<TStats>(VectorAssert check, VectorCase c, Func<int, GenericSequenceStatistics<TStats>> make)
        where TStats : IStatistics, new()
    {
        int dimension = c.Input("dimension").GetInt32();
        double[][] samples = [.. c.Input("samples").EnumerateArray().Select(s => s.EnumerateArray().Select(x => x.GetDouble()).ToArray())];
        double[] weights = [.. c.Input("weights").EnumerateArray().Select(x => x.GetDouble())];
        if (c.Expected.TryGetProperty("add", out _))
        {
            check.Output(c, "add", () =>
            {
                make(dimension).Add(samples[0], weights[0]);
                return null;
            });
            return;
        }

        GenericSequenceStatistics<TStats> s = make(dimension);
        for (int i = 0; i < samples.Length; ++i)
        {
            s.Add(samples[i], weights[i]);
        }

        check.Output(c, "size", s.Size);
        check.Output(c, "samples", s.Samples);
        check.Output(c, "weightSum", () => s.WeightSum());
        check.Output(c, "mean", () => s.Mean());
        check.Output(c, "variance", () => s.Variance());
        check.Output(c, "standardDeviation", () => s.StandardDeviation());
        check.Output(c, "errorEstimate", () => s.ErrorEstimate());
        check.Output(c, "skewness", () => s.Skewness());
        check.Output(c, "kurtosis", () => s.Kurtosis());
        check.Output(c, "min", () => s.Min());
        check.Output(c, "max", () => s.Max());
        check.Output(c, "covariance", () => Rows(s.Covariance()));
        check.Output(c, "correlation", () => Rows(s.Correlation()));
        s.Reset();
        check.Output(c, "sizeAfterReset", s.Size);
        check.Output(c, "samplesAfterReset", s.Samples);
    }

    private static double[][] Rows(Matrix m) => [.. Enumerable.Range(0, m.Rows).Select(i => m.Row(i).ToArray())];
}
