// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2006, 2014 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2016 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/capfloor.hpp ql/instruments/capfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// What QuantLib's <c>CapFloor::setupArguments</c> computes for the cap and floor engines: the instrument's type and,
/// per floating coupon, its dates, accrual time, forward, nominal, gearing, spread, effective strikes and index.
/// </summary>
/// <remarks>
/// nanoql's caps hold no arguments object: the engines compute these from the leg with the context
/// (<see cref="CapFloor.SetupArguments"/>, <c>capfloor.cpp:210-269</c>), then check them with
/// <see cref="Validate"/>, as QuantLib's <c>Instrument::calculate</c> does. A forward, a cap rate or a floor rate
/// QuantLib sets to <c>Null&lt;Rate&gt;()</c> is <see langword="null"/>: the forward of a coupon paid before the
/// evaluation date, and the strikes of the other side.
/// </remarks>
internal sealed class CapFloorArguments
{
    private CapFloorArguments()
    {
    }

    /// <summary>Gets the instrument's type.</summary>
    public required CapFloorType Type { get; init; }

    /// <summary>Gets the coupons' accrual start dates.</summary>
    public required ImmutableArray<Date> StartDates { get; init; }

    /// <summary>Gets the coupons' fixing dates.</summary>
    public required ImmutableArray<Date> FixingDates { get; init; }

    /// <summary>Gets the coupons' payment dates.</summary>
    public required ImmutableArray<Date> EndDates { get; init; }

    /// <summary>Gets the coupons' accrual periods.</summary>
    public required ImmutableArray<double> AccrualTimes { get; init; }

    /// <summary>Gets the effective cap rates, net of spread and gearing, or <see langword="null"/> for a floor.</summary>
    public required ImmutableArray<double?> CapRates { get; init; }

    /// <summary>Gets the effective floor rates, net of spread and gearing, or <see langword="null"/> for a cap.</summary>
    public required ImmutableArray<double?> FloorRates { get; init; }

    /// <summary>
    /// Gets the coupons' adjusted fixings, or <see langword="null"/> for a coupon paid before the evaluation date.
    /// </summary>
    public required ImmutableArray<double?> Forwards { get; init; }

    /// <summary>Gets the coupons' gearings.</summary>
    public required ImmutableArray<double> Gearings { get; init; }

    /// <summary>Gets the coupons' spreads.</summary>
    public required ImmutableArray<double> Spreads { get; init; }

    /// <summary>Gets the coupons' nominals.</summary>
    public required ImmutableArray<double> Nominals { get; init; }

    /// <summary>Gets the coupons' indexes.</summary>
    public required ImmutableArray<InterestRateIndex> Indexes { get; init; }

    /// <summary>The arguments of a cap or floor, as QuantLib's <c>setupArguments</c> computes them.</summary>
    /// <param name="type">The instrument's type.</param>
    /// <param name="floatingLeg">The floating leg.</param>
    /// <param name="capRates">The cap rates, extended to the leg's length.</param>
    /// <param name="floorRates">The floor rates, extended to the leg's length.</param>
    /// <param name="context">The evaluation context: its evaluation date and fixings, which the forwards read.</param>
    /// <returns>The arguments.</returns>
    /// <exception cref="NanoQLException">A cash flow isn't a floating-rate coupon, or a forward can't be computed.</exception>
    internal static CapFloorArguments Of(
        CapFloorType type,
        ImmutableArray<CashFlow> floatingLeg,
        ImmutableArray<double> capRates,
        ImmutableArray<double> floorRates,
        EvaluationContext context)
    {
        int n = floatingLeg.Length;
        var startDates = new Date[n];
        var fixingDates = new Date[n];
        var endDates = new Date[n];
        var accrualTimes = new double[n];
        var forwards = new double?[n];
        var nominals = new double[n];
        var gearings = new double[n];
        var caps = new double?[n];
        var floors = new double?[n];
        var spreads = new double[n];
        var indexes = new InterestRateIndex[n];

        Date today = context.EvaluationDate;

        for (int i = 0; i < n; ++i)
        {
            // QuantLib's dynamic_pointer_cast and its check, at pricing: a leg holding another cash flow builds.
            var coupon = floatingLeg[i] as FloatingRateCoupon ?? throw new NanoQLException("non-FloatingRateCoupon given");
            startDates[i] = coupon.AccrualStartDate;
            fixingDates[i] = coupon.FixingDate();
            endDates[i] = coupon.Date;

            // this is passed explicitly for precision
            accrualTimes[i] = coupon.AccrualPeriod();

            // this is passed explicitly for precision...
            if (endDates[i] >= today)
            {
                // ...but only if needed
                forwards[i] = coupon.AdjustedFixing(context);
            }
            else
            {
                forwards[i] = null;
            }

            nominals[i] = coupon.Nominal(context);
            double spread = coupon.Spread;
            double gearing = coupon.Gearing;
            gearings[i] = gearing;
            spreads[i] = spread;

            caps[i] = type is CapFloorType.Cap or CapFloorType.Collar ? (capRates[i] - spread) / gearing : null;
            floors[i] = type is CapFloorType.Floor or CapFloorType.Collar ? (floorRates[i] - spread) / gearing : null;

            indexes[i] = coupon.Index;
        }

        return new CapFloorArguments
        {
            Type = type,
            StartDates = [.. startDates],
            FixingDates = [.. fixingDates],
            EndDates = [.. endDates],
            AccrualTimes = [.. accrualTimes],
            CapRates = [.. caps],
            FloorRates = [.. floors],
            Forwards = [.. forwards],
            Gearings = [.. gearings],
            Spreads = [.. spreads],
            Nominals = [.. nominals],
            Indexes = [.. indexes],
        };
    }

    /// <summary>
    /// QuantLib's <c>arguments::validate</c>: every vector as long as the start dates. Arguments computed from a leg
    /// can't fail it; the engines run it as QuantLib's protocol does.
    /// </summary>
    /// <exception cref="NanoQLException">A vector's length differs from the start dates'.</exception>
    public void Validate()
    {
        int n = StartDates.Length;
        Check(EndDates.Length == n, "end dates", EndDates.Length);
        Check(AccrualTimes.Length == n, "accrual times", AccrualTimes.Length);
        Check(Type == CapFloorType.Floor || CapRates.Length == n, "cap rates", CapRates.Length);
        Check(Type == CapFloorType.Cap || FloorRates.Length == n, "floor rates", FloorRates.Length);
        Check(Gearings.Length == n, "gearings", Gearings.Length);
        Check(Spreads.Length == n, "spreads", Spreads.Length);
        Check(Nominals.Length == n, "nominals", Nominals.Length);
        Check(Forwards.Length == n, "forwards", Forwards.Length);

        void Check(bool condition, string what, int size)
        {
            if (!condition)
            {
                throw new NanoQLException($"number of start dates ({n}) different from that of {what} ({size})");
            }
        }
    }
}
