// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's historical rates analysis vectors,
/// <c>tests/vectors/cxx/models/marketmodels/historicalratesanalysis.json</c>:
/// <see cref="HistoricalRatesAnalysisFunctions.HistoricalRatesAnalysis"/> on Ibor indexes' stored histories, the dates
/// skipped and their messages, and the statistics' size, samples, weight sum, means and covariance; and the class on
/// the same inputs.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HistoricalRatesAnalysisCxxVectorTests
{
    [Fact]
    public void HistoricalRatesAnalysisCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/historicalratesanalysis");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            InterestRateIndex[] indexes = [.. c.Input("indexes").EnumerateArray().Select(IndexInputs.IborIndexOf)];
            var stats = new SequenceStatistics();
            var analysis = new HistoricalRatesAnalysis(
                stats, new Date(c.Input("startDate").GetInt32()), new Date(c.Input("endDate").GetInt32()), PeriodOf(c.Input("step")), indexes, c.Context());
            Assert.Same(stats, analysis.Stats);
            check.Output(c, "skippedDates", analysis.SkippedDates.Select(d => d.SerialNumber).ToArray());
            check.Output(c, "skippedDatesErrorMessage", analysis.SkippedDatesErrorMessage);
            CheckStatistics(check, c, stats);
        }

        check.Complete();
    }

    // A period [length, units].
    internal static Period PeriodOf(JsonElement p) => new(p[0].GetInt32(), (TimeUnit)p[1].GetInt32());

    // The statistics as the harness's statistics() reads them.
    internal static void CheckStatistics(VectorAssert check, VectorCase c, SequenceStatistics stats)
    {
        check.Output(c, "size", stats.Size);
        check.Output(c, "samples", stats.Samples);
        check.Output(c, "weightSum", stats.WeightSum());
        check.Output(c, "mean", Lazy(() => stats.Mean()));
        check.Output(c, "covariance", Lazy(() => Rows(stats.Covariance())));
    }
}
