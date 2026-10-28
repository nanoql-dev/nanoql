// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/interpolateddefaultdensitycurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A default-probability term structure interpolating default densities.</summary>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <remarks>
/// <para>
/// The first date is the reference date. Past the last node the density stays at its last value; the survival
/// probability is one less the density's integral, floored at 0, and the hazard rate the base's. The curve implements
/// <see cref="IInterpolatedCurve{TTraits, TInterpolator}"/> over <see cref="DefaultDensity"/>, as the piecewise
/// default-density curve does.
/// </para>
/// <para>
/// QuantLib's protected constructors without nodes, through which its piecewise curve derives, aren't ported: the
/// piecewise curve implements the interface itself. QuantLib reads the first date before it checks how many there are,
/// so with no date it throws <c>std::out_of_range</c>; here an empty list is refused at that point with QuantLib's own
/// "not enough input dates given", the playbook's convention for such an exception. An extension point, for its named
/// class.
/// </para>
/// </remarks>
public class InterpolatedDefaultDensityCurve<TInterpolator>
    : DefaultDensityStructure, IInterpolatedCurve<DefaultDensity, TInterpolator>
    where TInterpolator : struct, IInterpolator
{
    private readonly ImmutableArray<Date> dates;
    private readonly InterpolatedCurve<TInterpolator> curve;

    /// <summary>Initializes a new instance of the <see cref="InterpolatedDefaultDensityCurve{TInterpolator}"/>
    /// class.</summary>
    /// <param name="dates">The nodes' dates, sorted, the first one the reference date.</param>
    /// <param name="densities">The default densities at the nodes, one per date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, if any.</param>
    /// <param name="jumps">The survival-probability jumps; none when <see langword="null"/>.</param>
    /// <param name="jumpDates">The jumps' dates; at the turns of the years when <see langword="null"/> or
    /// empty.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dates"/>, <paramref name="densities"/> or
    /// <paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentException">There are fewer dates than the interpolator requires, a date is
    /// <c>default(Date)</c>, the densities aren't one per date, a density is negative, the dates aren't sorted or two
    /// give the same time, the interpolation refuses the points, or the jumps' dates don't match the jumps.</exception>
    public InterpolatedDefaultDensityCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> densities,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator interpolator = default)
        : this(
            InterpolatedCurve<TInterpolator>.Inputs(dates, densities, dayCounter, nameof(densities)),
            calendar,
            jumps,
            jumpDates,
            interpolator)
    {
    }

    private InterpolatedDefaultDensityCurve(
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
            throw new ArgumentException("dates/data count mismatch", "densities");
        }

        foreach (double density in data)
        {
            if (!(density >= 0.0))
            {
                throw new ArgumentException("negative default density", "densities");
            }
        }

        curve = InterpolatedCurve<TInterpolator>.Create(dates.AsSpan(), data.AsSpan(), dates[0], inputs.DayCounter, interpolator);
    }

    /// <inheritdoc/>
    public sealed override Date MaxDate => TermStructures.DefaultDensity.MaxDate(dates.AsSpan());

    /// <inheritdoc/>
    public ImmutableArray<double> Times() => curve.Times();

    /// <inheritdoc/>
    public ImmutableArray<Date> Dates() => dates;

    /// <inheritdoc/>
    public ImmutableArray<double> Data() => curve.Data();

    /// <summary>The default densities at the nodes.</summary>
    /// <returns>The densities.</returns>
    public ImmutableArray<double> DefaultDensities() => curve.Data();

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
    protected sealed override double DefaultDensityImpl(double t) =>
        TermStructures.DefaultDensity.DefaultDensityImpl(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);

    /// <inheritdoc/>
    internal override double? HazardRateSurvival(double t) =>
        GetType() == typeof(InterpolatedDefaultDensityCurve<TInterpolator>) || GetType() == typeof(DefaultDensityCurve)
            ? DensityHazardRateSurvival(t)
            : null;

    /// <inheritdoc/>
    protected sealed override double SurvivalProbabilityImpl(double t) =>
        TermStructures.DefaultDensity.SurvivalProbabilityImpl(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);
}
