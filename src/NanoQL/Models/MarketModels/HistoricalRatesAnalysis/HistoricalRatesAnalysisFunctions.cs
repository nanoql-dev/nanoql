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

/// <summary>The statistical analysis of historical rates: QuantLib's free function
/// <c>historicalRatesAnalysis</c>.</summary>
public static class HistoricalRatesAnalysisFunctions
{
    /// <summary>
    /// The relative changes of the indexes' fixings between successive dates, from the first business day after the
    /// start date to the end date at the given step, added to the statistics, a sample a date after the first; a date
    /// whose fixing is missing or refused is skipped.
    /// </summary>
    /// <remarks>
    /// The statistics are reset to the number of indexes and filled in place, as QuantLib's reference argument is; the
    /// skipped dates and their messages are returned. The fixings are read in the given context, as QuantLib's read the
    /// global settings. QuantLib's <c>catch (std::exception&amp;)</c> catches the <see cref="NanoQLException"/> and
    /// <see cref="ArgumentException"/> QuantLib's errors map to. A step of no length, on which QuantLib's loop never
    /// ends, is refused.
    /// </remarks>
    /// <param name="statistics">The statistics filled.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="step">The step between dates, positive.</param>
    /// <param name="indexes">The indexes, at least one; the first's fixing calendar gives the dates.</param>
    /// <param name="context">The evaluation context, holding the fixings.</param>
    /// <returns>The skipped dates and their messages.</returns>
    /// <exception cref="ArgumentException">No index is given, or the step isn't positive.</exception>
    public static HistoricalRatesAnalysisResults HistoricalRatesAnalysis(
        SequenceStatistics statistics,
        Date startDate,
        Date endDate,
        Period step,
        ReadOnlySpan<InterestRateIndex> indexes,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(context);
        if (indexes.IsEmpty)
        {
            throw new ArgumentException("no index given", nameof(indexes));
        }

        foreach (InterestRateIndex index in indexes)
        {
            ArgumentNullException.ThrowIfNull(index, nameof(indexes));
        }

        if (step.Length <= 0)
        {
            throw new ArgumentException("the step must be positive", nameof(step));
        }

        var skippedDates = new List<Date>();
        var skippedDatesErrorMessage = new List<string>();

        int nRates = indexes.Length;
        statistics.Reset(nRates);

        double[] sample = new double[nRates];
        double[] prevSample = new double[nRates];
        double[] sampleDiff = new double[nRates];

        Calendar cal = indexes[0].FixingCalendar;

        // start with a valid business date
        Date currentDate = cal.Advance(startDate, 1, TimeUnit.Days, BusinessDayConvention.Following);
        bool isFirst = true;

        // Loop over the historical dataset
        for (; currentDate <= endDate; currentDate = cal.Advance(currentDate, step, BusinessDayConvention.Following))
        {
            try
            {
                for (int i = 0; i < nRates; ++i)
                {
                    double fixing = indexes[i].Fixing(currentDate, context, false);
                    sample[i] = fixing;
                }
            }
            catch (Exception e) when (e is NanoQLException or ArgumentException)
            {
                skippedDates.Add(currentDate);
                skippedDatesErrorMessage.Add(e.Message);
                continue;
            }

            // From 2nd step onwards, calculate forward rate relative differences
            if (!isFirst)
            {
                for (int i = 0; i < nRates; ++i)
                {
                    sampleDiff[i] = (sample[i] / prevSample[i]) - 1.0;
                }

                // add observation
                statistics.Add(sampleDiff);
            }
            else
            {
                isFirst = false;
            }

            // Store last calculated forward rates
            (prevSample, sample) = (sample, prevSample);
        }

        return new HistoricalRatesAnalysisResults([.. skippedDates], [.. skippedDatesErrorMessage]);
    }
}
