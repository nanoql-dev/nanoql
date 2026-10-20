// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/inflationcapfloor.hpp ql/instruments/inflationcapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// What QuantLib's <c>YoYInflationCapFloor::setupArguments</c> computes for the year-on-year cap and floor engines: the
/// instrument's type and, per coupon, its dates, accrual time, nominal, gearing, spread and effective strikes; with
/// each coupon's accrual end, observation lag and interpolation, from which the engines observe its forward.
/// </summary>
/// <remarks>
/// nanoql's caps hold no arguments object: the engines compute these from the leg with the context
/// (<see cref="YoYInflationCapFloor.SetupArguments"/>, <c>inflationcapfloor.cpp:133-179</c>), then check them with
/// <see cref="Validate"/>, as QuantLib's <c>Instrument::calculate</c> does. A cap rate or a floor rate QuantLib sets to
/// <c>Null&lt;Rate&gt;()</c>, the strikes of the other side, is <see langword="null"/>. QuantLib's <c>index</c> and
/// <c>observationLag</c> members, which its <c>setupArguments</c> never sets, aren't ported; the coupons' accrual ends,
/// lags and interpolations are nanoql's, for the engines' forwards (a documented divergence: QuantLib's engines read
/// the index's curve at the fixing date).
/// </remarks>
internal sealed class YoYInflationCapFloorArguments
{
    private YoYInflationCapFloorArguments()
    {
    }

    /// <summary>Gets the instrument's type.</summary>
    public required YoYInflationCapFloorType Type { get; init; }

    /// <summary>Gets the coupons' accrual start dates.</summary>
    public required ImmutableArray<Date> StartDates { get; init; }

    /// <summary>Gets the coupons' fixing dates.</summary>
    public required ImmutableArray<Date> FixingDates { get; init; }

    /// <summary>Gets the coupons' payment dates.</summary>
    public required ImmutableArray<Date> PayDates { get; init; }

    /// <summary>Gets the coupons' accrual periods.</summary>
    public required ImmutableArray<double> AccrualTimes { get; init; }

    /// <summary>Gets the effective cap rates, net of spread and gearing, or <see langword="null"/> for a
    /// floor.</summary>
    public required ImmutableArray<double?> CapRates { get; init; }

    /// <summary>Gets the effective floor rates, net of spread and gearing, or <see langword="null"/> for a
    /// cap.</summary>
    public required ImmutableArray<double?> FloorRates { get; init; }

    /// <summary>Gets the coupons' gearings.</summary>
    public required ImmutableArray<double> Gearings { get; init; }

    /// <summary>Gets the coupons' spreads.</summary>
    public required ImmutableArray<double> Spreads { get; init; }

    /// <summary>Gets the coupons' nominals.</summary>
    public required ImmutableArray<double> Nominals { get; init; }

    /// <summary>Gets the coupons' accrual end dates, on which their fixings are observed with the lag.</summary>
    public required ImmutableArray<Date> AccrualEndDates { get; init; }

    /// <summary>Gets the coupons' observation lags.</summary>
    public required ImmutableArray<Period> ObservationLags { get; init; }

    /// <summary>Gets how the coupons observe the index.</summary>
    public required ImmutableArray<CPIInterpolationType> Interpolations { get; init; }

    /// <summary>The arguments of a year-on-year cap or floor, as QuantLib's <c>setupArguments</c> computes
    /// them.</summary>
    /// <param name="type">The instrument's type.</param>
    /// <param name="yoyLeg">The year-on-year leg.</param>
    /// <param name="capRates">The cap rates, extended to the leg's length.</param>
    /// <param name="floorRates">The floor rates, extended to the leg's length.</param>
    /// <param name="context">The evaluation context, which the coupons' nominals take.</param>
    /// <returns>The arguments.</returns>
    /// <exception cref="NanoQLException">A cash flow isn't a year-on-year inflation coupon.</exception>
    internal static YoYInflationCapFloorArguments Of(
        YoYInflationCapFloorType type,
        ImmutableArray<CashFlow> yoyLeg,
        ImmutableArray<double> capRates,
        ImmutableArray<double> floorRates,
        EvaluationContext context)
    {
        int n = yoyLeg.Length;
        var startDates = new Date[n];
        var fixingDates = new Date[n];
        var payDates = new Date[n];
        var accrualTimes = new double[n];
        var nominals = new double[n];
        var gearings = new double[n];
        var caps = new double?[n];
        var floors = new double?[n];
        var spreads = new double[n];
        var accrualEnds = new Date[n];
        var lags = new Period[n];
        var interpolations = new CPIInterpolationType[n];

        for (int i = 0; i < n; ++i)
        {
            // QuantLib's dynamic_pointer_cast and its check, at pricing: a leg holding another cash flow builds.
            var coupon = yoyLeg[i] as YoYInflationCoupon ?? throw new NanoQLException("non-YoYInflationCoupon given");
            startDates[i] = coupon.AccrualStartDate;
            fixingDates[i] = coupon.FixingDate();
            payDates[i] = coupon.Date;

            // this is passed explicitly for precision
            accrualTimes[i] = coupon.AccrualPeriod();

            nominals[i] = coupon.Nominal(context);
            double spread = coupon.Spread;
            double gearing = coupon.Gearing;
            gearings[i] = gearing;
            spreads[i] = spread;

            caps[i] = type is YoYInflationCapFloorType.Cap or YoYInflationCapFloorType.Collar
                ? (capRates[i] - spread) / gearing
                : null;
            floors[i] = type is YoYInflationCapFloorType.Floor or YoYInflationCapFloorType.Collar
                ? (floorRates[i] - spread) / gearing
                : null;

            accrualEnds[i] = coupon.AccrualEndDate;
            lags[i] = coupon.ObservationLag;
            interpolations[i] = coupon.Interpolation;
        }

        return new YoYInflationCapFloorArguments
        {
            Type = type,
            StartDates = [.. startDates],
            FixingDates = [.. fixingDates],
            PayDates = [.. payDates],
            AccrualTimes = [.. accrualTimes],
            CapRates = [.. caps],
            FloorRates = [.. floors],
            Gearings = [.. gearings],
            Spreads = [.. spreads],
            Nominals = [.. nominals],
            AccrualEndDates = [.. accrualEnds],
            ObservationLags = [.. lags],
            Interpolations = [.. interpolations],
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
        Check(PayDates.Length == n, "pay dates", PayDates.Length);
        Check(AccrualTimes.Length == n, "accrual times", AccrualTimes.Length);
        Check(Type == YoYInflationCapFloorType.Floor || CapRates.Length == n, "cap rates", CapRates.Length);
        Check(Type == YoYInflationCapFloorType.Cap || FloorRates.Length == n, "floor rates", FloorRates.Length);
        Check(Gearings.Length == n, "gearings", Gearings.Length);
        Check(Spreads.Length == n, "spreads", Spreads.Length);
        Check(Nominals.Length == n, "nominals", Nominals.Length);

        void Check(bool condition, string what, int size)
        {
            if (!condition)
            {
                throw new NanoQLException($"number of start dates ({n}) different from that of {what} ({size})");
            }
        }
    }
}
