// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/indexes/inflationindex.hpp ql/indexes/inflationindex.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Indexes;

/// <summary>
/// A year-on-year inflation index: quoted, with fixings of its own, or defined as the ratio of a zero-inflation index's
/// fixings a year apart.
/// </summary>
/// <remarks>
/// <para>
/// The index holds its forecasting curve, <see langword="null"/> for QuantLib's empty handle; a forecast without one
/// throws QuantLib's "empty Handle cannot be dereferenced". <see cref="Clone"/> is QuantLib's <c>clone(h)</c>.
/// </para>
/// <para>
/// QuantLib's deprecated <c>interpolated()</c> and its member, which no QuantLib class sets, aren't ported (D5): the
/// index never interpolates, the coupons do. An extension point.
/// </para>
/// </remarks>
public class YoYInflationIndex : InflationIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YoYInflationIndex"/> class defined as a ratio: it stores no fixings
    /// of its own, its past fixings computed from the underlying index's. Its family name is the underlying's with the
    /// prefix "YYR_".
    /// </summary>
    /// <param name="underlyingIndex">The zero-inflation index.</param>
    /// <param name="ts">The forecasting curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="underlyingIndex"/> is null.</exception>
    public YoYInflationIndex(ZeroInflationIndex underlyingIndex, YoYInflationTermStructure? ts = null)
        : base(
            "YYR_" + (underlyingIndex ?? throw new ArgumentNullException(nameof(underlyingIndex))).FamilyName,
            underlyingIndex.Region,
            underlyingIndex.Revised,
            underlyingIndex.Frequency,
            underlyingIndex.AvailabilityLag,
            underlyingIndex.Currency)
    {
        Ratio = true;
        UnderlyingIndex = underlyingIndex;
        YoYInflationTermStructure = ts;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YoYInflationIndex"/> class for a quoted index, whose past fixings,
    /// the past year-on-year values, are stored through <see cref="IndexFixings"/>.
    /// </summary>
    /// <param name="familyName">The index's family name.</param>
    /// <param name="region">The region the index is published for.</param>
    /// <param name="revised">Whether the index's fixings are revised after publication.</param>
    /// <param name="frequency">The index's frequency.</param>
    /// <param name="availabilityLag">The lag after which a period's fixing is available.</param>
    /// <param name="currency">The index's currency.</param>
    /// <param name="ts">The forecasting curve, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="familyName"/>, <paramref name="region"/> or
    /// <paramref name="currency"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its type.</exception>
    public YoYInflationIndex(
        string familyName,
        Region region,
        bool revised,
        Frequency frequency,
        Period availabilityLag,
        Currency currency,
        YoYInflationTermStructure? ts = null)
        : base(familyName, region, revised, frequency, availabilityLag, currency)
    {
        Ratio = false;
        YoYInflationTermStructure = ts;
    }

    /// <summary>Gets a value indicating whether the index is defined as a ratio of its underlying's fixings.</summary>
    public bool Ratio { get; }

    /// <summary>Gets the underlying zero-inflation index of a ratio index, or <see langword="null"/> for a quoted
    /// one.</summary>
    public ZeroInflationIndex? UnderlyingIndex { get; }

    /// <summary>Gets the forecasting curve, or <see langword="null"/> for none.</summary>
    public YoYInflationTermStructure? YoYInflationTermStructure { get; }

    /// <summary>An index of the same kind and conventions, forecasting through another curve.</summary>
    /// <param name="h">The forecasting curve, or <see langword="null"/> for none.</param>
    /// <returns>A plain year-on-year index: a ratio of the same underlying, or a quoted index.</returns>
    public YoYInflationIndex Clone(YoYInflationTermStructure? h) =>
        UnderlyingIndex is { } underlying
            ? new YoYInflationIndex(underlying, h)
            : new YoYInflationIndex(FamilyName, Region, Revised, Frequency, AvailabilityLag, Currency, h);

    /// <summary>The fixing for a date's inflation period: forecast when it needs a forecast, else the past one.</summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="forecastTodaysFixing">Ignored, as QuantLib ignores it.</param>
    /// <returns>The fixing.</returns>
    /// <exception cref="NanoQLException">A forecast is needed and the index has no curve, or a past fixing is
    /// missing.</exception>
    public override double Fixing(Date fixingDate, EvaluationContext context, bool forecastTodaysFixing = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        if (NeedsForecast(fixingDate, context))
        {
            return ForecastFixing(fixingDate);
        }

        return PastFixing(fixingDate, context)!.Value;
    }

    /// <summary>
    /// The past fixing for a date's inflation period: for a ratio index, the underlying's fixing over the one a year
    /// earlier, less one; for a quoted index, the fixing stored on the period's first day.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context, whose fixings are read.</param>
    /// <returns>The fixing; never <see langword="null"/>, since QuantLib's refuses a missing one.</returns>
    /// <exception cref="NanoQLException">A fixing it reads is missing.</exception>
    public override double? PastFixing(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        if (UnderlyingIndex is { } underlying)
        {
            double pastFixing = CPI.LaggedFixing(
                underlying, fixingDate, new Period(0, TimeUnit.Months), CPIInterpolationType.Flat, context);
            double previousFixing = CPI.LaggedFixing(
                underlying,
                fixingDate - new Period(1, TimeUnit.Years),
                new Period(0, TimeUnit.Months),
                CPIInterpolationType.Flat,
                context);
            return (pastFixing / previousFixing) - 1.0;
        }

        // NOT ratio
        (Date periodStart, _) = InflationTermStructureFunctions.InflationPeriod(fixingDate, Frequency);
        if (!TimeSeries(context).TryGetValue(periodStart, out double yy0))
        {
            throw new NanoQLException($"Missing {Name} fixing for {periodStart}");
        }

        return yy0;
    }

    /// <summary>
    /// The first day of the inflation period of the last fixing stored: the underlying's, for a ratio index.
    /// </summary>
    /// <param name="context">The evaluation context, whose fixings are read.</param>
    /// <returns>The date the last fixing is attributed to.</returns>
    /// <exception cref="NanoQLException">No fixing is stored.</exception>
    public Date LastFixingDate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (UnderlyingIndex is { } underlying)
        {
            return underlying.LastFixingDate(context);
        }

        TimeSeries<double> fixings = TimeSeries(context);
        if (fixings.Empty)
        {
            throw new NanoQLException($"no fixings stored for {Name}");
        }

        // attribute fixing to first day of the underlying period
        return InflationTermStructureFunctions.InflationPeriod(fixings.LastDate(), Frequency).Start;
    }

    /// <summary>
    /// Whether the fixing for a date's inflation period needs a forecast: the underlying's answer for the period's
    /// first day, for a ratio index; otherwise as <see cref="ZeroInflationIndex.NeedsForecast"/> decides it.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="context">The evaluation context, whose evaluation date and fixings are read.</param>
    /// <returns>Whether the fixing is forecast.</returns>
    public bool NeedsForecast(Date fixingDate, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(fixingDate, nameof(fixingDate));
        Date today = context.EvaluationDate;
        (Date latestNeededDate, _) = InflationTermStructureFunctions.InflationPeriod(fixingDate, Frequency);

        if (UnderlyingIndex is { } underlying)
        {
            return underlying.NeedsForecast(latestNeededDate, context);
        }

        (Date latestStart, Date latestEnd) =
            InflationTermStructureFunctions.InflationPeriod(today - AvailabilityLag, Frequency);
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

    private double ForecastFixing(Date fixingDate)
    {
        // if the value is not interpolated use the starting value
        // by internal convention this will be consistent
        (Date d, _) = InflationTermStructureFunctions.InflationPeriod(fixingDate, Frequency);
        YoYInflationTermStructure yoyInflation =
            YoYInflationTermStructure ?? throw new NanoQLException("empty Handle cannot be dereferenced");
        return yoyInflation.YoyRate(d);
    }
}
