// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflationindex.hpp ql/indexes/inflationindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>A zero-inflation (price) index, such as CPI, RPI or HICP, forecasting through a zero-inflation curve.</summary>
/// <remarks>
/// <para>
/// The index holds its forecasting curve, <see langword="null"/> for QuantLib's empty handle; a forecast without one
/// throws QuantLib's "empty Handle cannot be dereferenced". <see cref="Clone"/> is QuantLib's <c>clone(h)</c>, which
/// returns a plain <see cref="ZeroInflationIndex"/> with the same conventions.
/// </para>
/// <para>
/// Zero-index fixings are never interpolated: every read keys on the first day of the date's inflation period. Reading
/// never writes, where QuantLib's <c>hasHistoricalFixing</c> on a missing date inserts a null entry that
/// <see cref="LastFixingDate"/> then reads, a documented divergence. An extension point.
/// </para>
/// </remarks>
public class ZeroInflationIndex : InflationIndex
{
    /// <summary>Initializes a new instance of the <see cref="ZeroInflationIndex"/> class.</summary>
    /// <param name="familyName">The index's family name, such as "RPI".</param>
    /// <param name="region">The region the index is published for.</param>
    /// <param name="revised">Whether the index's fixings are revised after publication.</param>
    /// <param name="frequency">The index's frequency.</param>
    /// <param name="availabilityLag">The lag after which a period's fixing is available.</param>
    /// <param name="currency">The index's currency.</param>
    /// <param name="ts">The forecasting curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="familyName"/>, <paramref name="region"/> or
    /// <paramref name="currency"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its type.</exception>
    public ZeroInflationIndex(
        string familyName,
        Region region,
        bool revised,
        Frequency frequency,
        Period availabilityLag,
        Currency currency,
        ZeroInflationTermStructure? ts = null)
        : this(familyName, region, revised, frequency, availabilityLag, currency, ts, ConventionProvider.Current)
    {
    }

    // An index keeping a given provider: a preset's, read once, or a clone's source's.
    private protected ZeroInflationIndex(
        string familyName,
        Region region,
        bool revised,
        Frequency frequency,
        Period availabilityLag,
        Currency currency,
        ZeroInflationTermStructure? ts,
        ConventionProvider provider)
        : base(familyName, region, revised, frequency, availabilityLag, currency, provider)
    {
        ZeroInflationTermStructure = ts;
    }

    /// <summary>Gets the forecasting curve, or <see langword="null"/> for none.</summary>
    public ZeroInflationTermStructure? ZeroInflationTermStructure { get; }

    /// <summary>A plain zero-inflation index with this index's conventions, forecasting through another curve.</summary>
    /// <param name="h">The forecasting curve, or <see langword="null"/> for none.</param>
    /// <returns>The index.</returns>
    public ZeroInflationIndex Clone(ZeroInflationTermStructure? h) =>
        new(FamilyName, Region, Revised, Frequency, AvailabilityLag, Currency, h, Provider);

    /// <summary>
    /// The fixing for a date's inflation period: the stored one when the period can't need a forecast, else the
    /// forecast through the curve.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="forecastTodaysFixing">Ignored, as QuantLib ignores it.</param>
    /// <returns>The fixing.</returns>
    /// <exception cref="NanoQLException">The period's fixing should be stored and isn't; or a forecast is needed and
    /// the index has no curve, or the fixing at the curve's base date isn't available.</exception>
    public override double Fixing(Date fixingDate, EvaluationContext context, bool forecastTodaysFixing = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        if (!NeedsForecast(fixingDate, context))
        {
            double? i1 = PastFixing(fixingDate, context);
            return i1 ?? throw new NanoQLException(
                $"Missing {Name} fixing for {InflationTermStructureFunctions.InflationPeriod(fixingDate, Frequency).Start}");
        }

        return ForecastFixing(fixingDate, context);
    }

    /// <inheritdoc/>
    public override double? PastFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        (Date start, _) = InflationTermStructureFunctions.InflationPeriod(fixingDate, Frequency);
        return TimeSeries(context).TryGetValue(start, out double fixing) ? fixing : null;
    }

    /// <summary>The first day of the inflation period of the last fixing stored.</summary>
    /// <param name="context">The evaluation context, whose fixings are read.</param>
    /// <returns>The date the last fixing is attributed to.</returns>
    /// <exception cref="NanoQLException">No fixing is stored for the index.</exception>
    public Date LastFixingDate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        TimeSeries<double> fixings = TimeSeries(context);
        if (fixings.Empty)
        {
            throw new NanoQLException($"no fixings stored for {Name}");
        }

        // attribute fixing to first day of the underlying period
        return InflationTermStructureFunctions.InflationPeriod(fixings.LastDate(), Frequency).Start;
    }

    /// <summary>
    /// Whether the fixing for a date's inflation period needs a forecast: not when the period is before the latest one
    /// whose fixing can be available, given the evaluation date and the availability lag; always when it is after it;
    /// and in that latest period, when its fixing isn't stored yet.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context, whose evaluation date and fixings are read.</param>
    /// <returns>Whether the fixing is forecast.</returns>
    public bool NeedsForecast(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        Date today = context.EvaluationDate;
        (Date latestStart, Date latestEnd) =
            InflationTermStructureFunctions.InflationPeriod(today - AvailabilityLag, Frequency);

        // Zero-index fixings are always non-interpolated.
        (Date latestNeededDate, _) = InflationTermStructureFunctions.InflationPeriod(fixingDate, Frequency);

        if (latestNeededDate < latestStart)
        {
            // the fixing date is well before the availability lag, so
            // we know that fixings must be provided.
            return false;
        }

        if (latestNeededDate > latestEnd)
        {
            // the fixing can't be available yet
            return true;
        }

        // we're not sure, but the fixing might be there so we check.
        return !TimeSeries(context).TryGetValue(latestNeededDate, out _);
    }

    private double ForecastFixing(Date fixingDate, EvaluationContext context)
    {
        // the term structure is relative to the fixing value at the base date.
        ZeroInflationTermStructure zeroInflation =
            ZeroInflationTermStructure ?? throw new NanoQLException("empty Handle cannot be dereferenced");
        Date baseDate = zeroInflation.BaseDate;
        if (NeedsForecast(baseDate, context))
        {
            throw new NanoQLException($"{Name} index fixing at base date {baseDate} is not available");
        }

        double baseFixing = Fixing(baseDate, context);
        (Date firstDateInPeriod, _) = InflationTermStructureFunctions.InflationPeriod(fixingDate, Frequency);
        double z1 = zeroInflation.ZeroRate(firstDateInPeriod, false);
        double t1 = InflationTermStructureFunctions.InflationYearFraction(
            Frequency, false, zeroInflation.DayCounter, baseDate, firstDateInPeriod);

        // During bootstrapping, extrapolated rates can temporarily go below -1.
        // Guard against pow of a negative base with non-integer exponent.
        if (z1 <= -1.0)
        {
            return 0.0;
        }

        return baseFixing * Math.Pow(1.0 + z1, t1);
    }
}
