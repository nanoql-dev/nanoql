// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/interpolatedcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The data of an interpolated term structure: the times of its nodes, its data at them, their interpolation and the
/// maximum date, which an interpolated curve holds and answers from.
/// </summary>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <remarks>
/// QuantLib's <c>InterpolatedCurve</c> is a protected base its curves inherit their data members from; nanoql's curves
/// hold one instead, so no is-a relation is lost (QuantLib's inheritance is protected). Immutable: it copies its inputs
/// and builds its interpolation once.
/// </remarks>
public sealed class InterpolatedCurve<TInterpolator>
    where TInterpolator : struct, IInterpolator
{
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> data;

    internal InterpolatedCurve(
        ImmutableArray<double> times,
        ImmutableArray<double> data,
        TInterpolator interpolator,
        Interpolation interpolation,
        Date? maxDate)
    {
        this.times = times;
        this.data = data;
        Interpolator = interpolator;
        Interpolation = interpolation;
        MaxDate = maxDate;
    }

    /// <summary>Gets the interpolator the curve was built with.</summary>
    public TInterpolator Interpolator { get; }

    /// <summary>
    /// Gets the interpolation of the data: the interpolator's, or a bootstrap's linear interpolation where the
    /// interpolator couldn't be built over the curve's nodes.
    /// </summary>
    public Interpolation Interpolation { get; }

    /// <summary>
    /// Gets the latest date a bootstrap's helpers needed, which may be past the last node, or <see langword="null"/>
    /// for a curve that wasn't bootstrapped: QuantLib's <c>maxDate_</c>.
    /// </summary>
    public Date? MaxDate { get; }

    /// <summary>
    /// The data of a curve given its nodes' dates: the times from the reference date, checked as QuantLib's
    /// <c>setupTimes</c> checks them, and the interpolation built over them: QuantLib's <c>setupInterpolation</c>.
    /// </summary>
    /// <param name="dates">The nodes' dates, sorted, each with its own time under the day counter.</param>
    /// <param name="data">The data at the nodes, one per date.</param>
    /// <param name="referenceDate">The date the times are measured from.</param>
    /// <param name="dayCounter">The day counter measuring the times.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <returns>The curve's data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// No date is given, a date is <c>default(Date)</c>, the dates aren't sorted, two of them give the same time, or the
    /// interpolation refuses the points.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the points.</exception>
    public static InterpolatedCurve<TInterpolator> Create(
        ReadOnlySpan<Date> dates,
        ReadOnlySpan<double> data,
        Date referenceDate,
        DayCounter dayCounter,
        TInterpolator interpolator = default)
    {
        ArgumentNullException.ThrowIfNull(dayCounter);
        Date.ThrowIfDefault(referenceDate, nameof(referenceDate));
        if (dates.IsEmpty)
        {
            throw new ArgumentException("not enough input dates given", nameof(dates));
        }

        foreach (Date d in dates)
        {
            Date.ThrowIfDefault(d, nameof(dates));
        }

        // setupTimes
        var times = new double[dates.Length];
        times[0] = dayCounter.YearFraction(referenceDate, dates[0]);
        for (int i = 1; i < dates.Length; i++)
        {
            if (!(dates[i] > dates[i - 1]))
            {
                throw new ArgumentException($"dates not sorted: {dates[i]} passed after {dates[i - 1]}", nameof(dates));
            }

            times[i] = dayCounter.YearFraction(referenceDate, dates[i]);
            if (ComparisonFunctions.Close(times[i], times[i - 1]))
            {
                throw new ArgumentException(
                    $"two passed dates ({dates[i - 1]} and {dates[i]}) correspond to the same time under this curve's "
                    + $"day count convention ({dayCounter.Name})",
                    nameof(dates));
            }
        }

        // setupInterpolation
        Interpolation interpolation = interpolator.Interpolate(times, data);
        return new InterpolatedCurve<TInterpolator>([.. times], [.. data], interpolator, interpolation, null);
    }

    // An interpolated curve's inputs, copied and checked before its base constructor runs: QuantLib's curves read
    // dates.at(0) for their reference date first, so an empty list fails before every other check, and the curve's own
    // check of too few dates gives the text.
    internal static (ImmutableArray<Date> Dates, ImmutableArray<double> Data, DayCounter DayCounter) Inputs(
        IEnumerable<Date> dates, IEnumerable<double> data, DayCounter dayCounter, string dataName)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(data, dataName);
        ArgumentNullException.ThrowIfNull(dayCounter);
        ImmutableArray<Date> d = [.. dates];
        foreach (Date date in d)
        {
            Date.ThrowIfDefault(date, nameof(dates));
        }

        if (d.IsEmpty)
        {
            throw new ArgumentException("not enough input dates given", nameof(dates));
        }

        return (d, [.. data], dayCounter);
    }

    /// <summary>The times of the curve's nodes, from the reference date.</summary>
    /// <returns>The times.</returns>
    public ImmutableArray<double> Times() => times;

    /// <summary>The curve's data at its nodes.</summary>
    /// <returns>The data.</returns>
    public ImmutableArray<double> Data() => data;
}
