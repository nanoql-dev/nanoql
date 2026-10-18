// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Decillion Pty(Ltd)
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/discountcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A yield term structure based on the interpolation of discount factors.</summary>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <remarks>
/// Past its last node the curve extrapolates with a flat instantaneous forward rate. Its formula and maximum date are
/// the <see cref="Discount"/> traits', which a <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}"/>
/// on those traits shares. Unsealed for its named classes, which add constructors only. QuantLib's protected
/// constructors, through which its piecewise curve derives, aren't ported: no class derives through them here.
/// </remarks>
public class InterpolatedDiscountCurve<TInterpolator> : YieldTermStructure, IInterpolatedCurve<Discount, TInterpolator>
    where TInterpolator : struct, IInterpolator
{
    private readonly ImmutableArray<Date> dates;
    private readonly InterpolatedCurve<TInterpolator> curve;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterpolatedDiscountCurve{TInterpolator}"/> class from its nodes;
    /// the first date is the reference date.
    /// </summary>
    /// <param name="dates">The nodes' dates, sorted.</param>
    /// <param name="dfs">The discount factors at the nodes, the first one 1.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="calendar">The calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dates"/>, <paramref name="dfs"/> or <paramref name="dayCounter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// There are fewer dates than the interpolation needs, the counts of dates and discounts differ, the first discount
    /// isn't 1, a discount isn't positive, the dates aren't sorted or two of them give the same time, the jumps and
    /// their dates differ in number, or the interpolation refuses the nodes.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the nodes.</exception>
    public InterpolatedDiscountCurve(
        IEnumerable<Date> dates,
        IEnumerable<double> dfs,
        DayCounter dayCounter,
        Calendar? calendar = null,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator interpolator = default)
        : this(InterpolatedCurve<TInterpolator>.Inputs(dates, dfs, dayCounter, nameof(dfs)), calendar, jumps, jumpDates, interpolator)
    {
    }

    private InterpolatedDiscountCurve(
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
            throw new ArgumentException("dates/data count mismatch", "dfs");
        }

        if (!(data[0] == 1.0))
        {
            throw new ArgumentException(
                "the first discount must be == 1.0 to flag the corresponding date as reference date", "dfs");
        }

        for (int i = 1; i < dates.Length; ++i)
        {
            if (!(data[i] > 0.0))
            {
                throw new ArgumentException("negative discount", "dfs");
            }
        }

        curve = InterpolatedCurve<TInterpolator>.Create(dates.AsSpan(), data.AsSpan(), dates[0], inputs.DayCounter, interpolator);
    }

    /// <inheritdoc/>
    public sealed override Date MaxDate => DiscountCurveFormula.MaxDate(dates.AsSpan(), curve.MaxDate);

    /// <inheritdoc/>
    public ImmutableArray<double> Times() => curve.Times();

    /// <inheritdoc/>
    public ImmutableArray<Date> Dates() => dates;

    /// <inheritdoc/>
    public ImmutableArray<double> Data() => curve.Data();

    /// <summary>The discount factors at the curve's nodes, its data.</summary>
    /// <returns>The discount factors.</returns>
    public ImmutableArray<double> Discounts() => curve.Data();

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
    protected sealed override double DiscountImpl(double t) =>
        DiscountCurveFormula.DiscountImpl(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);
}
