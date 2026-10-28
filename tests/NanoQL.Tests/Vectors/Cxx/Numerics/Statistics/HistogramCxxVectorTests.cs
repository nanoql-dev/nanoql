// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The histograms' harness vectors, <c>tests/vectors/cxx/math/statistics/histogram.json</c>: <see cref="Histogram"/>
/// by a number of breaks, by Sturges' and Scott's rules and by given breaks, what it holds.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HistogramCxxVectorTests
{
    [Fact]
    public void HistogramCxxVector()
    {
        var file = VectorFile.Load("cxx/math/statistics/histogram");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] data = [.. c.Input("data").EnumerateArray().Select(VectorCase.Real)];
            JsonElement how = c.Input("how");
            Histogram h = how.TryGetProperty("breaks", out JsonElement k) ? new Histogram(data, k.GetInt32())
                : how.TryGetProperty("algorithm", out JsonElement a) ? new Histogram(data, Enum.Parse<HistogramAlgorithm>(a.GetString()!))
                : new Histogram(data, how.GetProperty("breaksGiven").EnumerateArray().Select(VectorCase.Real).ToArray());
            check.Output(c, "bins", h.Bins);
            check.Output(c, "breaks", h.Breaks.ToArray());
            check.Output(c, "algorithm", h.Algorithm);
            check.Output(c, "empty", h.IsEmpty);
            check.Output(c, "counts", Enumerable.Range(0, h.Bins).Select(h.Counts).ToArray());
            check.Output(c, "frequency", Enumerable.Range(0, h.Bins).Select(h.Frequency).ToArray());
        }

        check.Complete();
    }
}
