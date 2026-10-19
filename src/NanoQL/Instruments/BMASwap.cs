// SPDX-FileCopyrightText: Copyright (C) 2007 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bmaswap.hpp ql/instruments/bmaswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A swap paying Libor against BMA coupons.</summary>
/// <remarks>
/// <para>
/// The type, payer or receiver, refers to the BMA leg. The Libor leg is the first leg, Ibor coupons geared by the Libor
/// fraction with the Libor spread; the BMA leg is the second, average-BMA coupons. Each leg's payments follow its
/// schedule's convention.
/// </para>
/// <para>
/// The constructor builds the Libor leg with the index's default pricer, which reads the context's at-par switch, so it
/// takes the context. It refuses a type that isn't a member of <see cref="SwapType"/> on entry, before it builds the
/// legs, where QuantLib refuses it after them. An engine's results are a <see cref="BMASwapResults"/>, with QuantLib's
/// inspectors derived from the swap's results.
/// </para>
/// </remarks>
public sealed class BMASwap : Swap
{
    private const double BasisPoint = 1.0e-4;

    /// <summary>Initializes a new instance of the <see cref="BMASwap"/> class.</summary>
    /// <param name="type">Whether the BMA leg is paid or received.</param>
    /// <param name="nominal">The nominal of both legs.</param>
    /// <param name="liborSchedule">The Libor leg's schedule.</param>
    /// <param name="liborFraction">The gearing of the Libor fixings.</param>
    /// <param name="liborSpread">The spread over the Libor leg.</param>
    /// <param name="liborIndex">The Libor leg's index.</param>
    /// <param name="liborDayCount">The Libor leg's day counter; the index's when <see langword="null"/>, QuantLib's
    /// empty day counter.</param>
    /// <param name="bmaSchedule">The BMA leg's schedule.</param>
    /// <param name="bmaIndex">The BMA index.</param>
    /// <param name="bmaDayCount">The BMA leg's day counter; the index's when <see langword="null"/>, QuantLib's empty
    /// day counter.</param>
    /// <param name="context">The evaluation context, whose at-par switch the Libor leg reads.</param>
    /// <exception cref="ArgumentNullException">An argument but a day counter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">A leg can't be built from its arguments, a zero gearing among them.</exception>
    /// <exception cref="NanoQLException">
    /// A leg can't be built from its arguments, an empty schedule among them.
    /// </exception>
    public BMASwap(
        SwapType type,
        double nominal,
        Schedule liborSchedule,
        double liborFraction,
        double liborSpread,
        IborIndex liborIndex,
        DayCounter? liborDayCount,
        Schedule bmaSchedule,
        BMAIndex bmaIndex,
        DayCounter? bmaDayCount,
        EvaluationContext context)
        : base(
            BuildLegs(
                type,
                nominal,
                liborSchedule,
                liborFraction,
                liborSpread,
                liborIndex,
                liborDayCount,
                bmaSchedule,
                bmaIndex,
                bmaDayCount,
                context),
            [type == SwapType.Receiver, type == SwapType.Payer])
    {
        Type = type;
        Nominal = nominal;
        LiborFraction = liborFraction;
        LiborSpread = liborSpread;
    }

    /// <summary>Gets the gearing of the Libor fixings.</summary>
    public double LiborFraction { get; }

    /// <summary>Gets the spread over the Libor leg.</summary>
    public double LiborSpread { get; }

    /// <summary>Gets the nominal of both legs.</summary>
    public double Nominal { get; }

    /// <summary>Gets whether the BMA leg is paid or received.</summary>
    public SwapType Type { get; }

    /// <summary>Gets the BMA leg.</summary>
    public ImmutableArray<CashFlow> BmaLeg => Legs[1];

    /// <summary>Gets the Libor leg.</summary>
    public ImmutableArray<CashFlow> LiborLeg => Legs[0];

    /// <inheritdoc/>
    /// <remarks>
    /// An expired swap has zero leg NPVs and BPS, so no fair Libor fraction, and a fair Libor spread of NaN, the
    /// quotient of zeros QuantLib computes.
    /// </remarks>
    public override BMASwapResults SetupExpired() => Derive(base.SetupExpired());

    /// <inheritdoc/>
    /// <remarks>
    /// The fair Libor fraction and spread are derived from the swap's results, as QuantLib derives them.
    /// </remarks>
    public override BMASwapResults FetchResults(InstrumentResults results) => Derive(base.FetchResults(results));

    // QuantLib builds the legs in its constructor's body; here they are built before the base constructor takes them,
    // in QuantLib's order, after the context and the type are checked.
    private static ImmutableArray<CashFlow>[] BuildLegs(
        SwapType type,
        double nominal,
        Schedule liborSchedule,
        double liborFraction,
        double liborSpread,
        IborIndex liborIndex,
        DayCounter? liborDayCount,
        Schedule bmaSchedule,
        BMAIndex bmaIndex,
        DayCounter? bmaDayCount,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (type is not (SwapType.Payer or SwapType.Receiver))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown BMA-swap type");
        }

        ArgumentNullException.ThrowIfNull(liborSchedule);
        if (liborIndex is null)
        {
            throw new ArgumentNullException(nameof(liborIndex), "no index provided");
        }

        BusinessDayConvention convention = liborSchedule.BusinessDayConvention;

        ImmutableArray<CashFlow> liborLeg = new IborLeg(liborSchedule, liborIndex)
            .WithNotionals(nominal)
            .WithPaymentDayCounter(liborDayCount)
            .WithPaymentAdjustment(convention)
            .WithFixingDays(liborIndex.FixingDays)
            .WithGearings(liborFraction)
            .WithSpreads(liborSpread)
            .ToLeg(context);

        ArgumentNullException.ThrowIfNull(bmaSchedule);

        // QuantLib's coupons dereference the BMA index before any check, which is undefined for a null index.
        if (bmaIndex is null)
        {
            throw new ArgumentNullException(nameof(bmaIndex), "no index provided");
        }

        BusinessDayConvention bmaConvention = bmaSchedule.BusinessDayConvention;

        ImmutableArray<CashFlow> bmaLeg = new AverageBMALeg(bmaSchedule, bmaIndex)
            .WithNotionals(nominal)
            .WithPaymentDayCounter(bmaDayCount)
            .WithPaymentAdjustment(bmaConvention)
            .ToLeg();

        return [liborLeg, bmaLeg];
    }

    // QuantLib's result inspectors, each null where QuantLib's refuses to read.
    private BMASwapResults Derive(SwapResults r)
    {
        double? liborLegBPS = r.LegBPS[0];
        double? liborLegNPV = r.LegNPV[0];
        double? bmaLegNPV = r.LegNPV[1];

        double? fairLiborFraction = null;
        if (liborLegBPS is double bps && liborLegNPV is double npv)
        {
            double spreadNPV = (LiborSpread / BasisPoint) * bps;
            double pureLiborNPV = npv - spreadNPV;
            if (pureLiborNPV != 0.0 && bmaLegNPV is double bmaNPV)
            {
                fairLiborFraction = -LiborFraction * (bmaNPV + spreadNPV) / pureLiborNPV;
            }
        }

        double? fairLiborSpread = r.NPV is double value && liborLegBPS is double liborBPS
            ? LiborSpread - (value / (liborBPS / BasisPoint))
            : null;

        return r is BMASwapResults fetched
            ? fetched with { FairLiborFraction = fairLiborFraction, FairLiborSpread = fairLiborSpread }
            : new BMASwapResults(r) { FairLiborFraction = fairLiborFraction, FairLiborSpread = fairLiborSpread };
    }
}
