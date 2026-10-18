// SPDX-FileCopyrightText: Copyright (C) 2025 Eugene Toder
// SPDX-FileCopyrightText: Copyright (C) 2025 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/multicurve.hpp ql/termstructures/multicurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// Builds a set of curves that form a dependency cycle, solving the bootstrapped ones together with one optimizer.
/// </summary>
/// <remarks>
/// <para>
/// The steps to set up the cycle's members:
/// </para>
/// <list type="number">
/// <item>Create a <see cref="MultiCurveMember"/> for each member, with the member's reference date and day counter, to
/// represent it: QuantLib's empty internal handle.</item>
/// <item>Build the cycle's indexes and rate helpers on the members.</item>
/// <item>Add each member with its curve's arguments: <see cref="AddBootstrappedCurve{TTraits, TInterpolator}(MultiCurveMember, Date, IEnumerable{BootstrapHelper{YieldTermStructure}}, DayCounter, IEnumerable{double}?, IEnumerable{Date}?, TInterpolator, GlobalBootstrap)"/>
/// for a piecewise curve on a <see cref="GlobalBootstrap"/>, in either of the piecewise curve's forms, and
/// <see cref="AddNonBootstrappedCurve"/> for any other curve.</item>
/// <item><see cref="Build"/>: it builds the bootstrapped members, links each member once to its curve, bootstraps the
/// bootstrapped curves jointly, every curve's data being set before any helper is priced, and returns the curves in
/// the order they were added.</item>
/// </list>
/// <para>
/// An immutable builder: each <c>Add…</c> returns a copy. The members are linked by <see cref="Build"/>, not by the
/// <c>Add…</c> calls, since a curve is built in an evaluation context; a member already linked, or already added, is
/// refused, so a second <see cref="Build"/> of the same builder refuses. A non-bootstrapped curve reads the curves it
/// references when it is queried, so it needs nothing from the joint bootstrap. QuantLib's checks that a curve is a
/// multicurve bootstrap provider with a contributor have no counterpart: a bootstrapped member is built on
/// <see cref="GlobalBootstrap"/> by <see cref="Build"/> itself.
/// </para>
/// </remarks>
public sealed record MultiCurve
{
    private const string AddedAlready = "internal handle must be empty; was the curve added already?";

    // The constructor's arguments: an accuracy, or an optimizer and end criteria, the defaults built at each Build.
    private readonly double? accuracy;
    private readonly OptimizationMethod? optimizer;
    private readonly EndCriteria? endCriteria;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiCurve"/> class whose joint bootstrap runs the Levenberg–Marquardt
    /// method and end criteria of 1000 iterations and 10 stationary ones, each tolerance the accuracy.
    /// </summary>
    /// <param name="accuracy">The accuracy.</param>
    public MultiCurve(double accuracy)
    {
        this.accuracy = accuracy;
    }

    /// <summary>Initializes a new instance of the <see cref="MultiCurve"/> class.</summary>
    /// <param name="optimizer">
    /// The joint bootstrap's optimization method; <see langword="null"/> for Levenberg–Marquardt at an accuracy of 1e-10.
    /// </param>
    /// <param name="endCriteria">
    /// The joint bootstrap's end criteria; <see langword="null"/> for 1000 iterations and 10 stationary ones at an
    /// accuracy of 1e-10.
    /// </param>
    public MultiCurve(OptimizationMethod? optimizer = null, EndCriteria? endCriteria = null)
    {
        this.optimizer = optimizer;
        this.endCriteria = endCriteria;
    }

    // The members added, in order, each with how Build builds its curve.
    private ImmutableArray<Entry> Entries { get; init; } = [];

    /// <summary>
    /// A copy of the builder with a member whose curve is a piecewise curve with a fixed reference date, bootstrapped
    /// with the cycle's other bootstrapped curves.
    /// </summary>
    /// <typeparam name="TTraits">The curve's traits.</typeparam>
    /// <typeparam name="TInterpolator">The curve's interpolator.</typeparam>
    /// <param name="internalHandle">The member, on which the cycle's indexes and helpers are built.</param>
    /// <param name="referenceDate">The curve's reference date.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The curve's day counter.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The curve's global bootstrap, whose own optimizer and end criteria the joint bootstrap doesn't use.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="internalHandle"/>, <paramref name="instruments"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The member is already linked or added, or a helper is <see langword="null"/>.
    /// </exception>
    public MultiCurve AddBootstrappedCurve<TTraits, TInterpolator>(
        MultiCurveMember internalHandle,
        Date referenceDate,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        DayCounter dayCounter,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator i = default,
        GlobalBootstrap bootstrap = default)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator
    {
        CheckEmpty(internalHandle);
        ImmutableArray<BootstrapHelper<YieldTermStructure>> helpers = Helpers(instruments);
        ArgumentNullException.ThrowIfNull(dayCounter);
        double[]? jumpValues = jumps?.ToArray();
        Date[]? dates = jumpDates?.ToArray();
        return this with
        {
            Entries = Entries.Add(new Entry(
                internalHandle,
                (parent, context) => new PiecewiseYieldCurve<TTraits, TInterpolator, GlobalBootstrap>(
                    parent, referenceDate, null, null, helpers, dayCounter, context, jumpValues, dates, i, bootstrap))),
        };
    }

    /// <summary>
    /// A copy of the builder with a member whose curve is a piecewise curve whose reference date is the evaluation date
    /// advanced by settlement days on a calendar, bootstrapped with the cycle's other bootstrapped curves: the curve
    /// <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}.Create"/> builds in <see cref="Build"/>'s
    /// context.
    /// </summary>
    /// <typeparam name="TTraits">The curve's traits.</typeparam>
    /// <typeparam name="TInterpolator">The curve's interpolator.</typeparam>
    /// <param name="internalHandle">The member, on which the cycle's indexes and helpers are built.</param>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar the settlement days are counted on.</param>
    /// <param name="instruments">The helpers the curve is bootstrapped on.</param>
    /// <param name="dayCounter">The curve's day counter.</param>
    /// <param name="jumps">The discount-factor jumps, or <see langword="null"/> for none.</param>
    /// <param name="jumpDates">The jumps' dates, or <see langword="null"/> for 31 December of successive years.</param>
    /// <param name="i">The interpolator.</param>
    /// <param name="bootstrap">The curve's global bootstrap, whose own optimizer and end criteria the joint bootstrap doesn't use.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="internalHandle"/>, <paramref name="calendar"/>, <paramref name="instruments"/> or
    /// <paramref name="dayCounter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The member is already linked or added, or a helper is <see langword="null"/>.
    /// </exception>
    public MultiCurve AddBootstrappedCurve<TTraits, TInterpolator>(
        MultiCurveMember internalHandle,
        int settlementDays,
        Calendar calendar,
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments,
        DayCounter dayCounter,
        IEnumerable<double>? jumps = null,
        IEnumerable<Date>? jumpDates = null,
        TInterpolator i = default,
        GlobalBootstrap bootstrap = default)
        where TTraits : struct, IYieldTraits
        where TInterpolator : struct, IInterpolator
    {
        CheckEmpty(internalHandle);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        ImmutableArray<BootstrapHelper<YieldTermStructure>> helpers = Helpers(instruments);
        ArgumentNullException.ThrowIfNull(dayCounter);
        double[]? jumpValues = jumps?.ToArray();
        Date[]? dates = jumpDates?.ToArray();
        return this with
        {
            Entries = Entries.Add(new Entry(
                internalHandle,
                (parent, context) => new PiecewiseYieldCurve<TTraits, TInterpolator, GlobalBootstrap>(
                    parent,
                    MultiCurveMember.SettlementDate(settlementDays, calendar, context),
                    calendar,
                    settlementDays,
                    helpers,
                    dayCounter,
                    context,
                    jumpValues,
                    dates,
                    i,
                    bootstrap))),
        };
    }

    /// <summary>
    /// A copy of the builder with a member whose curve isn't bootstrapped: a curve that reads other members when it is
    /// queried, as a spread over one of them does.
    /// </summary>
    /// <param name="internalHandle">The member, on which the cycle's indexes and helpers are built.</param>
    /// <param name="curve">The member's curve.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="internalHandle"/> or <paramref name="curve"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">The member is already linked or added.</exception>
    public MultiCurve AddNonBootstrappedCurve(MultiCurveMember internalHandle, YieldTermStructure curve)
    {
        CheckEmpty(internalHandle);
        if (curve is null)
        {
            throw new ArgumentNullException(nameof(curve), "curve must not be null");
        }

        return this with { Entries = Entries.Add(new Entry(internalHandle, (_, _) => curve)) };
    }

    /// <summary>
    /// Builds the members' curves, links each member to its curve and bootstraps the bootstrapped curves jointly.
    /// </summary>
    /// <param name="context">The evaluation context the curves are built in.</param>
    /// <returns>The curves, in the order they were added.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// A member was linked since it was added, a curve's reference date or day counter differs from its member's, a
    /// curve's bootstrap setup refuses, or the joint optimization fails to minimize to the required accuracy; no member
    /// is then linked.
    /// </exception>
    public ImmutableArray<YieldTermStructure> Build(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MultiCurveBootstrap multiCurveBootstrap = accuracy is double a
            ? new MultiCurveBootstrap(a)
            : new MultiCurveBootstrap(optimizer, endCriteria);

        // the curves, built in add order: each bootstrapped one adds its contributor to the joint bootstrap
        var curves = new YieldTermStructure[Entries.Length];
        for (int k = 0; k < Entries.Length; ++k)
        {
            Entry e = Entries[k];
            if (e.Member.IsLinked)
            {
                throw new NanoQLException(AddedAlready);
            }

            curves[k] = e.Curve(multiCurveBootstrap, context);
            e.Member.CheckCurve(curves[k]);
        }

        for (int k = 0; k < Entries.Length; ++k)
        {
            Entries[k].Member.LinkTo(curves[k]);
        }

        try
        {
            // QuantLib's joint bootstrap runs when a contributing curve is first read; the members are built when it
            // returns
            if (multiCurveBootstrap.HasContributors)
            {
                multiCurveBootstrap.RunMultiCurveBootstrap();
            }
        }
        catch
        {
            foreach (Entry e in Entries)
            {
                e.Member.LinkTo(null);
            }

            throw;
        }

        return [.. curves];
    }

    // QuantLib's check of the internal handle on adding it, and nanoql's of a null one.
    private void CheckEmpty(MultiCurveMember internalHandle)
    {
        ArgumentNullException.ThrowIfNull(internalHandle);
        if (internalHandle.IsLinked || Entries.Any(e => e.Member.SharesLinkWith(internalHandle)))
        {
            throw new ArgumentException(AddedAlready, nameof(internalHandle));
        }
    }

    // The curve's helpers, as given: the builder keeps its own list.
    private static ImmutableArray<BootstrapHelper<YieldTermStructure>> Helpers(
        IEnumerable<BootstrapHelper<YieldTermStructure>> instruments)
    {
        ArgumentNullException.ThrowIfNull(instruments);
        ImmutableArray<BootstrapHelper<YieldTermStructure>> helpers = [.. instruments];
        if (helpers.Contains(null!))
        {
            throw new ArgumentException("null rate helper", nameof(instruments));
        }

        return helpers;
    }

    // A member added, with how Build builds its curve: in the joint bootstrap, for a bootstrapped curve, in a context.
    private sealed record Entry(MultiCurveMember Member, Func<MultiCurveBootstrap, EvaluationContext, YieldTermStructure> Curve);
}
