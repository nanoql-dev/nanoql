// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A yield curve restated on another structure's clock: dated at the clock's reference date, on its day counter, its
/// discount at a time the curve's discount accrued by the instant the time denotes, 1 before the curve's own reference
/// date (<see cref="YieldTermStructure.DiscountAt"/>). For the units whose Hull-White parts read their curve by time:
/// built on it, they read the curve on the model's clock as QuantLib reads a curve that shares the clock's times.
/// </summary>
internal sealed class YieldTermStructureOnClock : YieldTermStructure
{
    private readonly YieldTermStructure curve;
    private readonly TermStructure clock;

    /// <summary>Initializes a new instance of the <see cref="YieldTermStructureOnClock"/> class.</summary>
    /// <param name="curve">The curve.</param>
    /// <param name="clock">The clock it is restated on.</param>
    internal YieldTermStructureOnClock(YieldTermStructure curve, TermStructure clock)
        : base(clock.ReferenceDate, curve.Calendar, clock.DayCounter)
    {
        this.curve = curve;
        this.clock = clock;
    }

    /// <inheritdoc/>
    public override Date MaxDate => curve.MaxDate;

    /// <inheritdoc/>
    protected override double DiscountImpl(double t) => curve.DiscountAt(clock, t, true);
}
