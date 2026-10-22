// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's GeneralStatistics vectors, <c>tests/vectors/cxx/math/statistics/generalstatistics.json</c>: the
/// members the wheel doesn't expose, <see cref="GeneralStatistics.Percentile"/> and
/// <see cref="GeneralStatistics.TopPercentile"/> at 11 percentiles, and <see cref="GeneralStatistics.Data"/> in the set's
/// order and after <see cref="GeneralStatistics.Sort"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GeneralStatisticsCxxVectorTests
{
    [Fact]
    public void GeneralStatisticsCxxVector()
    {
        var file = VectorFile.Load("cxx/math/statistics/generalstatistics");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] data = Numbers(c.Input("data")), weights = Numbers(c.Input("weights"));
            double[] percentiles = Numbers(c.Input("percentiles"));
            check.Output(c, "percentile", percentiles.Select(y => (object?)(Func<object?>)(() => Make(data, weights).Percentile(y))).ToArray());
            check.Output(
                c, "topPercentile", percentiles.Select(y => (object?)(Func<object?>)(() => Make(data, weights).TopPercentile(y))).ToArray());
            if (c.Expected.TryGetProperty("data", out _))
            {
                check.Output(c, "data", Pairs(Make(data, weights)));
                GeneralStatistics sorted = Make(data, weights);
                sorted.Sort();
                check.Output(c, "sortedData", Pairs(sorted));
            }
        }

        check.Complete();
    }

    private static GeneralStatistics Make(double[] data, double[] weights)
    {
        var s = new GeneralStatistics();
        s.AddSequence(data, weights);
        return s;
    }

    private static object?[] Pairs(GeneralStatistics s)
    {
        var pairs = new List<object?>();
        foreach ((double value, double weight) in s.Data)
        {
            pairs.Add(new[] { value, weight });
        }

        return [.. pairs];
    }
}
