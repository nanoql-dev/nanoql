// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.HistoricalRatesAnalysisCxxVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's historical forward rates analysis vectors,
/// <c>tests/vectors/cxx/models/marketmodels/historicalforwardratesanalysis.json</c>:
/// <see cref="HistoricalForwardRatesAnalysisImpl{TTraits, TInterpolator}"/> on log-linear discounts and linear zero
/// rates bootstrapped on Ibor deposits' stored histories, the dates skipped and failed, their messages, the fixing
/// periods and the statistics (the failed dates' messages counted, not read: the curve's range check prints its times
/// in full where QuantLib prints six digits). nanoql bootstraps each date's curve afresh where QuantLib restarts from
/// the last date's, its forwards within the bootstrap's accuracy.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HistoricalForwardRatesAnalysisCxxVectorTests
{
    [Fact]
    public void HistoricalForwardRatesAnalysisCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/historicalforwardratesanalysis");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            IborIndex[] ibors = [.. c.Input("iborIndexes").EnumerateArray().Select(IndexInputs.IborIndexOf)];
            var stats = new SequenceStatistics();
            var start = new Date(c.Input("startDate").GetInt32());
            var end = new Date(c.Input("endDate").GetInt32());
            HistoricalForwardRatesAnalysis analysis = c.String("curve") == "Discount/LogLinear"
                ? Make<Discount, LogLinear>(c, stats, start, end, ibors)
                : Make<ZeroYield, Linear>(c, stats, start, end, ibors);
            check.Output(c, "skippedDates", analysis.SkippedDates.Select(d => d.SerialNumber).ToArray());
            check.Output(c, "skippedDatesErrorMessage", analysis.SkippedDatesErrorMessage);
            check.Output(c, "failedDates", analysis.FailedDates.Select(d => d.SerialNumber).ToArray());
            check.Output(c, "failedDatesErrorMessages", analysis.FailedDatesErrorMessage.Length);
            check.Output(c, "fixingPeriods", analysis.FixingPeriods.Select(p => new[] { p.Length, (int)p.Units }).ToArray());
            CheckStatistics(check, c, stats);
        }

        check.Complete();
    }

    private static HistoricalForwardRatesAnalysisImpl<TTraits, TInterpolator> Make<TTraits, TInterpolator>(
        VectorCase c, SequenceStatistics stats, Date start, Date end, IborIndex[] ibors)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator =>
        new(
            stats,
            start,
            end,
            PeriodOf(c.Input("step")),
            IndexInputs.IborIndexOf(c.Input("fwdIndex")),
            PeriodOf(c.Input("initialGap")),
            PeriodOf(c.Input("horizon")),
            ibors,
            [],
            CashFlowInputs.DayCounterOf(c.Input("yieldCurveDayCounter")),
            c.Double("yieldCurveAccuracy"),
            c.Context());
}
