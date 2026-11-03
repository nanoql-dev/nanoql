// SPDX-FileCopyrightText: Copyright (C) 2007, 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflationtermstructure.hpp ql/termstructures/inflationtermstructure.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>The base class of inflation term structures: a base date, a frequency and an optional seasonality.</summary>
/// <remarks>
/// <para>
/// An extension point. Every built structure has a fixed reference date: QuantLib's constructor that takes neither a
/// reference date nor settlement days is the reference-date form, and the settlement-days form takes the reference date
/// its concrete class's <c>Create</c> computed from the evaluation context.
/// </para>
/// <para>
/// <see cref="BaseDate"/> and <see cref="Frequency"/> aren't virtual, where QuantLib's are: the constructor's
/// seasonality check reads them, and C# would run a derived override before the derived constructor. A derived curve
/// passes its base date in; QuantLib's piecewise zero-inflation curve, whose base date is computed lazily, computes it
/// at construction (the playbook's constructor-read row).
/// </para>
/// <para>
/// QuantLib's <c>setSeasonality</c> changes a built curve; here
/// <see cref="InflationTermStructureExtensions.SetSeasonality"/> returns a copy of the receiver's type through
/// <see cref="WithSeasonality"/>, which a bootstrapped curve overrides to bootstrap its copy again.
/// </para>
/// </remarks>
public abstract class InflationTermStructure : TermStructure
{
    private readonly double? baseRate;

    // Written at construction and on a fresh copy (WithSeasonality, ResetBase), never after.
    private Seasonality? seasonality;

    /// <summary>
    /// Initializes a new instance of the <see cref="InflationTermStructure"/> class with a fixed reference date.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="baseDate">The base date: the last date for which there is information.</param>
    /// <param name="frequency">The frequency of the index the structure forecasts.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <param name="baseRate">The rate at the base date, or <see langword="null"/>, QuantLib's <c>Null&lt;Rate&gt;()</c>,
    /// for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="baseDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the structure.</exception>
    protected InflationTermStructure(
        Date referenceDate,
        Date baseDate,
        Frequency frequency,
        DayCounter dayCounter,
        Seasonality? seasonality = null,
        double? baseRate = null)
        : this(referenceDate, null, null, baseDate, frequency, dayCounter, seasonality, baseRate)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InflationTermStructure"/> class whose reference date is the given
    /// number of business days after the evaluation date: QuantLib's settlement-days form, given the reference date
    /// the concrete class computed from the context.
    /// </summary>
    /// <param name="referenceDate">The reference date, computed from the evaluation date.</param>
    /// <param name="settlementDays">The business days the reference date was computed with.</param>
    /// <param name="calendar">The calendar the business days were counted on.</param>
    /// <param name="baseDate">The base date: the last date for which there is information.</param>
    /// <param name="frequency">The frequency of the index the structure forecasts.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <param name="baseRate">The rate at the base date, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="calendar"/> or <paramref name="dayCounter"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="baseDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or
    /// <paramref name="frequency"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the structure.</exception>
    protected InflationTermStructure(
        Date referenceDate,
        int settlementDays,
        Calendar calendar,
        Date baseDate,
        Frequency frequency,
        DayCounter dayCounter,
        Seasonality? seasonality = null,
        double? baseRate = null)
        : this(
            referenceDate,
            calendar ?? throw new ArgumentNullException(nameof(calendar)),
            settlementDays,
            baseDate,
            frequency,
            dayCounter,
            seasonality,
            baseRate)
    {
    }

    private InflationTermStructure(
        Date referenceDate,
        Calendar? calendar,
        int? settlementDays,
        Date baseDate,
        Frequency frequency,
        DayCounter dayCounter,
        Seasonality? seasonality,
        double? baseRate)
        : base(referenceDate, calendar, dayCounter, settlementDays)
    {
        Date.ThrowIfDefault(baseDate, nameof(baseDate));
        EnumArguments.Check(frequency, nameof(frequency));
        BaseDate = baseDate;
        Frequency = frequency;
        this.baseRate = baseRate;
        this.seasonality = seasonality;
        CheckSeasonality();
    }

    /// <summary>Gets the frequency of the index the structure forecasts.</summary>
    public Frequency Frequency { get; }

    /// <summary>Gets the base date: the last date for which there is information, the structure's minimum date.</summary>
    public Date BaseDate { get; private set; }

    /// <summary>Gets the seasonality, or <see langword="null"/> for none.</summary>
    public Seasonality? Seasonality => seasonality;

    /// <summary>Gets a value indicating whether the structure has a seasonality.</summary>
    public bool HasSeasonality => seasonality is not null;

    /// <summary>The rate at the base date.</summary>
    /// <returns>The rate.</returns>
    /// <exception cref="NanoQLException">The structure has no base rate.</exception>
    public virtual double BaseRate() => baseRate ?? throw new NanoQLException("base rate not available");

    /// <summary>
    /// A copy of the structure with another seasonality, which <see cref="InflationTermStructureExtensions.SetSeasonality"/>
    /// returns: QuantLib's <c>setSeasonality</c>.
    /// </summary>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <param name="context">The evaluation context, which a structure that bootstraps its copy again reads.</param>
    /// <returns>The copy; the base copies the structure and sets the seasonality, checked against the copy.</returns>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the structure.</exception>
    /// <remarks>An extension point: a bootstrapped curve overrides it to build its copy afresh with the
    /// seasonality, under the context given.</remarks>
    protected internal virtual InflationTermStructure WithSeasonality(Seasonality? seasonality, EvaluationContext context)
    {
        var copy = (InflationTermStructure)MemberwiseClone();
        copy.seasonality = seasonality;
        copy.CheckSeasonality();
        return copy;
    }

    /// <summary>
    /// Sets a fresh copy's base date and seasonality, and checks the seasonality against the copy: the step of a
    /// bootstrapped curve's <see cref="WithSeasonality"/>, before it bootstraps the copy again.
    /// </summary>
    /// <param name="baseDate">The base date.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the structure.</exception>
    private protected void ResetBase(Date baseDate, Seasonality? seasonality)
    {
        BaseDate = baseDate;
        this.seasonality = seasonality;
        CheckSeasonality();
    }

    /// <summary>Checks that a date is within the structure's range, or that extrapolation is allowed.</summary>
    /// <param name="d">The date.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The date is before the base date, or past the maximum date without
    /// extrapolation.</exception>
    protected new void CheckRange(Date d, bool extrapolate)
    {
        if (!(d >= BaseDate))
        {
            throw new NanoQLException($"date ({d}) is before base date ({BaseDate})");
        }

        if (!(extrapolate || AllowsExtrapolation || d <= MaxDate))
        {
            throw new NanoQLException($"date ({d}) is past max curve date ({MaxDate})");
        }
    }

    /// <summary>Checks that a time is within the structure's range, or that extrapolation is allowed.</summary>
    /// <param name="t">The time.</param>
    /// <param name="extrapolate">Whether this call extrapolates.</param>
    /// <exception cref="NanoQLException">The time is before the base date's, or past the maximum time without
    /// extrapolation.</exception>
    protected new void CheckRange(double t, bool extrapolate)
    {
        if (!(t >= TimeFromReference(BaseDate)))
        {
            throw new NanoQLException($"time ({Show(t)}) is before base date");
        }

        if (!(extrapolate || AllowsExtrapolation || t <= MaxTime))
        {
            throw new NanoQLException($"time ({Show(t)}) is past max curve time ({Show(MaxTime)})");
        }
    }

    private static string Show(double x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void CheckSeasonality()
    {
        if (seasonality is not null && !seasonality.IsConsistent(this))
        {
            throw new NanoQLException("Seasonality inconsistent with inflation term structure");
        }
    }
}
