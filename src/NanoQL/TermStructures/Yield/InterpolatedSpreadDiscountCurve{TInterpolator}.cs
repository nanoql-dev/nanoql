// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/spreaddiscountcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A yield curve over a base curve, its discount the base's times an interpolated spread discount factor.
/// </summary>
/// <typeparam name="TInterpolator">The interpolator of the spread discount factors.</typeparam>
/// <remarks>
/// <para>
/// QuantLib's curve derives from <c>RelativeDerivedYieldTermStructure&lt;&gt;</c> and from its interpolated curve; it
/// implements the template's members itself. Its reference date and day counter are the base's, read when it is built,
/// and so is its extrapolation flag; its calendar and settlement days are the base's, read when they are asked for. Its
/// first node is at the base's reference date with a discount of 1; past its last node the spread extrapolates with a
/// flat instantaneous forward rate, as <see cref="InterpolatedDiscountCurve{TInterpolator}"/> does, and its maximum
/// date is the earlier of its last node's and the base's. A base that changes is a new curve: QuantLib's curve follows
/// its handle. Unsealed for its named class, which adds constructors only. QuantLib's protected constructor, through
/// which its piecewise spread curve derives, isn't ported: that curve is
/// <see cref="PiecewiseSpreadYieldCurve{TInterpolator, TBootstrap}"/>.
/// </para>
/// <para>
/// QuantLib reads the base without extrapolation (<c>spreaddiscountcurve.hpp:157</c>), so asked to extrapolate past a
/// base that doesn't, the curve throws where the zero- and forward-spreaded, implied and composite curves extrapolate;
/// here the base is read with extrapolation on, as theirs are. A documented divergence.
/// </para>
/// </remarks>
public class InterpolatedSpreadDiscountCurve<TInterpolator> : YieldTermStructure, IInterpolatedCurve<Discount, TInterpolator>
    where TInterpolator : struct, IInterpolator
{
    private readonly YieldTermStructure baseCurve;
    private readonly ImmutableArray<Date> dates;
    private readonly InterpolatedCurve<TInterpolator> curve;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterpolatedSpreadDiscountCurve{TInterpolator}"/> class from its
    /// nodes; the first date is the base's reference date.
    /// </summary>
    /// <param name="baseCurve">The base curve.</param>
    /// <param name="dates">The nodes' dates, sorted, the first the base's reference date.</param>
    /// <param name="dfs">The spread discount factors at the nodes, the first one 1.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="baseCurve"/>, <paramref name="dates"/> or <paramref name="dfs"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// There are fewer dates than the interpolation needs, the counts of dates and discounts differ, the first discount
    /// isn't 1, a discount isn't positive, the first date isn't the base's reference date, the dates aren't sorted or two
    /// of them give the same time, or the interpolation refuses the nodes.
    /// </exception>
    /// <exception cref="NanoQLException">The interpolation refuses the nodes.</exception>
    public InterpolatedSpreadDiscountCurve(
        YieldTermStructure baseCurve,
        IEnumerable<Date> dates,
        IEnumerable<double> dfs,
        TInterpolator interpolator = default)
        : base(Base(baseCurve).ReferenceDate, null, baseCurve.DayCounter)
    {
        this.baseCurve = baseCurve;
        (ImmutableArray<Date> d, ImmutableArray<double> data, _) =
            InterpolatedCurve<TInterpolator>.Inputs(dates, dfs, baseCurve.DayCounter, nameof(dfs));
        if (!(d.Length >= TInterpolator.RequiredPoints))
        {
            throw new ArgumentException("not enough input dates given", nameof(dates));
        }

        if (!(data.Length == d.Length))
        {
            throw new ArgumentException("dates/data count mismatch", nameof(dfs));
        }

        if (!(data[0] == 1.0))
        {
            throw new ArgumentException(
                "the first discount must be == 1.0 to flag the corresponding date as reference date", nameof(dfs));
        }

        for (int i = 1; i < d.Length; ++i)
        {
            if (!(data[i] > 0.0))
            {
                throw new ArgumentException("negative discount", nameof(dfs));
            }
        }

        // updateInterpolation
        if (!(d[0] == ReferenceDate))
        {
            throw new ArgumentException("the first date should be the same as in the original curve", nameof(dates));
        }

        this.dates = d;
        curve = InterpolatedCurve<TInterpolator>.Create(d.AsSpan(), data.AsSpan(), d[0], DayCounter, interpolator);
        AllowsExtrapolation = baseCurve.AllowsExtrapolation;
    }

    /// <summary>Gets the base curve.</summary>
    public YieldTermStructure BaseCurve => baseCurve;

    /// <summary>Gets the base curve's calendar, read when asked for.</summary>
    public override Calendar? Calendar => baseCurve.Calendar;

    /// <summary>Gets the base curve's settlement days, read when asked for.</summary>
    public override int? SettlementDays => baseCurve.SettlementDays;

    /// <summary>Gets the earlier of the last node's date and the base curve's maximum date.</summary>
    public sealed override Date MaxDate
    {
        get
        {
            Date baseMax = baseCurve.MaxDate;
            Date maxDate = dates[^1];
            return maxDate < baseMax ? maxDate : baseMax;
        }
    }

    /// <inheritdoc/>
    public ImmutableArray<double> Times() => curve.Times();

    /// <inheritdoc/>
    public ImmutableArray<Date> Dates() => dates;

    /// <inheritdoc/>
    public ImmutableArray<double> Data() => curve.Data();

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

    /// <summary>The base's discount at a time, read with extrapolation on, times the spread discount there.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    protected sealed override double DiscountImpl(double t) =>
        baseCurve.Discount(t, true)
        * DiscountCurveFormula.DiscountImpl(curve.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);

    // The base, refused when null before the base constructor reads it.
    private static YieldTermStructure Base(YieldTermStructure baseCurve)
    {
        ArgumentNullException.ThrowIfNull(baseCurve);
        return baseCurve;
    }
}
