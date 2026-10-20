// SPDX-FileCopyrightText: Copyright (C) 2008 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/interpolatedhazardratecurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A default-probability term structure interpolating hazard rates.</summary>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <remarks>
/// <para>
/// The first date is the reference date. Past the last node the hazard rate stays at its last value. The curve
/// implements <see cref="IInterpolatedCurve{TTraits, TInterpolator}"/> over <see cref="HazardRate"/>, as the piecewise
/// hazard-rate curve does, so the ISDA engine's curve test accepts it as QuantLib's cast does.
/// </para>
/// <para>
/// QuantLib's protected constructors without nodes, through which its piecewise curve derives, aren't ported: the
/// piecewise curve implements the interface itself. QuantLib reads the first date before it checks how many there are,
/// so with no date it throws <c>std::out_of_range</c>; here an empty list is refused at that point with QuantLib's own
/// "not enough input dates given", the playbook's convention for such an exception. An extension point, for its named
/// class.
/// </para>
/// </remarks>
public class InterpolatedHazardRateCurve<TInterpolator>
    : HazardRateStructure, IInterpolatedCurve<HazardRate, TInterpolator>
    where TInterpolator : struct, IInterpolator
{
    private readonly ImmutableArray<Date> dates;
    private readonly InterpolatedCurve<TInterpolator> curve;

    /// <summary>Initializes a new instance of the <see cref="InterpolatedHazardRateCurve{TInterpolator}"/>
    /// class.</summary>
    /// <param name="dates">The nodes' dates, sorted, the first one the reference date.</param>
    /// <param name="hazardRates">The hazard rates at the nodes, one per date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, if any.</param>
    /// <param name="jumps">The survival-probability jumps; none when <see langword="null"/>.</param>
    /// <param name="jumpDates">The jumps' dates; at the turns of the years when <see langword="null"/> or
    /// empty.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dates"/>, <paramref name="hazardRates"/> or
    /// <paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer dates than the interpolator requires, a date is
    /// <c>default(Date)</c>, the rates aren't one per date, a rate is negative, the dates aren't sorted or two give the
    /// same time, the interpolation refuses the points, or the jumps' dates don't match the jumps.</exception>
    public InterpolatedHazardRateCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> hazardRates,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator interpolator = default)
        : this(
            InterpolatedCurve<TInterpolator>.Inputs(dates, hazardRates, dayCounter, nameof(hazardRates)),
            calendar,
            jumps,
            jumpDates,
            interpolator)
    {
    }

    private InterpolatedHazardRateCurve(
        (ImmutableArray<Date> Dates, ImmutableArray<double> Data, DayCounter DayCounter) inputs,
        Calendar? calendar,
        IEnumerable<double>? jumps,
        IEnumerable<Date>? jumpDates,
        TInterpolator interpolator)
        : base(inputs.Dates[0], calendar, inputs.DayCounter, null, jumps, jumpDates)
    {
        // initialize
        dates = inputs.Dates;
        ImmutableArray<double> data = inputs.Data;
        if (!(dates.Length >= TInterpolator.RequiredPoints))
        {
            throw new ArgumentException("not enough input dates given", nameof(dates));
        }

        if (!(data.Length == dates.Length))
        {
            throw new ArgumentException("dates/data count mismatch", "hazardRates");
        }

        foreach (double rate in data)
        {
            if (!(rate >= 0.0))
            {
                throw new ArgumentException("negative hazard rate", "hazardRates");
            }
        }

        curve = InterpolatedCurve<TInterpolator>.Create(dates.AsSpan(), data.AsSpan(), dates[0], inputs.DayCounter, interpolator);
    }

    /// <inheritdoc/>
    public sealed override Date MaxDate => TermStructures.HazardRate.MaxDate(dates.AsSpan());

    /// <inheritdoc/>
    public ImmutableArray<double> Times() => curve.Times();

    /// <inheritdoc/>
    public ImmutableArray<Date> Dates() => dates;

    /// <inheritdoc/>
    public ImmutableArray<double> Data() => curve.Data();

    /// <summary>The hazard rates at the nodes.</summary>
    /// <returns>The rates.</returns>
    public ImmutableArray<double> HazardRates() => curve.Data();

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
    protected sealed override double HazardRateImpl(double t) =>
        TermStructures.HazardRate.HazardRateImpl(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t, this);

    /// <inheritdoc/>
    protected sealed override double SurvivalProbabilityImpl(double t) =>
        TermStructures.HazardRate.SurvivalProbabilityImpl(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);
}
