// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflationtermstructure.hpp ql/termstructures/inflationtermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>The base class of year-on-year inflation term structures.</summary>
/// <remarks>
/// An extension point. QuantLib's deprecated lagged <c>yoyRate</c> isn't ported (D5); <see cref="YoyRate(Date, bool)"/>
/// computes what QuantLib's non-lagged form computes through it with a zero lag.
/// </remarks>
public abstract class YoYInflationTermStructure : InflationTermStructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YoYInflationTermStructure"/> class with a fixed reference date.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="baseDate">The base date.</param>
    /// <param name="baseYoYRate">The year-on-year rate at the base date.</param>
    /// <param name="frequency">The frequency of the index the structure forecasts.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="baseDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the structure.</exception>
    protected YoYInflationTermStructure(
        Date referenceDate,
        Date baseDate,
        double baseYoYRate,
        Frequency frequency,
        DayCounter dayCounter,
        Seasonality? seasonality = null)
        : base(referenceDate, baseDate, frequency, dayCounter, seasonality, baseYoYRate)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YoYInflationTermStructure"/> class whose reference date was
    /// computed from settlement days and the evaluation date.
    /// </summary>
    /// <param name="referenceDate">The reference date, computed from the evaluation date.</param>
    /// <param name="settlementDays">The business days the reference date was computed with.</param>
    /// <param name="calendar">The calendar the business days were counted on.</param>
    /// <param name="baseDate">The base date.</param>
    /// <param name="baseYoYRate">The year-on-year rate at the base date.</param>
    /// <param name="frequency">The frequency of the index the structure forecasts.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="calendar"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="baseDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="frequency"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the structure.</exception>
    protected YoYInflationTermStructure(
        Date referenceDate,
        int settlementDays,
        Calendar calendar,
        Date baseDate,
        double baseYoYRate,
        Frequency frequency,
        DayCounter dayCounter,
        Seasonality? seasonality = null)
        : base(referenceDate, settlementDays, calendar, baseDate, frequency, dayCounter, seasonality, baseYoYRate)
    {
    }

    /// <summary>
    /// The year-on-year inflation rate for a date, with the seasonality's correction: QuantLib's
    /// <c>yoyRate(d, extrapolate)</c>. It isn't the year-on-year swap rate, which the instrument gives.
    /// </summary>
    /// <param name="d">The date; the rate is read at the start of its inflation period.</param>
    /// <param name="extrapolate">Whether to extrapolate past the maximum date.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">The period's start is before the base date, or past the maximum date without
    /// extrapolation.</exception>
    public double YoyRate(Date d, bool extrapolate = false)
    {
        Date.ThrowIfDefault(d, nameof(d));
        (Date start, _) = InflationTermStructureFunctions.InflationPeriod(d, Frequency);
        CheckRange(start, extrapolate);
        double t = TimeFromReference(start);
        double yoyRate = YoyRateImpl(t);
        if (Seasonality is { } seasonality)
        {
            yoyRate = seasonality.CorrectYoYRate(d, yoyRate, this);
        }

        return yoyRate;
    }

    /// <summary>
    /// The year-on-year inflation rate at a time, with no lag, period or seasonality applied: QuantLib's
    /// <c>yoyRate(t, extrapolate)</c>.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether to extrapolate past the maximum time.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">The time is before the base date's, or past the maximum time without
    /// extrapolation.</exception>
    public double YoyRate(double t, bool extrapolate = false)
    {
        CheckRange(t, extrapolate);
        return YoyRateImpl(t);
    }

    /// <summary>The year-on-year inflation rate at a time, which a derived structure computes.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The rate.</returns>
    protected abstract double YoyRateImpl(double t);
}
