// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/historicalforwardratesanalysis.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The statistical analysis of historical forward rates on a curve of the given traits and interpolator, run at
/// construction into the statistics it is given
/// (<see cref="HistoricalForwardRatesAnalysisFunctions.HistoricalForwardRatesAnalysis"/>).
/// </summary>
/// <remarks>
/// Immutable once built: its constructor fills the caller's statistics, which it gives no accessor to, as QuantLib's
/// (its <c>stats()</c> is commented out). The constructor with no argument gives an analysis of nothing, QuantLib's
/// default constructor.
/// </remarks>
/// <typeparam name="TTraits">The curve's bootstrap traits.</typeparam>
/// <typeparam name="TInterpolator">The curve's interpolator.</typeparam>
public sealed class HistoricalForwardRatesAnalysisImpl<TTraits, TInterpolator> : HistoricalForwardRatesAnalysis
    where TTraits : struct, IYieldTraits
    where TInterpolator : struct, IInterpolator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HistoricalForwardRatesAnalysisImpl{TTraits, TInterpolator}"/>
    /// class: an analysis of nothing.
    /// </summary>
    public HistoricalForwardRatesAnalysisImpl()
    {
        SkippedDates = [];
        SkippedDatesErrorMessage = [];
        FailedDates = [];
        FailedDatesErrorMessage = [];
        FixingPeriods = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoricalForwardRatesAnalysisImpl{TTraits, TInterpolator}"/> class
    /// and runs the analysis, with the default interpolator.
    /// </summary>
    /// <param name="stats">The statistics filled.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="step">The step between dates, positive.</param>
    /// <param name="fwdIndex">The index whose forwards are analysed.</param>
    /// <param name="initialGap">The period to the first forward's fixing.</param>
    /// <param name="horizon">The period to the last forward's fixing, at most.</param>
    /// <param name="iborIndexes">The deposit indexes the curve is bootstrapped on.</param>
    /// <param name="swapIndexes">The swap indexes the curve is bootstrapped on.</param>
    /// <param name="yieldCurveDayCounter">The curve's day counter.</param>
    /// <param name="yieldCurveAccuracy">The bootstrap's accuracy.</param>
    /// <param name="context">The evaluation context, holding the fixings.</param>
    /// <exception cref="ArgumentException">The step isn't positive.</exception>
    public HistoricalForwardRatesAnalysisImpl(
        SequenceStatistics stats,
        Date startDate,
        Date endDate,
        Period step,
        InterestRateIndex fwdIndex,
        Period initialGap,
        Period horizon,
        ReadOnlySpan<IborIndex> iborIndexes,
        ReadOnlySpan<SwapIndex> swapIndexes,
        DayCounter yieldCurveDayCounter,
        double yieldCurveAccuracy,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(stats);
        HistoricalForwardRatesAnalysisResults r =
            HistoricalForwardRatesAnalysisFunctions.HistoricalForwardRatesAnalysis<TTraits, TInterpolator>(
                stats,
                startDate,
                endDate,
                step,
                fwdIndex,
                initialGap,
                horizon,
                iborIndexes,
                swapIndexes,
                yieldCurveDayCounter,
                context,
                yieldCurveAccuracy);
        SkippedDates = r.SkippedDates;
        SkippedDatesErrorMessage = r.SkippedDatesErrorMessage;
        FailedDates = r.FailedDates;
        FailedDatesErrorMessage = r.FailedDatesErrorMessage;
        FixingPeriods = r.FixingPeriods;
    }

    /// <inheritdoc/>
    public override ImmutableArray<Date> SkippedDates { get; }

    /// <inheritdoc/>
    public override ImmutableArray<string> SkippedDatesErrorMessage { get; }

    /// <inheritdoc/>
    public override ImmutableArray<Date> FailedDates { get; }

    /// <inheritdoc/>
    public override ImmutableArray<string> FailedDatesErrorMessage { get; }

    /// <inheritdoc/>
    public override ImmutableArray<Period> FixingPeriods { get; }
}
