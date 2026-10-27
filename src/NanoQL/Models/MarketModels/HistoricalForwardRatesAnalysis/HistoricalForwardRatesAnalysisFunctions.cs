// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 Katiuscia Manzoni
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/historicalforwardratesanalysis.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The statistical analysis of historical forward rates: QuantLib's free function template
/// <c>historicalForwardRatesAnalysis&lt;Traits, Interpolator&gt;</c>.
/// </summary>
public static class HistoricalForwardRatesAnalysisFunctions
{
    /// <summary>
    /// The relative changes, between successive dates, of the forward rates of the given index at fixed periods ahead,
    /// read off a curve bootstrapped on each date from the deposit and swap indexes' fixings, from the first business
    /// day after the start date to the end date at the given step, added to the statistics, a sample a date after the
    /// first. A date whose fixing is missing or refused is skipped; a date whose curve or forward rate fails is failed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The statistics are reset to the number of forwards and filled in place; the dates skipped and failed, their
    /// messages and the fixing periods are returned (the S-mmp plan's SMD6). Each date is evaluated in a copy of the
    /// given context on that date with <see cref="EvaluationContext.EnforcesTodaysHistoricFixings"/> true, QuantLib's
    /// <c>SavedSettings</c> and global settings (SMD9).
    /// </para>
    /// <para>
    /// The curve is bootstrapped afresh on each date (S5R1, a documented divergence): QuantLib's single curve restarts
    /// each date's bootstrap from the last date's solution, so its forwards differ within the curve's accuracy.
    /// QuantLib's <c>catch (std::exception&amp;)</c> catches the <see cref="NanoQLException"/> and
    /// <see cref="ArgumentException"/> QuantLib's errors map to. A step of no length, on which QuantLib's loop over the
    /// dates never ends, is refused, as is a forward index of no tenor where the first forward is within the horizon,
    /// on which its loop over the fixing periods never ends.
    /// </para>
    /// </remarks>
    /// <typeparam name="TTraits">The curve's bootstrap traits.</typeparam>
    /// <typeparam name="TInterpolator">The curve's interpolator.</typeparam>
    /// <param name="statistics">The statistics filled.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="step">The step between dates, positive.</param>
    /// <param name="fwdIndex">The index whose forwards are analysed: its tenor, day counter and fixing
    /// calendar.</param>
    /// <param name="initialGap">The period to the first forward's fixing.</param>
    /// <param name="horizon">The period to the last forward's fixing, at most.</param>
    /// <param name="iborIndexes">The deposit indexes the curve is bootstrapped on.</param>
    /// <param name="swapIndexes">The swap indexes the curve is bootstrapped on.</param>
    /// <param name="yieldCurveDayCounter">The curve's day counter.</param>
    /// <param name="context">The evaluation context, holding the fixings.</param>
    /// <param name="yieldCurveAccuracy">The bootstrap's accuracy.</param>
    /// <param name="i">The interpolator.</param>
    /// <returns>The dates skipped and failed, their messages, and the fixing periods.</returns>
    /// <exception cref="ArgumentException">
    /// The step isn't positive, or the index's tenor isn't where the initial gap is within the horizon.
    /// </exception>
    public static HistoricalForwardRatesAnalysisResults HistoricalForwardRatesAnalysis<TTraits, TInterpolator>(
        SequenceStatistics statistics,
        Date startDate,
        Date endDate,
        Period step,
        InterestRateIndex fwdIndex,
        Period initialGap,
        Period horizon,
        ReadOnlySpan<IborIndex> iborIndexes,
        ReadOnlySpan<SwapIndex> swapIndexes,
        DayCounter yieldCurveDayCounter,
        EvaluationContext context,
        double yieldCurveAccuracy = 1.0e-12,
        TInterpolator i = default)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(fwdIndex);
        ArgumentNullException.ThrowIfNull(yieldCurveDayCounter);
        ArgumentNullException.ThrowIfNull(context);
        foreach (IborIndex index in iborIndexes)
        {
            ArgumentNullException.ThrowIfNull(index, nameof(iborIndexes));
        }

        foreach (SwapIndex index in swapIndexes)
        {
            ArgumentNullException.ThrowIfNull(index, nameof(swapIndexes));
        }

        if (step.Length <= 0)
        {
            throw new ArgumentException("the step must be positive", nameof(step));
        }

        if (fwdIndex.Tenor.Length <= 0 && initialGap <= horizon)
        {
            throw new ArgumentException("the index's tenor must be positive", nameof(fwdIndex));
        }

        statistics.Reset();
        var skippedDates = new List<Date>();
        var skippedDatesErrorMessage = new List<string>();
        var failedDates = new List<Date>();
        var failedDatesErrorMessage = new List<string>();
        var fixingPeriods = new List<Period>();

        double[] iborQuotes = new double[iborIndexes.Length];
        double[] swapQuotes = new double[swapIndexes.Length];

        // Set up the forward rates time grid
        Period indexTenor = fwdIndex.Tenor;
        Period fixingPeriod = initialGap;
        while (fixingPeriod <= horizon)
        {
            fixingPeriods.Add(fixingPeriod);
            fixingPeriod += indexTenor;
        }

        int nRates = fixingPeriods.Count;
        statistics.Reset(nRates);
        double[] fwdRates = new double[nRates];
        double[] prevFwdRates = new double[nRates];
        double[] fwdRatesDiff = new double[nRates];
        DayCounter indexDayCounter = fwdIndex.DayCounter;
        Calendar cal = fwdIndex.FixingCalendar;

        var bootstrap = new IterativeBootstrap(yieldCurveAccuracy);

        // start with a valid business date
        Date currentDate = cal.Advance(startDate, 1, TimeUnit.Days, BusinessDayConvention.Following);
        bool isFirst = true;

        // Loop over the historical dataset
        for (; currentDate <= endDate; currentDate = cal.Advance(currentDate, step, BusinessDayConvention.Following))
        {
            // move the evaluation date to the current date
            EvaluationContext dayContext = context with
            {
                EvaluationDate = currentDate,
                EnforcesTodaysHistoricFixings = true,
            };

            try
            {
                // update the quotes...
                for (int k = 0; k < iborIndexes.Length; ++k)
                {
                    iborQuotes[k] = iborIndexes[k].Fixing(currentDate, dayContext, false);
                }

                for (int k = 0; k < swapIndexes.Length; ++k)
                {
                    swapQuotes[k] = swapIndexes[k].Fixing(currentDate, dayContext, false);
                }
            }
            catch (Exception e) when (e is NanoQLException or ArgumentException)
            {
                skippedDates.Add(currentDate);
                skippedDatesErrorMessage.Add(e.Message);
                continue;
            }

            try
            {
                // Bootstrap the yield curve at the current date
                var rateHelpers = new List<BootstrapHelper<YieldTermStructure>>();
                for (int k = 0; k < iborIndexes.Length; ++k)
                {
                    IborIndex ibor = iborIndexes[k];
                    rateHelpers.Add(new DepositRateHelper(
                        iborQuotes[k],
                        ibor.Tenor,
                        ibor.FixingDays,
                        ibor.FixingCalendar,
                        ibor.BusinessDayConvention,
                        ibor.EndOfMonth,
                        ibor.DayCounter,
                        dayContext));
                }

                for (int k = 0; k < swapIndexes.Length; ++k)
                {
                    SwapIndex swap = swapIndexes[k];
                    rateHelpers.Add(new SwapRateHelper(
                        swapQuotes[k],
                        swap.Tenor,
                        swap.FixingCalendar,
                        swap.FixedLegTenor.Frequency(),
                        swap.FixedLegConvention,
                        swap.DayCounter,
                        swap.IborIndex,
                        dayContext));
                }

                PiecewiseYieldCurve<TTraits, TInterpolator, IterativeBootstrap> yc =
                    PiecewiseYieldCurve<TTraits, TInterpolator, IterativeBootstrap>.Create(
                        0, cal, rateHelpers, yieldCurveDayCounter, dayContext, i: i, bootstrap: bootstrap);

                for (int k = 0; k < nRates; ++k)
                {
                    // Time-to-go forwards
                    Date d = currentDate + fixingPeriods[k];
                    fwdRates[k] = yc.ForwardRate(d, indexTenor, indexDayCounter, Compounding.Simple).Rate;
                }
            }
            catch (Exception e) when (e is NanoQLException or ArgumentException)
            {
                failedDates.Add(currentDate);
                failedDatesErrorMessage.Add(e.Message);
                continue;
            }

            // From 2nd step onwards, calculate forward rate relative differences
            if (!isFirst)
            {
                for (int k = 0; k < nRates; ++k)
                {
                    fwdRatesDiff[k] = (fwdRates[k] / prevFwdRates[k]) - 1.0;
                }

                // add observation
                statistics.Add(fwdRatesDiff);
            }
            else
            {
                isFirst = false;
            }

            // Store last calculated forward rates
            (prevFwdRates, fwdRates) = (fwdRates, prevFwdRates);
        }

        return new HistoricalForwardRatesAnalysisResults(
            [.. skippedDates], [.. skippedDatesErrorMessage], [.. failedDates], [.. failedDatesErrorMessage],
            [.. fixingPeriods]);
    }
}
