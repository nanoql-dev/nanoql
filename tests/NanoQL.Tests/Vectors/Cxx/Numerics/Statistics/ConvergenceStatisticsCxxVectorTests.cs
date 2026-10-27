// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's convergence statistics vectors,
/// <c>tests/vectors/cxx/math/statistics/convergencestatistics.json</c>:
/// <see cref="ConvergenceStatistics{TStats, TRule}"/>'s table on the doubling rule over incremental and general
/// statistics, weighted, and after a reset part way through.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConvergenceStatisticsCxxVectorTests
{
    [Fact]
    public void ConvergenceStatisticsCxxVector()
    {
        var file = VectorFile.Load("cxx/math/statistics/convergencestatistics");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.String("statistics") == "Statistics")
            {
                Check<GeneralStatistics>(check, c);
            }
            else
            {
                Check<IncrementalStatistics>(check, c);
            }
        }

        check.Complete();
    }

    private static void Check<TStats>(VectorAssert check, VectorCase c)
        where TStats : IStatistics, new()
    {
        double[] values = c.Doubles("values"), weights = c.Doubles("weights");
        System.Text.Json.JsonElement reset = c.Input("resetAfter");
        int? resetAfter = reset.ValueKind == System.Text.Json.JsonValueKind.Null ? null : reset.GetInt32();
        var stats = new ConvergenceStatistics<TStats, DoublingConvergenceSteps>();
        for (int i = 0; i < values.Length; ++i)
        {
            if (i == resetAfter)
            {
                stats.Reset();
            }

            stats.Add(values[i], weights[i]);
        }

        check.Output(c, "table", stats.ConvergenceTable.Select(r => new object[] { r.Samples, r.Mean }).ToArray());
        check.Output(c, "samples", stats.Samples);
        check.Output(c, "mean", stats.Mean());
    }
}
