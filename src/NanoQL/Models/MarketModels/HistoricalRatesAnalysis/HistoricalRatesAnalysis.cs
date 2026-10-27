// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/historicalratesanalysis.hpp ql/models/marketmodels/historicalratesanalysis.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The statistical analysis of historical rates, run at construction: the relative changes of the indexes' fixings
/// between successive dates, added to the statistics it is given
/// (<see cref="HistoricalRatesAnalysisFunctions.HistoricalRatesAnalysis"/>).
/// </summary>
/// <remarks>
/// Immutable once built (the S-mmp plan's SMD6): it holds the caller's statistics, filled by its constructor, as
/// QuantLib's holds the shared pointer it is given.
/// </remarks>
public sealed class HistoricalRatesAnalysis
{
    /// <summary>Initializes a new instance of the <see cref="HistoricalRatesAnalysis"/> class and runs the
    /// analysis.</summary>
    /// <param name="stats">The statistics filled, and held.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="step">The step between dates, positive.</param>
    /// <param name="indexes">The indexes, at least one.</param>
    /// <param name="context">The evaluation context, holding the fixings.</param>
    /// <exception cref="ArgumentException">No index is given, or the step isn't positive.</exception>
    public HistoricalRatesAnalysis(
        SequenceStatistics stats,
        Date startDate,
        Date endDate,
        Period step,
        ReadOnlySpan<InterestRateIndex> indexes,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(stats);
        Stats = stats;
        HistoricalRatesAnalysisResults r =
            HistoricalRatesAnalysisFunctions.HistoricalRatesAnalysis(stats, startDate, endDate, step, indexes, context);
        SkippedDates = r.SkippedDates;
        SkippedDatesErrorMessage = r.SkippedDatesErrorMessage;
    }

    /// <summary>Gets the dates skipped.</summary>
    public ImmutableArray<Date> SkippedDates { get; }

    /// <summary>Gets each skipped date's error message.</summary>
    public ImmutableArray<string> SkippedDatesErrorMessage { get; }

    /// <summary>Gets the statistics given, filled by the analysis.</summary>
    public SequenceStatistics Stats { get; }
}
