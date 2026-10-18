// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/vanillaswap.hpp ql/instruments/vanillaswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>A plain-vanilla swap: a fixed leg against an Ibor leg.</summary>
/// <remarks>
/// <para>
/// If no payment convention is passed, the convention of the floating-rate schedule is used.
/// </para>
/// <para>
/// The constructor takes the context because its Ibor leg reads the context's
/// <see cref="EvaluationContext.UseAtParCoupons"/> when no coupon choice is given, as QuantLib's reads its at-par
/// switch when the leg's pricer is built; the swap keeps the choice it was built with.
/// </para>
/// <para>
/// When the context's <see cref="EvaluationContext.IncludeReferenceDateEvents"/> is <see langword="true"/>, payments
/// occurring at the settlement date of the swap might be included in the NPV and therefore affect the fair-rate and
/// fair-spread calculation. This might not be what you want.
/// </para>
/// </remarks>
public sealed class VanillaSwap : FixedVsFloatingSwap
{
    /// <summary>Initializes a new instance of the <see cref="VanillaSwap"/> class.</summary>
    /// <param name="type">Whether the fixed rate is paid or received.</param>
    /// <param name="nominal">The nominal of both legs.</param>
    /// <param name="fixedSchedule">The fixed leg's schedule.</param>
    /// <param name="fixedRate">The fixed rate.</param>
    /// <param name="fixedDayCount">The fixed leg's day counter; the index's when <see langword="null"/>.</param>
    /// <param name="floatSchedule">The floating leg's schedule.</param>
    /// <param name="iborIndex">The floating leg's index.</param>
    /// <param name="spread">The floating leg's spread.</param>
    /// <param name="floatingDayCount">
    /// The floating coupons' day counter; the index's when <see langword="null"/>, QuantLib's empty day counter.
    /// </param>
    /// <param name="context">The evaluation context, whose at-par switch the floating leg reads when
    /// <paramref name="useIndexedCoupons"/> is <see langword="null"/>.</param>
    /// <param name="paymentConvention">
    /// The payment convention; the floating schedule's convention when <see langword="null"/>.
    /// </param>
    /// <param name="useIndexedCoupons">
    /// Whether the floating coupons use indexed coupons rather than par coupons; the context's switch when
    /// <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument but a day counter is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The type or the payment convention isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">A schedule is empty.</exception>
    /// <exception cref="NanoQLException">A leg can't be built from the arguments.</exception>
    public VanillaSwap(
        SwapType type,
        double nominal,
        Schedule fixedSchedule,
        double fixedRate,
        DayCounter? fixedDayCount,
        Schedule floatSchedule,
        IborIndex iborIndex,
        double spread,
        DayCounter? floatingDayCount,
        EvaluationContext context,
        BusinessDayConvention? paymentConvention = null,
        bool? useIndexedCoupons = null)
        : base(
            EntryChecked(type, context, floatSchedule, iborIndex),
            [nominal],
            fixedSchedule,
            fixedRate,
            fixedDayCount,
            [nominal],
            floatSchedule,
            iborIndex,
            spread,
            floatingDayCount,
            paymentConvention,
            0,
            null,
            () => FloatingLegOf(
                nominal,
                floatSchedule,
                iborIndex,
                spread,
                floatingDayCount,
                context,
                paymentConvention,
                useIndexedCoupons),
            nameof(fixedSchedule),
            nameof(paymentConvention))
    {
    }

    // The checks on entry, in the first argument so that they run before the base's: the context first, as every
    // member that takes it checks it, then the floating schedule and the index under this constructor's names.
    private static SwapType EntryChecked(
        SwapType type, EvaluationContext context, Schedule floatSchedule, IborIndex iborIndex)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(floatSchedule);
        CheckFloatingIndex(iborIndex, nameof(iborIndex));
        return type;
    }

    // QuantLib's constructor body: the floating leg, which the base builds once it has checked its terms and built the
    // fixed leg, with QuantLib's setters in QuantLib's order.
    private static ImmutableArray<CashFlow> FloatingLegOf(
        double nominal,
        Schedule floatSchedule,
        IborIndex iborIndex,
        double spread,
        DayCounter? floatingDayCount,
        EvaluationContext context,
        BusinessDayConvention? paymentConvention,
        bool? useIndexedCoupons)
    {
        if (floatSchedule.Empty)
        {
            // QuantLib's period count wraps below zero for an empty schedule: refused as the Ibor leg's builder refuses
            // it, under this constructor's name.
            throw new ArgumentException("empty schedule", nameof(floatSchedule));
        }

        IborLeg leg = new IborLeg(floatSchedule, iborIndex)
            .WithNotionals(nominal);
        if (floatingDayCount is not null)
        {
            leg = leg.WithPaymentDayCounter(floatingDayCount);
        }

        BusinessDayConvention convention =
            ResolvePaymentConvention(paymentConvention, floatSchedule, nameof(paymentConvention));
        return leg
            .WithPaymentAdjustment(convention)
            .WithSpreads(spread)
            .WithIndexedCoupons(useIndexedCoupons)
            .ToLeg(context);
    }
}
