// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/piecewiseyoyoptionletvolatility.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A year-on-year optionlet volatility curve bootstrapped at one strike on optionlet helpers, flat in strike and
/// interpolated in time: QuantLib's <c>PiecewiseYoYOptionletVolatilityCurve</c>.
/// </summary>
/// <typeparam name="TInterpolator">The interpolator in time.</typeparam>
/// <typeparam name="TBootstrap">The bootstrap, <see cref="IterativeBootstrap"/> by QuantLib's default.</typeparam>
/// <typeparam name="TTraits">The traits, <see cref="YoYInflationVolatilityTraits"/> by QuantLib's default.</typeparam>
/// <remarks>
/// <para>
/// The curve bootstraps in its factory (<see cref="Create"/>), over a working state its helpers' bound copies price
/// against, as S-infl's piecewise year-on-year curve does; its first node is the base date at the base volatility, the
/// assumption on the unquoted early optionlets. It implements <see cref="IInterpolatedCurve{TTraits, TInterpolator}"/>,
/// where QuantLib's derives from the interpolated curve, and its nodes come from its dates and data, QuantLib's
/// master's (#2941), where 1.44's nodes are empty. The last date is the reference date plus the last time rounded up to
/// whole years, adjusted, QuantLib's approximation.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class PiecewiseYoYOptionletVolatilityCurve<TInterpolator, TBootstrap, TTraits>
    : YoYOptionletVolatilitySurface, IInterpolatedCurve<TTraits, TInterpolator>
    where TInterpolator : struct, IInterpolator
    where TBootstrap : struct, IBootstrap
    where TTraits : struct, IBootstrapTraits<YoYOptionletVolatilitySurface>
{
    // QuantLib's default accuracy for this curve.
    private const double DefaultAccuracy = 1.0e-12;

    private readonly double minStrike;
    private readonly double maxStrike;

    // Non-null only while the bootstrap runs, in the constructor; the frozen nodes are written after it.
    private BootstrapState<YoYOptionletVolatilitySurface>? state;
    private ImmutableArray<Date> dates;
    private InterpolatedCurve<TInterpolator>? curve;

    private PiecewiseYoYOptionletVolatilityCurve(
        Date referenceDate,
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period lag,
        Frequency frequency,
        bool indexIsInterpolated,
        double minStrike,
        double maxStrike,
        double baseYoYVolatility,
        IEnumerable<BootstrapHelper<YoYOptionletVolatilitySurface>> instruments,
        EvaluationContext context,
        double accuracy,
        TInterpolator interpolator,
        TBootstrap bootstrap)
        : base(referenceDate, settlementDays, cal, bdc, dc, lag, frequency, indexIsInterpolated)
    {
        this.minStrike = minStrike;
        this.maxStrike = maxStrike;
        Interpolator = interpolator;
        Bootstrap = bootstrap;

        // don't have the data yet except for the base volatility
        // must set to communicate with bootstrap
        SetBaseLevel(baseYoYVolatility);
        BootstrapState<YoYOptionletVolatilitySurface> s = new(this, instruments, accuracy, context, nameof(instruments));
        state = s;
        Bootstrap.Calculate<YoYOptionletVolatilitySurface, TTraits, TInterpolator>(s, Interpolator, context);
        dates = [.. s.Dates];
        curve = new InterpolatedCurve<TInterpolator>([.. s.Times], [.. s.Data], Interpolator, s.Interpolation!, s.MaxDate);
        state = null;
    }

    /// <summary>Gets the interpolator the curve was built with.</summary>
    public TInterpolator Interpolator { get; }

    /// <summary>Gets the bootstrap the curve was built with.</summary>
    public TBootstrap Bootstrap { get; }

    /// <inheritdoc/>
    /// <remarks>The reference date plus the last time rounded up to whole years, as an option tenor; on the working
    /// state while the curve is built.</remarks>
    public override Date MaxDate =>
        OptionDateFromTenor(new Period((int)Math.Ceiling(Interpolation().XMax), TimeUnit.Years));

    /// <summary>
    /// Builds the curve on the reference date the settlement days give after the evaluation date, and bootstraps it.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="cal">The calendar.</param>
    /// <param name="bdc">The convention adjusting option dates computed from tenors.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="lag">The observation lag.</param>
    /// <param name="frequency">The index's frequency.</param>
    /// <param name="indexIsInterpolated">Whether the index is observed with linear interpolation.</param>
    /// <param name="minStrike">The lowest strike the curve gives volatilities for.</param>
    /// <param name="maxStrike">The highest strike the curve gives volatilities for.</param>
    /// <param name="baseYoYVolatility">The volatility at the base date, the first node's.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="context">The evaluation context the helpers are dated and priced in.</param>
    /// <param name="accuracy">The bootstrap's accuracy.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">A helper is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative, or an enum argument
    /// isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The bootstrap fails.</exception>
    public static PiecewiseYoYOptionletVolatilityCurve<TInterpolator, TBootstrap, TTraits> Create(
        int settlementDays,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        Period lag,
        Frequency frequency,
        bool indexIsInterpolated,
        double minStrike,
        double maxStrike,
        double baseYoYVolatility,
        IEnumerable<BootstrapHelper<YoYOptionletVolatilitySurface>> instruments,
        EvaluationContext context,
        double accuracy = DefaultAccuracy,
        TInterpolator interpolator = default,
        TBootstrap bootstrap = default)
    {
        ArgumentNullException.ThrowIfNull(cal);
        ArgumentNullException.ThrowIfNull(instruments);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        Date referenceDate = cal.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
        return new PiecewiseYoYOptionletVolatilityCurve<TInterpolator, TBootstrap, TTraits>(
            referenceDate, settlementDays, cal, bdc, dc, lag, frequency, indexIsInterpolated, minStrike, maxStrike,
            baseYoYVolatility, instruments, context, accuracy, interpolator, bootstrap);
    }

    /// <inheritdoc/>
    public override double MinStrike() => minStrike;

    /// <inheritdoc/>
    public override double MaxStrike() => maxStrike;

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
    /// <remarks>From the dates and data, QuantLib's master's (#2941); on the working state while the curve is
    /// built.</remarks>
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

    /// <inheritdoc/>
    /// <remarks>The interpolation's value at the time, whatever the strike; on the working state while the curve is
    /// built.</remarks>
    protected override double VolatilityImpl(double length, double strike) => Interpolation().Evaluate(length);

    // A working state with no nodes yet.
    private static NanoQLException NotSetUp() => new("curve not set up yet: its bootstrap has set no nodes");

    private Interpolation Interpolation() =>
        state is { } s ? s.Interpolation ?? throw NotSetUp() : curve!.Interpolation;
}
