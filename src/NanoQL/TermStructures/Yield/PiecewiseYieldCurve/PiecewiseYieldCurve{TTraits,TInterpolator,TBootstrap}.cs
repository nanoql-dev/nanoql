// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/piecewiseyieldcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A yield term structure bootstrapped on rate helpers: each helper's pillar marks the end of an interpolated segment,
/// solved so that the helper reprices its quote.
/// </summary>
/// <typeparam name="TTraits">The traits, which name the curve kind (<see cref="Discount"/>, <see cref="ZeroYield"/>, …).</typeparam>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <typeparam name="TBootstrap">The bootstrap (<see cref="IterativeBootstrap"/>, <see cref="GlobalBootstrap"/>).</typeparam>
/// <remarks>
/// <para>
/// The curve bootstraps in its constructor, as a <c>LazyObject</c>'s work is done at construction. While its bootstrap
/// runs, the curve answers from a working state (<see cref="BootstrapState{TTermStructure}"/>), and its helpers are
/// copies bound to the curve itself, as QuantLib binds them to <c>this</c>; when the bootstrap returns, the state is
/// frozen into the curve's nodes and dropped, and the bound copies are discarded. Nothing outside the constructor sees
/// the curve change. The helpers whose dates follow the evaluation date are re-dated for the context's (see
/// <see cref="BootstrapHelper{TTermStructure}"/>). A curve in a cycle of curves, each bootstrapped with the others, is
/// built by a <see cref="MultiCurve"/>.
/// </para>
/// <para>
/// QuantLib's curve derives from its traits' interpolated curve; this one is that curve through
/// <see cref="IInterpolatedCurve{TTraits, TInterpolator}"/>, with the traits' formula and maximum-date rule. The members
/// its bootstrap calls on the curve under construction (<see cref="DiscountImpl"/>, <see cref="MaxDate"/>,
/// <see cref="MaxTime"/>, <see cref="DayCounter"/>) are sealed. Unsealed for its named classes, which add constructors
/// only.
/// </para>
/// <para>
/// A spread curve (<see cref="PiecewiseSpreadYieldCurve{TInterpolator, TBootstrap}"/>) is bootstrapped on a base curve:
/// its nodes are the spread discount factors, its discount the base's times theirs and its maximum date the earlier of
/// its own and the base's, QuantLib's <c>InterpolatedSpreadDiscountCurve</c>; it allows extrapolation when the base does,
/// when it is built. The base is read with extrapolation on, as the other derived curves read their originals. Every
/// other curve has no base.
/// </para>
/// <para>
/// A curve is always built afresh: QuantLib's curve, recalculated after a quote or the evaluation date changed, starts
/// from its previous state and, when its reference date is fixed, keeps its first evaluation date's pillars. Both are
/// documented divergences.
/// </para>
/// </remarks>
public class PiecewiseYieldCurve<TTraits, TInterpolator, TBootstrap> : YieldTermStructure, IInterpolatedCurve<TTraits, TInterpolator>
    where TTraits : struct, IYieldTraits
    where TInterpolator : struct, IInterpolator
    where TBootstrap : struct, IBootstrap
{
    // QuantLib's accuracy_, the bootstrap's default.
    private const double DefaultAccuracy = 1.0e-12;

    // The base curve of a spread curve, null for every other curve.
    private readonly YieldTermStructure? baseCurve;

    // Non-null only while the constructor bootstraps the curve; the frozen nodes are written once, by Freeze.
    private BootstrapState<YieldTermStructure>? state;
    private ImmutableArray<Date> dates;
    private InterpolatedCurve<TInterpolator>? curve;

    /// <summary>
    /// Initializes a new instance of the <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}"/> class with
    /// a fixed reference date, and bootstraps it.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context the helpers are dated and priced in.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="instruments"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A helper is <see langword="null"/>, the reference date is <c>default(Date)</c>, or the jumps and their dates
    /// differ in number.
    /// </exception>
    /// <exception cref="NanoQLException">The bootstrap fails.</exception>
    public PiecewiseYieldCurve(
        Date referenceDate,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator i = default,
        TBootstrap bootstrap = default)
        : this(referenceDate, null, null, instruments, dayCounter, context, jumps, jumpDates, i, bootstrap)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}"/> class
    /// whose reference date was computed from settlement days, and bootstraps it: for <see cref="Create"/> and the named
    /// classes' factories.
    /// </summary>
    private protected PiecewiseYieldCurve(
        Date referenceDate,
        Calendar? calendar,
        int? settlementDays,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps,
        IEnumerable<Date>? jumpDates,
        TInterpolator i,
        TBootstrap bootstrap)
        : this(null, referenceDate, calendar, settlementDays, instruments, dayCounter, context, jumps, jumpDates, i, bootstrap)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}"/> class
    /// on a base curve, whose reference date, calendar, settlement days and day counter it takes, and bootstraps it:
    /// for <see cref="PiecewiseSpreadYieldCurve{TInterpolator, TBootstrap}"/>.
    /// </summary>
    private protected PiecewiseYieldCurve(
        YieldTermStructure baseCurve,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        EvaluationContext context,
        TInterpolator i,
        TBootstrap bootstrap)
        : this(
            null,
            Base(baseCurve).ReferenceDate,
            baseCurve.Calendar,
            baseCurve.SettlementDays,
            instruments,
            baseCurve.DayCounter,
            context,
            null,
            null,
            i,
            bootstrap,
            baseCurve)
    {
    }

    // The constructors' work, and QuantLib's multiCurveBootstrapContributor(): a curve a multicurve builds (a parent
    // given) isn't bootstrapped here. Its bootstrap's contributor joins the multicurve's joint bootstrap, which sets it to
    // valid, freezing the curve, once the joint optimization has succeeded; only a bootstrap that can join one is given
    // a parent (MultiCurve builds its members on GlobalBootstrap).
    internal PiecewiseYieldCurve(
        MultiCurveBootstrap? parent,
        Date referenceDate,
        Calendar? calendar,
        int? settlementDays,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps,
        IEnumerable<Date>? jumpDates,
        TInterpolator i,
        TBootstrap bootstrap,
        YieldTermStructure? baseCurve = null)
        : base(referenceDate, calendar, CheckArguments(context, instruments, dayCounter), settlementDays, jumps, jumpDates)
    {
        this.baseCurve = baseCurve;
        if (baseCurve is not null)
        {
            // a spread curve takes its base's extrapolation flag, as QuantLib's relative derived curves do
            AllowsExtrapolation = baseCurve.AllowsExtrapolation;
        }

        Interpolator = i;
        Bootstrap = bootstrap;
        BootstrapState<YieldTermStructure> s = new(this, instruments, DefaultAccuracy, context, nameof(instruments));
        state = s;
        if (parent is not null)
        {
            parent.Add(((IMultiCurveBootstrap)bootstrap).Contributor<YieldTermStructure, TTraits, TInterpolator>(
                s, i, context, Freeze));
            return;
        }

        bootstrap.Calculate<YieldTermStructure, TTraits, TInterpolator>(s, i, context);
        Freeze();
    }

    /// <summary>Gets the interpolator the curve was built with.</summary>
    public TInterpolator Interpolator { get; }

    /// <summary>Gets the bootstrap the curve was built with.</summary>
    public TBootstrap Bootstrap { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// The traits' rule: the latest date the helpers need, or the last node's (<see cref="IYieldTraits.MaxDate"/>); a
    /// spread curve's the earlier of that and its base's maximum date.
    /// </remarks>
    /// <exception cref="NanoQLException">
    /// The curve is read from another curve's bootstrap before its own has set its nodes (a multicurve's guess function).
    /// </exception>
    public sealed override Date MaxDate
    {
        get
        {
            Date own = state is { } s
                ? TTraits.MaxDate(s.Dates.Length == 0 ? throw NotSetUp() : s.Dates, s.MaxDate)
                : TTraits.MaxDate(dates.AsSpan(), curve!.MaxDate);
            if (baseCurve is null)
            {
                return own;
            }

            Date baseMax = baseCurve.MaxDate;
            return own < baseMax ? own : baseMax;
        }
    }

    /// <inheritdoc/>
    public sealed override double MaxTime => base.MaxTime;

    // The base curve of a spread curve, null for every other curve.
    private protected YieldTermStructure? SpreadBase => baseCurve;

    /// <inheritdoc/>
    public sealed override DayCounter DayCounter => base.DayCounter;

    /// <summary>
    /// A curve with a reference date that is the evaluation date advanced by settlement days on a calendar: QuantLib's
    /// constructors taking settlement days, whose reference date moves with the evaluation date. The curve is built for
    /// the context's evaluation date.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar the settlement days are counted on.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <returns>The bootstrapped curve.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="calendar"/>, <paramref name="instruments"/> or
    /// <paramref name="dayCounter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// A helper is <see langword="null"/>, or the jumps and their dates differ in number.
    /// </exception>
    /// <exception cref="NanoQLException">The bootstrap fails.</exception>
    public static PiecewiseYieldCurve<TTraits, TInterpolator, TBootstrap> Create(
        int settlementDays,
        Calendar calendar,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator i = default,
        TBootstrap bootstrap = default)
    {
        Date referenceDate = SettlementDate(settlementDays, calendar, context);
        return new PiecewiseYieldCurve<TTraits, TInterpolator, TBootstrap>(
            referenceDate, calendar, settlementDays, instruments, dayCounter, context, jumps, jumpDates, i, bootstrap);
    }

    /// <inheritdoc/>
    /// <remarks>On the working state while the curve is built.</remarks>
    public ImmutableArray<double> Times() => state is { } s ? [.. s.Times] : curve!.Times();

    /// <inheritdoc/>
    /// <remarks>On the working state while the curve is built.</remarks>
    public ImmutableArray<Date> Dates() => state is { } s ? [.. s.Dates] : dates;

    /// <inheritdoc/>
    /// <remarks>On the working state while the curve is built.</remarks>
    public ImmutableArray<double> Data() => state is { } s ? [.. s.Data] : curve!.Data();

    /// <inheritdoc/>
    /// <remarks>On the working state while the curve is built.</remarks>
    public ImmutableArray<KeyValuePair<Date, double>> Nodes()
    {
        ImmutableArray<Date> d = Dates();
        ImmutableArray<double> data = Data();
        var nodes = ImmutableArray.CreateBuilder<KeyValuePair<Date, double>>(d.Length);
        for (int k = 0; k < d.Length; ++k)
        {
            nodes.Add(new KeyValuePair<Date, double>(d[k], data[k]));
        }

        return nodes.MoveToImmutable();
    }

    /// <summary>
    /// The reference date of a curve built from settlement days: the context's evaluation date advanced by them on the
    /// calendar, as QuantLib's moving term structures compute it.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The reference date.</returns>
    private protected static Date SettlementDate(int settlementDays, Calendar calendar, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        return calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The traits' formula (<see cref="IYieldTraits.DiscountImpl"/>), on the working state while the curve is built; a
    /// spread curve's the base's discount times it.
    /// </remarks>
    /// <exception cref="NanoQLException">
    /// The curve is read from another curve's bootstrap before its own has set its nodes (a multicurve's guess function).
    /// </exception>
    protected sealed override double DiscountImpl(double t)
    {
        double d = state is { } s
            ? TTraits.DiscountImpl(s.Times, s.Data, s.Interpolation ?? throw NotSetUp(), t)
            : TTraits.DiscountImpl(curve!.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);
        return baseCurve is null ? d : baseCurve.Discount(t, true) * d;
    }

    // The base curve, refused when null before the base constructor reads it.
    private static YieldTermStructure Base(YieldTermStructure baseCurve)
    {
        ArgumentNullException.ThrowIfNull(baseCurve);
        return baseCurve;
    }

    private static DayCounter CheckArguments(
        EvaluationContext context, IEnumerable<BootstrapHelper<YieldTermStructure>> instruments, DayCounter dayCounter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(instruments);
        ArgumentNullException.ThrowIfNull(dayCounter);
        return dayCounter;
    }

    // A working state with no nodes yet, which only another curve's bootstrap can reach: in a multicurve, a guess
    // function pricing a helper on a member added after its own curve.
    private static NanoQLException NotSetUp() => new("curve not set up yet: its bootstrap has set no nodes");

    // The bootstrap's result, frozen into the curve's nodes; the working state and the helpers bound to the curve go.
    private void Freeze()
    {
        BootstrapState<YieldTermStructure> s = state!;
        dates = [.. s.Dates];
        curve = new InterpolatedCurve<TInterpolator>([.. s.Times], [.. s.Data], Interpolator, s.Interpolation!, s.MaxDate);
        state = null;
    }
}
