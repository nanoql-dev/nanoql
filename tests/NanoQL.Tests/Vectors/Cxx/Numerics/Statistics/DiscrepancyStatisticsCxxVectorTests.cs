// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;


namespace NanoQL.Tests.Vectors;

/// <summary>
/// The discrepancy statistics' harness vectors, <c>tests/vectors/cxx/math/statistics/discrepancystatistics.json</c>:
/// <see cref="DiscrepancyStatistics"/> of a dimension given, its discrepancy after each sample.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscrepancyStatisticsCxxVectorTests
{
    [Fact]
    public void DiscrepancyStatisticsCxxVector()
    {
        var file = VectorFile.Load("cxx/math/statistics/discrepancystatistics");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var stat = new DiscrepancyStatistics(c.Int("dimension"));
            var discrepancy = new List<double>();
            foreach (System.Text.Json.JsonElement sample in c.Input("samples").EnumerateArray())
            {
                stat.Add(sample.EnumerateArray().Select(VectorCase.Real).ToArray());
                discrepancy.Add(stat.Discrepancy());
            }

            check.Output(c, "discrepancy", discrepancy.ToArray());
        }

        check.Complete();
    }
}
