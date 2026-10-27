// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/inflation/piecewisezeroinflationcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A zero-inflation term structure bootstrapped on inflation helpers: each helper's pillar marks the end of an
/// interpolated segment of zero-coupon inflation rates, solved so that the helper reprices its quote.
/// </summary>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <typeparam name="TBootstrap">The bootstrap (<see cref="IterativeBootstrap"/>,
/// <see cref="GlobalBootstrap"/>).</typeparam>
/// <typeparam name="TTraits">The traits, <see cref="ZeroInflationTraits"/> by QuantLib's default.</typeparam>
/// <remarks>
/// <para>
/// The curve bootstraps in its constructor, over a working state its helpers' bound copies price against, as
/// <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}"/> does; the helpers whose dates follow the
/// evaluation date are re-dated for the context's. It implements
/// <see cref="IInterpolatedCurve{TTraits, TInterpolator}"/>, where QuantLib's derives from the interpolated curve. The
/// members its bootstrap calls on the curve under construction are sealed.
/// </para>
/// <para>
/// QuantLib's lazy base date, a function its first calculation calls, is a function of the context here, called once
/// by the constructor with the curve's context, and passed to the base constructor, whose seasonality check reads it.
/// QuantLib's lazy constructor gives its base <c>Date()</c>, so a seasonality of more than one cycle checks itself
/// against the null date, whose month is read before its table, which is undefined; here it checks itself against the
/// base date the function computes, a documented divergence.
/// </para>
/// <para>
/// <see cref="InflationTermStructureExtensions.SetSeasonality"/> returns a copy bootstrapped again with the
/// seasonality, from a cold start, under the context given, with a lazy base date computed again in it: QuantLib
/// re-bootstraps warm, from the curve's own nodes, which differs from a fresh build by up to 7.9e-15 at its default
/// accuracy, a documented divergence. Unsealed for its named class, which adds constructors only.
/// </para>
/// </remarks>
public class PiecewiseZeroInflationCurve<TInterpolator, TBootstrap, TTraits>
    : ZeroInflationTermStructure, IInterpolatedCurve<TTraits, TInterpolator>
    where TInterpolator : struct, IInterpolator
    where TBootstrap : struct, IBootstrap
    where TTraits : struct, IZeroInflationTraits
{
    // QuantLib's default accuracy for this curve.
    private const double DefaultAccuracy = 1.0e-14;

    // QuantLib's instruments_, kept for SetSeasonality's re-bootstrap.
    private readonly ImmutableArray<BootstrapHelper<ZeroInflationTermStructure>> instruments;
    private readonly double accuracy;
    private readonly Func<EvaluationContext, Date>? baseDateFunc;

    // Non-null only while a bootstrap runs, in the constructor or on a fresh copy (WithSeasonality); the frozen nodes
    // are written by Freeze.
    private BootstrapState<ZeroInflationTermStructure>? state;
    private ImmutableArray<Date> dates;
    private ImmutableArray<double> additionalVariables = [];
    private InterpolatedCurve<TInterpolator>? curve;

    /// <summary>
    /// Initializes a new instance of the <see cref="PiecewiseZeroInflationCurve{TInterpolator, TBootstrap, TTraits}"/>
    /// class, and bootstraps it.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="baseDate">The base date: the last date for which there is information.</param>
    /// <param name="frequency">The frequency of the index the curve forecasts.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="context">The evaluation context the helpers are dated and priced in.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <param name="accuracy">The bootstrap's accuracy.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <exception cref="ArgumentNullException"><paramref name="instruments"/>, <paramref name="dayCounter"/> or
    /// <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">A helper is null, or <paramref name="baseDate"/> is
    /// <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the curve, or the bootstrap
    /// fails.</exception>
    public PiecewiseZeroInflationCurve(
        Date referenceDate,
        Date baseDate,
        Frequency frequency,
        DayCounter dayCounter,
        IEnumerable<BootstrapHelper<ZeroInflationTermStructure>> instruments,
        EvaluationContext context,
        Seasonality? seasonality = null,
        double accuracy = DefaultAccuracy,
        TInterpolator i = default,
        TBootstrap bootstrap = default)
        : this(referenceDate, baseDate, null, frequency, dayCounter, instruments, context, seasonality, accuracy, i, bootstrap)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PiecewiseZeroInflationCurve{TInterpolator, TBootstrap, TTraits}"/>
    /// class whose base date a function computes in the context, and bootstraps it.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="baseDateFunc">The function computing the base date from the context, typically the period of the
    /// index's last fixing: QuantLib's lazy base date.</param>
    /// <param name="frequency">The frequency of the index the curve forecasts.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="context">The evaluation context the base date is computed in and the helpers are dated and priced
    /// in.</param>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <param name="accuracy">The bootstrap's accuracy.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <exception cref="ArgumentNullException"><paramref name="baseDateFunc"/>, <paramref name="instruments"/>,
    /// <paramref name="dayCounter"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">A helper is null, or the function returns <c>default(Date)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frequency"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the curve, or the bootstrap
    /// fails.</exception>
    public PiecewiseZeroInflationCurve(
        Date referenceDate,
        Func<EvaluationContext, Date> baseDateFunc,
        Frequency frequency,
        DayCounter dayCounter,
        IEnumerable<BootstrapHelper<ZeroInflationTermStructure>> instruments,
        EvaluationContext context,
        Seasonality? seasonality = null,
        double accuracy = DefaultAccuracy,
        TInterpolator i = default,
        TBootstrap bootstrap = default)
        : this(
            referenceDate,
            BaseDateOf(baseDateFunc, context),
            baseDateFunc,
            frequency,
            dayCounter,
            instruments,
            context,
            seasonality,
            accuracy,
            i,
            bootstrap)
    {
    }

    private PiecewiseZeroInflationCurve(
        Date referenceDate,
        Date baseDate,
        Func<EvaluationContext, Date>? baseDateFunc,
        Frequency frequency,
        DayCounter dayCounter,
        IEnumerable<BootstrapHelper<ZeroInflationTermStructure>> instruments,
        EvaluationContext context,
        Seasonality? seasonality,
        double accuracy,
        TInterpolator i,
        TBootstrap bootstrap)
        : base(referenceDate, baseDate, frequency, CheckArguments(context, instruments, dayCounter), seasonality)
    {
        Interpolator = i;
        Bootstrap = bootstrap;
        this.baseDateFunc = baseDateFunc;
        this.accuracy = accuracy;
        var helpers = ImmutableArray.CreateBuilder<BootstrapHelper<ZeroInflationTermStructure>>();
        foreach (BootstrapHelper<ZeroInflationTermStructure> helper in instruments)
        {
            helpers.Add(helper ?? throw new ArgumentException("null rate helper", nameof(instruments)));
        }

        this.instruments = helpers.ToImmutable();
        Calculate(context);
    }

    /// <summary>Gets the interpolator the curve was built with.</summary>
    public TInterpolator Interpolator { get; }

    /// <summary>Gets the bootstrap the curve was built with.</summary>
    public TBootstrap Bootstrap { get; }

    /// <inheritdoc/>
    /// <remarks>The traits' rule: the latest date the helpers need, or the last node's.</remarks>
    public sealed override Date MaxDate =>
        state is { } s
            ? TTraits.MaxDate(s.Dates.Length == 0 ? throw NotSetUp() : s.Dates, s.MaxDate)
            : TTraits.MaxDate(dates.AsSpan(), curve!.MaxDate);

    /// <inheritdoc/>
    public sealed override double MaxTime => base.MaxTime;

    /// <inheritdoc/>
    public sealed override DayCounter DayCounter => base.DayCounter;

    /// <inheritdoc/>
    /// <remarks>On the working state while the curve is built.</remarks>
    public ImmutableArray<double> Times() => state is { } s ? [.. s.Times] : curve!.Times();

    /// <inheritdoc/>
    /// <remarks>On the working state while the curve is built.</remarks>
    public ImmutableArray<Date> Dates() => state is { } s ? [.. s.Dates] : dates;

    /// <inheritdoc/>
    /// <remarks>On the working state while the curve is built.</remarks>
    public ImmutableArray<double> Data() => state is { } s ? [.. s.Data] : curve!.Data();

    /// <summary>
    /// Gets the values of its global bootstrap's additional variables at the curve's optimum
    /// (<see cref="GlobalBootstrap.AdditionalVariables"/>), which QuantLib leaves in its quotes; empty for another
    /// bootstrap, or for one with no variables.
    /// </summary>
    /// <remarks>On the working state while the curve is built.</remarks>
    public ImmutableArray<double> AdditionalVariables => state is { } s ? s.AdditionalVariables : additionalVariables;

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
    /// A copy of the curve with another seasonality, bootstrapped again from a cold start under the context given, its
    /// lazy base date computed again in it: <see cref="InflationTermStructureExtensions.SetSeasonality"/>'s step.
    /// </summary>
    /// <param name="seasonality">The seasonality, or <see langword="null"/> for none.</param>
    /// <param name="context">The evaluation context the copy is bootstrapped in.</param>
    /// <returns>The copy, of the curve's runtime type; the curve is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">The seasonality is inconsistent with the copy, or the bootstrap
    /// fails.</exception>
    protected internal override InflationTermStructure WithSeasonality(
        Seasonality? seasonality, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date baseDate = baseDateFunc is null ? BaseDate : BaseDateOf(baseDateFunc, context);
        var copy = (PiecewiseZeroInflationCurve<TInterpolator, TBootstrap, TTraits>)MemberwiseClone();
        copy.ResetBase(baseDate, seasonality);
        copy.Calculate(context);
        return copy;
    }

    /// <inheritdoc/>
    /// <remarks>The traits' formula, on the working state while the curve is built.</remarks>
    protected sealed override double ZeroRateImpl(double t) =>
        state is { } s
            ? TTraits.ZeroRateImpl(s.Interpolation ?? throw NotSetUp(), t)
            : TTraits.ZeroRateImpl(curve!.Interpolation, t);

    private static DayCounter CheckArguments(
        EvaluationContext context,
        IEnumerable<BootstrapHelper<ZeroInflationTermStructure>> instruments,
        DayCounter dayCounter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(instruments);
        ArgumentNullException.ThrowIfNull(dayCounter);
        return dayCounter;
    }

    // QuantLib's lazy base date, computed in the context.
    private static Date BaseDateOf(Func<EvaluationContext, Date> baseDateFunc, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(baseDateFunc);
        ArgumentNullException.ThrowIfNull(context);
        Date baseDate = baseDateFunc(context);
        Date.ThrowIfDefault(baseDate, nameof(baseDateFunc));
        return baseDate;
    }

    // A working state with no nodes yet.
    private static NanoQLException NotSetUp() => new("curve not set up yet: its bootstrap has set no nodes");

    // QuantLib's performCalculations: the bootstrap, from a cold start, then the frozen nodes.
    private void Calculate(EvaluationContext context)
    {
        BootstrapState<ZeroInflationTermStructure> s = new(this, instruments, accuracy, context, "instruments");
        state = s;
        Bootstrap.Calculate<ZeroInflationTermStructure, TTraits, TInterpolator>(s, Interpolator, context);
        dates = [.. s.Dates];
        curve = new InterpolatedCurve<TInterpolator>([.. s.Times], [.. s.Data], Interpolator, s.Interpolation!, s.MaxDate);
        additionalVariables = s.AdditionalVariables;
        state = null;
    }
}
