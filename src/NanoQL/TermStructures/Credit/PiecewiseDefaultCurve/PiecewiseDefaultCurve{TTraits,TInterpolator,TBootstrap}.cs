// SPDX-FileCopyrightText: Copyright (C) 2008, 2016 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2008 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/credit/piecewisedefaultcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A default-probability term structure bootstrapped on credit helpers: each helper's pillar marks the end of an
/// interpolated segment, solved so that the helper reprices its quote.
/// </summary>
/// <typeparam name="TTraits">The traits, which name the curve kind (<see cref="SurvivalProbability"/>,
/// <see cref="HazardRate"/>, <see cref="DefaultDensity"/>).</typeparam>
/// <typeparam name="TInterpolator">The interpolator.</typeparam>
/// <typeparam name="TBootstrap">The bootstrap (<see cref="IterativeBootstrap"/>,
/// <see cref="GlobalBootstrap"/>).</typeparam>
/// <remarks>
/// <para>
/// The curve bootstraps in its constructor, over a working state its helpers' bound copies price against, as
/// <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}"/> does; the helpers whose dates follow the
/// evaluation date are re-dated for the context's. QuantLib's curve derives from its traits' interpolated curve; this
/// one derives from <see cref="DefaultProbabilityTermStructure"/> and is that curve through
/// <see cref="IInterpolatedCurve{TTraits, TInterpolator}"/>, with the traits' formulas, so a piecewise hazard-rate
/// curve isn't a <see cref="HazardRateStructure"/> here; no QuantLib code casts to one. The members its bootstrap
/// calls on the curve under construction are sealed.
/// </para>
/// <para>
/// QuantLib's settlement-days constructors are <see cref="Create"/>. Its constructor on a one-factor affine model of
/// the default intensity is the one taking a <see cref="OneFactorAffineModel"/>, for the affine hazard-rate traits: the
/// curve calls the traits' model-aware members when it holds a model, which other traits refuse, and the landed
/// formulas otherwise. QuantLib's piecewise affine curve is a one-factor affine survival structure, with its
/// conditional survival; this one can't change its base by its traits, so that structure is built on its nodes. A curve
/// is always built afresh, as the piecewise yield curve is. Unsealed for its named class, which adds constructors only.
/// </para>
/// </remarks>
public class PiecewiseDefaultCurve<TTraits, TInterpolator, TBootstrap>
    : DefaultProbabilityTermStructure, IInterpolatedCurve<TTraits, TInterpolator>
    where TTraits : struct, IDefaultProbabilityTraits
    where TInterpolator : struct, IInterpolator
    where TBootstrap : struct, IBootstrap
{
    // QuantLib's accuracy_.
    private const double Accuracy = 1.0e-12;

    // Non-null only while the constructor bootstraps the curve; the frozen nodes are written once, by Calculate.
    private BootstrapState<DefaultProbabilityTermStructure>? state;
    private ImmutableArray<Date> dates;
    private ImmutableArray<double> additionalVariables = [];
    private InterpolatedCurve<TInterpolator>? curve;

    /// <summary>
    /// Initializes a new instance of the <see cref="PiecewiseDefaultCurve{TTraits, TInterpolator, TBootstrap}"/> class
    /// with a fixed reference date, and bootstraps it.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context the helpers are dated and priced in.</param>
    /// <param name="jumps">The survival-probability jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="instruments"/> or
    /// <paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A helper is <see langword="null"/>, the reference date is
    /// <c>default(Date)</c>, or the jumps and their dates differ in number.</exception>
    /// <exception cref="NanoQLException">The bootstrap fails.</exception>
    public PiecewiseDefaultCurve(
        Date referenceDate,
        IEnumerable<BootstrapHelper<DefaultProbabilityTermStructure>> instruments,
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
    /// Initializes a new instance of the <see cref="PiecewiseDefaultCurve{TTraits, TInterpolator, TBootstrap}"/> class
    /// on a one-factor affine model of the default intensity, and bootstraps it: the curve is the deterministic part
    /// added to the model so that the helpers reprice, QuantLib's constructor for its affine hazard-rate traits.
    /// </summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="model">The model of the default intensity.</param>
    /// <param name="context">The evaluation context the helpers are dated and priced in.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <exception cref="ArgumentNullException">An argument but the interpolator and bootstrap is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A helper is <see langword="null"/>, or the reference date is
    /// <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The traits take no model, or the bootstrap fails.</exception>
    public PiecewiseDefaultCurve(
        Date referenceDate,
        IEnumerable<BootstrapHelper<DefaultProbabilityTermStructure>> instruments,
        DayCounter dayCounter,
        OneFactorAffineModel model,
        EvaluationContext context,
        TInterpolator i = default,
        TBootstrap bootstrap = default)
        : this(referenceDate, null, null, instruments, dayCounter, context, null, null, i, bootstrap, model ?? throw new ArgumentNullException(nameof(model)))
    {
    }

    /// <summary>The constructors' work: the curve with its calendar and settlement days, bootstrapped.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="calendar">The calendar the settlement days were counted on, if any.</param>
    /// <param name="settlementDays">The settlement days, if any.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="jumps">The survival-probability jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/>.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <param name="model">The model of the default intensity, if any.</param>
    private protected PiecewiseDefaultCurve(
        Date referenceDate,
        Calendar? calendar,
        int? settlementDays,
        IEnumerable<BootstrapHelper<DefaultProbabilityTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps,
        IEnumerable<Date>? jumpDates,
        TInterpolator i,
        TBootstrap bootstrap,
        OneFactorAffineModel? model = null)
        : base(referenceDate, calendar, CheckArguments(context, instruments, dayCounter), settlementDays, jumps, jumpDates)
    {
        Model = model;
        Interpolator = i;
        Bootstrap = bootstrap;
        BootstrapState<DefaultProbabilityTermStructure> s =
            new(this, instruments, Accuracy, context, nameof(instruments));
        state = s;
        Bootstrap.Calculate<DefaultProbabilityTermStructure, TTraits, TInterpolator>(s, Interpolator, context);
        dates = [.. s.Dates];
        curve = new InterpolatedCurve<TInterpolator>([.. s.Times], [.. s.Data], Interpolator, s.Interpolation!, s.MaxDate);
        additionalVariables = s.AdditionalVariables;
        state = null;
    }

    /// <summary>Gets the one-factor affine model of the default intensity the curve was built on, if any.</summary>
    public OneFactorAffineModel? Model { get; }

    /// <summary>Gets the interpolator the curve was built with.</summary>
    public TInterpolator Interpolator { get; }

    /// <summary>Gets the bootstrap the curve was built with.</summary>
    public TBootstrap Bootstrap { get; }

    /// <inheritdoc/>
    /// <remarks>The last node's date, on the working state while the curve is built.</remarks>
    public sealed override Date MaxDate =>
        state is { } s
            ? TTraits.MaxDate(s.Dates.Length == 0 ? throw NotSetUp() : s.Dates)
            : TTraits.MaxDate(dates.AsSpan());

    /// <inheritdoc/>
    public sealed override double MaxTime => base.MaxTime;

    /// <inheritdoc/>
    public sealed override DayCounter DayCounter => base.DayCounter;

    /// <summary>
    /// A curve whose reference date is the given number of business days after the context's evaluation date:
    /// QuantLib's settlement-days constructors.
    /// </summary>
    /// <param name="settlementDays">The business days from the evaluation date to the reference date.</param>
    /// <param name="calendar">The calendar the days are counted on.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="jumps">The survival-probability jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/>.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The bootstrap.</param>
    /// <returns>The curve.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="calendar"/>, <paramref name="context"/>,
    /// <paramref name="instruments"/> or <paramref name="dayCounter"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">A helper is <see langword="null"/>, or the jumps and their dates differ in
    /// number.</exception>
    /// <exception cref="NanoQLException">The bootstrap fails.</exception>
    public static PiecewiseDefaultCurve<TTraits, TInterpolator, TBootstrap> Create(
        int settlementDays,
        Calendar calendar,
        IEnumerable<BootstrapHelper<DefaultProbabilityTermStructure>> instruments,
        DayCounter dayCounter,
        EvaluationContext context,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator i = default,
        TBootstrap bootstrap = default)
    {
        Date referenceDate = SettlementDate(settlementDays, calendar, context);
        return new PiecewiseDefaultCurve<TTraits, TInterpolator, TBootstrap>(
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

    /// <summary>The settlement date of the settlement-days forms.</summary>
    /// <param name="settlementDays">The business days from the evaluation date.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The date.</returns>
    private protected static Date SettlementDate(int settlementDays, Calendar calendar, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        return calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
    }

    /// <inheritdoc/>
    internal override double? HazardRateSurvival(double t)
    {
        if (Model is not null)
        {
            // the affine traits' rate, the deterministic part alone: its integral, the hazard-rate traits' survival
            return state is { } s
                ? TermStructures.HazardRate.SurvivalProbabilityImpl(s.Times, s.Data, s.Interpolation ?? throw NotSetUp(), t)
                : TermStructures.HazardRate.SurvivalProbabilityImpl(curve!.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t);
        }

        if (typeof(TTraits) == typeof(TermStructures.HazardRate))
        {
            return SurvivalProbabilityImpl(t);
        }

        return typeof(TTraits) == typeof(TermStructures.DefaultDensity) || typeof(TTraits) == typeof(TermStructures.SurvivalProbability)
            ? DensityHazardRateSurvival(t)
            : null;
    }

    /// <inheritdoc/>
    /// <remarks>The traits' formula, on the working state while the curve is built.</remarks>
    protected sealed override double SurvivalProbabilityImpl(double t) =>
        state is { } s
            ? (Model is null
                ? TTraits.SurvivalProbabilityImpl(s.Times, s.Data, s.Interpolation ?? throw NotSetUp(), t)
                : TTraits.SurvivalProbabilityImpl(s.Times, s.Data, s.Interpolation ?? throw NotSetUp(), t, Model))
            : (Model is null
                ? TTraits.SurvivalProbabilityImpl(curve!.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t)
                : TTraits.SurvivalProbabilityImpl(curve!.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t, Model));

    /// <inheritdoc/>
    /// <remarks>The traits' formula, on the working state while the curve is built.</remarks>
    protected sealed override double DefaultDensityImpl(double t) =>
        state is { } s
            ? (Model is null
                ? TTraits.DefaultDensityImpl(s.Times, s.Data, s.Interpolation ?? throw NotSetUp(), t)
                : TTraits.DefaultDensityImpl(s.Times, s.Data, s.Interpolation ?? throw NotSetUp(), t, Model))
            : (Model is null
                ? TTraits.DefaultDensityImpl(curve!.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t)
                : TTraits.DefaultDensityImpl(curve!.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t, Model));

    /// <inheritdoc/>
    /// <remarks>The traits' rate, on the working state while the curve is built.</remarks>
    protected sealed override double HazardRateImpl(double t) =>
        state is { } s
            ? (Model is null
                ? TTraits.HazardRateImpl(s.Times, s.Data, s.Interpolation ?? throw NotSetUp(), t, this)
                : TTraits.HazardRateImpl(s.Times, s.Data, s.Interpolation ?? throw NotSetUp(), t, this, Model))
            : (Model is null
                ? TTraits.HazardRateImpl(curve!.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t, this)
                : TTraits.HazardRateImpl(curve!.Times().AsSpan(), curve.Data().AsSpan(), curve.Interpolation, t, this, Model));

    private static DayCounter CheckArguments(
        EvaluationContext context,
        IEnumerable<BootstrapHelper<DefaultProbabilityTermStructure>> instruments,
        DayCounter dayCounter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(instruments);
        ArgumentNullException.ThrowIfNull(dayCounter);
        return dayCounter;
    }

    // A working state with no nodes yet.
    private static NanoQLException NotSetUp() => new("curve not set up yet: its bootstrap has set no nodes");
}
