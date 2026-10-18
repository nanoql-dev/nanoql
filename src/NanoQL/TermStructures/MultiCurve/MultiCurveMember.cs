// SPDX-FileCopyrightText: Copyright (C) 2025 Eugene Toder
// SPDX-FileCopyrightText: Copyright (C) 2025 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/multicurve.hpp ql/termstructures/multicurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A member of a cycle of curves (<see cref="MultiCurve"/>): QuantLib's internal handle, a curve on which the cycle's
/// indexes and helpers are built before the member exists, and which the multicurve's <see cref="MultiCurve.Build"/>
/// links once to the member it builds.
/// </summary>
/// <remarks>
/// <para>
/// A cycle of curves has no build order, so where every other handle becomes the object itself, a member is the one
/// reference written after it is built: once, by <see cref="MultiCurve.Build"/>. The link is shared by every copy of
/// the member (<see cref="Numerics.ExtrapolatorExtensions.EnableExtrapolation{T}"/> returns a copy), so an index built
/// on a copy taken before <c>Build</c> prices after it.
/// </para>
/// <para>
/// Before <c>Build</c>, every query refuses, "multicurve member not built"; its reference date, given when it is
/// created, is the one thing it answers. After <c>Build</c>, a query is checked against the member's own range and
/// extrapolation flag, then forwarded to the curve it is linked to with extrapolation allowed, so the member's flag
/// rules; its day counter, calendar, settlement days and jumps are that curve's. <c>Build</c> links it only to a curve
/// with its reference date and day counter, since it measures a date's time from its own reference date with its own
/// day counter and the curve reads that time against its own.
/// </para>
/// <para>
/// <c>Build</c> writes the members' working state while their handles are linked, so no other thread may read a member
/// during <c>Build</c>; once built, a member is immutable, as its curve is.
/// </para>
/// </remarks>
public sealed class MultiCurveMember : YieldTermStructure
{
    // The link every copy of the member shares: written once, by a multicurve's Build.
    private readonly Link link = new();

    /// <summary>Initializes a new instance of the <see cref="MultiCurveMember"/> class with a fixed reference date.</summary>
    /// <param name="referenceDate">The reference date, which the member's curve must have.</param>
    /// <param name="dayCounter">The day counter, which the member's curve must have.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reference date is <c>default(Date)</c>.</exception>
    public MultiCurveMember(Date referenceDate, DayCounter dayCounter)
        : base(referenceDate, null, dayCounter ?? throw new ArgumentNullException(nameof(dayCounter)))
    {
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The member isn't built.</exception>
    public override DayCounter DayCounter => Curve.DayCounter;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The member isn't built.</exception>
    public override Calendar? Calendar => Curve.Calendar;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The member isn't built.</exception>
    public override int? SettlementDays => Curve.SettlementDays;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The member isn't built.</exception>
    public override Date MaxDate => Curve.MaxDate;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The member isn't built.</exception>
    public override ImmutableArray<Date> JumpDates() => Curve.JumpDates();

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The member isn't built.</exception>
    public override ImmutableArray<double> JumpTimes() => Curve.JumpTimes();

    // Whether a multicurve's Build has linked the member.
    internal bool IsLinked => link.Curve is not null;

    // The curve the member is linked to.
    private YieldTermStructure Curve => link.Curve ?? throw new NanoQLException("multicurve member not built");

    /// <summary>
    /// A member whose reference date is the evaluation date advanced by settlement days on a calendar, as the piecewise
    /// curve's <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}.Create"/> computes its own: the member
    /// of a curve built that way for the same context.
    /// </summary>
    /// <param name="settlementDays">The settlement days.</param>
    /// <param name="calendar">The calendar the settlement days are counted on.</param>
    /// <param name="dayCounter">The day counter, which the member's curve must have.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The member.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, <paramref name="calendar"/> or <paramref name="dayCounter"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settlementDays"/> is negative.</exception>
    public static MultiCurveMember Create(int settlementDays, Calendar calendar, DayCounter dayCounter, EvaluationContext context)
    {
        Date referenceDate = SettlementDate(settlementDays, calendar, context);
        return new MultiCurveMember(referenceDate, dayCounter);
    }

    // The reference date of a member or curve built from settlement days, as QuantLib's moving term structures compute
    // it: the context's evaluation date advanced by them on the calendar.
    internal static Date SettlementDate(int settlementDays, Calendar calendar, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(settlementDays);
        ArgumentNullException.ThrowIfNull(calendar);
        return calendar.Advance(context.EvaluationDate, settlementDays, TimeUnit.Days);
    }

    // Build's checks before it links the member: the curve has the member's reference date and day counter.
    internal void CheckCurve(YieldTermStructure curve)
    {
        if (curve.ReferenceDate != ReferenceDate)
        {
            throw new NanoQLException(
                $"multicurve member's reference date ({curve.ReferenceDate}) differs from its handle's ({ReferenceDate})");
        }

        if (curve.DayCounter != base.DayCounter)
        {
            throw new NanoQLException(
                $"multicurve member's day counter ({curve.DayCounter}) differs from its handle's ({base.DayCounter})");
        }
    }

    // Build links the member once; a Build that fails unlinks the members it linked.
    internal void LinkTo(YieldTermStructure? curve) => link.Curve = curve;

    // Whether two members are copies of one, sharing its link.
    internal bool SharesLinkWith(MultiCurveMember other) => ReferenceEquals(link, other.link);

    /// <inheritdoc/>
    /// <remarks>The linked curve's discount factor, with extrapolation allowed: the member's own range was checked.</remarks>
    protected override double DiscountImpl(double t) => Curve.Discount(t, true);

    // The cell the member's copies share.
    private sealed class Link
    {
        public YieldTermStructure? Curve { get; set; }
    }
}
