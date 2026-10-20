// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/interpolatedzeroinflationcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>An inflation term structure interpolating zero-coupon inflation rates.</summary>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <remarks>
/// <para>
/// The first date is the base date. A rate is the interpolation's value at the start of the date's inflation period,
/// extrapolated, with the seasonality's correction. The curve implements
/// <see cref="IInterpolatedCurve{TTraits, TInterpolator}"/> over <see cref="ZeroInflationTraits"/>, as the piecewise
/// zero-inflation curve does.
/// </para>
/// <para>
/// QuantLib's protected constructor without nodes, through which its piecewise curve derives, isn't ported: the
/// piecewise curve implements the interface itself. An extension point, for its named class.
/// </para>
/// <para>
/// QuantLib reads the first date before it checks how many there are, so with no date it throws
/// <c>std::out_of_range</c>; here an empty list is refused first with QuantLib's own "too few dates", a documented
/// divergence.
/// </para>
/// </remarks>
public class InterpolatedZeroInflationCurve<TInterpolator>
    : ZeroInflationTermStructure, IInterpolatedCurve<ZeroInflationTraits, TInterpolator>
    where TInterpolator : struct, IInterpolator
{
    private readonly ImmutableArray<Date> dates;
    private readonly InterpolatedCurve<TInterpolator> curve;

    /// <summary>Initializes a new instance of the <see cref="InterpolatedZeroInflationCurve{TInterpolator}"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dates">The nodes' dates, sorted, the first one the base date.</param>
    /// <param name="rates">The zero-coupon inflation rates at the nodes, one per date.</param>
    /// <param name="frequency">The frequency of the index the curve forecasts.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dates"/>, <paramref name="rates"/> or
    /// <paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer than two dates, a date is <c>default(Date)</c>, the rates
    /// aren't one per date, a rate after the first isn't above -100%, the dates aren't sorted or two give the same time,
    /// or the interpolation refuses the points.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the curve.</exception>
    public InterpolatedZeroInflationCurve(
        Date referenceDate,
        IEnumerable<Date> dates,
        IEnumerable<double> rates,
        Frequency frequency,
        DayCounter dayCounter,
        Seasonality? seasonality = null,
        TInterpolator interpolator = default)
        : this(referenceDate, Inputs(dates, rates), frequency, dayCounter, seasonality, interpolator)
    {
    }

    private InterpolatedZeroInflationCurve(
        Date referenceDate,
        (ImmutableArray<Date> Dates, ImmutableArray<double> Rates) inputs,
        Frequency frequency,
        DayCounter dayCounter,
        Seasonality? seasonality,
        TInterpolator interpolator)
        : base(referenceDate, inputs.Dates[0], frequency, dayCounter, seasonality)
    {
        dates = inputs.Dates;
        ImmutableArray<double> data = inputs.Rates;
        if (!(dates.Length > 1))
        {
            throw new ArgumentException($"too few dates: {dates.Length}", nameof(dates));
        }

        if (!(data.Length == dates.Length))
        {
            throw new ArgumentException(
                $"indices/dates count mismatch: {data.Length} vs {dates.Length}", "rates");
        }

        for (int i = 1; i < dates.Length; i++)
        {
            // must be greater than -1
            if (!(data[i] > -1.0))
            {
                throw new ArgumentException("zero inflation data < -100 %", "rates");
            }
        }

        double[] times = InterpolatedCurve<TInterpolator>.SetupTimes(dates.AsSpan(), referenceDate, dayCounter);
        curve = InterpolatedCurve<TInterpolator>.FromTimes(times, data.AsSpan(), interpolator);
    }

    /// <inheritdoc/>
    public sealed override Date MaxDate => ZeroInflationTraits.MaxDate(dates.AsSpan(), curve.MaxDate);

    /// <inheritdoc/>
    public ImmutableArray<Date> Dates() => dates;

    /// <inheritdoc/>
    public ImmutableArray<double> Times() => curve.Times();

    /// <inheritdoc/>
    public ImmutableArray<double> Data() => curve.Data();

    /// <summary>The zero-coupon inflation rates at the nodes.</summary>
    /// <returns>The rates.</returns>
    public ImmutableArray<double> Rates() => curve.Data();

    /// <inheritdoc/>
    public ImmutableArray<KeyValuePair<Date, double>> Nodes()
    {
        ImmutableArray<double> data = curve.Data();
        var nodes = ImmutableArray.CreateBuilder<KeyValuePair<Date, double>>(dates.Length);
        for (int i = 0; i < dates.Length; ++i)
        {
            nodes.Add(new KeyValuePair<Date, double>(dates[i], data[i]));
        }

        return nodes.MoveToImmutable();
    }

    /// <inheritdoc/>
    protected sealed override double ZeroRateImpl(double t) => ZeroInflationTraits.ZeroRateImpl(curve.Interpolation, t);

    // The inputs, copied and checked before the base constructor reads the first date: an empty list is refused with
    // QuantLib's own text, where QuantLib's dates.at(0) throws std::out_of_range.
    private static (ImmutableArray<Date> Dates, ImmutableArray<double> Rates) Inputs(
        IEnumerable<Date> dates, IEnumerable<double> rates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(rates);
        ImmutableArray<Date> d = [.. dates];
        foreach (Date date in d)
        {
            Date.ThrowIfDefault(date, nameof(dates));
        }

        if (d.IsEmpty)
        {
            throw new ArgumentException("too few dates: 0", nameof(dates));
        }

        return (d, [.. rates]);
    }
}
