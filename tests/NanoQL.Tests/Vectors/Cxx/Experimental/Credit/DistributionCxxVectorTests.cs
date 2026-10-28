// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's distribution vectors, <c>tests/vectors/cxx/experimental/credit/distribution.json</c>: distributions
/// of added values, densities and averages, their buckets and reads, and their tranches.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DistributionCxxVectorTests
{
    [Fact]
    public void DistributionCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/distribution");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name == "refused convolutions")
            {
                check.Output(c, "convolve", Each(c.Input("pairs").EnumerateArray(), p => Summary(ManipulateDistribution.Convolve(Build(p[0]), Build(p[1])))));
                continue;
            }

            JsonElement spec = c.Input("distribution");
            double[] levels = Reals(c.Input("levels"));
            double[] cumulative = Reals(c.Input("cumulative"));
            double[] locates = Reals(c.Input("locates"));
            check.Output(c, "summary", Summary(Build(spec)));
            check.Output(c, "expectedValue", Build(spec).ExpectedValue());
            check.Output(c, "expectedSquare", Build(spec).ExpectedValue(x => x * x));
            check.Output(c, "confidenceLevel", Each(levels, q => Build(spec).ConfidenceLevel(q)));
            check.Output(c, "cumulativeDensity", Each(cumulative, x => Build(spec).CumulativeDensity(x)));
            check.Output(c, "trancheExpectedValue", Each(Pairs(c.Input("trancheValues")), p => Build(spec).TrancheExpectedValue(p.A, p.B)));
            check.Output(c, "cumulativeExcessProbability", Each(Pairs(c.Input("intervals")), p => Build(spec).CumulativeExcessProbability(p.A, p.B)));
            check.Output(c, "locate", Each(locates, x => Build(spec).Locate(x)));
            check.Output(c, "dx", Each(locates, x => Build(spec).Dx(x)));
            check.Output(c, "tranche", Each(Pairs(c.Input("tranches")), p =>
            {
                Distribution d = Build(spec);
                d.Tranche(p.A, p.B);
                int n = d.Size;
                return new object[]
                {
                    n,
                    Enumerable.Range(0, n).Select(k => (object)d.X(k)).ToArray(),
                    Enumerable.Range(0, n).Select(k => (object)d.Cumulative(k)).ToArray(),
                    Enumerable.Range(0, n - 1).Select(k => (object)d.Density(k)).ToArray(),
                    Enumerable.Range(0, n - 1).Select(k => (object)d.Excess(k)).ToArray(),
                };
            }));
        }

        check.Complete();
    }

    /// <summary>A distribution's buckets as the harness's <c>distributionspec.hpp</c> reads them, the densities first:
    /// [size, x, dx, density, cumulative, excess, cumulativeExcess, average].</summary>
    /// <param name="d">The distribution.</param>
    /// <returns>The summary.</returns>
    internal static object[] Summary(Distribution d)
    {
        int n = d.Size;
        object[] density = [.. Enumerable.Range(0, n).Select(k => (object)d.Density(k))];
        object[] cumulative = [.. Enumerable.Range(0, n).Select(k => (object)d.Cumulative(k))];
        object[] excess = [.. Enumerable.Range(0, n).Select(k => (object)d.Excess(k))];
        object[] cumulativeExcess = [.. Enumerable.Range(0, n).Select(k => (object)d.CumulativeExcess(k))];
        object[] average = [.. Enumerable.Range(0, n).Select(k => (object)d.Average(k))];
        object[] x = [.. d.X().Select(v => (object)v)];
        object[] dx = [.. d.Dx().Select(v => (object)v)];
        return [n, x, dx, density, cumulative, excess, cumulativeExcess, average];
    }

    private static Distribution Build(JsonElement spec)
    {
        var d = new Distribution(spec.GetProperty("nBuckets").GetInt32(), VectorCase.Real(spec.GetProperty("xmin")), VectorCase.Real(spec.GetProperty("xmax")));
        foreach (JsonElement x in spec.GetProperty("values").EnumerateArray())
        {
            d.Add(VectorCase.Real(x));
        }

        foreach (JsonElement p in spec.GetProperty("densities").EnumerateArray())
        {
            d.AddDensity((int)VectorCase.Real(p[0]), VectorCase.Real(p[1]));
        }

        foreach (JsonElement p in spec.GetProperty("averages").EnumerateArray())
        {
            d.AddAverage((int)VectorCase.Real(p[0]), VectorCase.Real(p[1]));
        }

        return d;
    }

    private static double[] Reals(JsonElement e) => [.. e.EnumerateArray().Select(VectorCase.Real)];

    private static (double A, double B)[] Pairs(JsonElement e) =>
        [.. e.EnumerateArray().Select(p => (VectorCase.Real(p[0]), VectorCase.Real(p[1])))];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
