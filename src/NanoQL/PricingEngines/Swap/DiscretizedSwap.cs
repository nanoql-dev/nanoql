// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2022 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swap/discretizedswap.hpp ql/pricingengines/swap/discretizedswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>A discretized fixed-versus-floating swap: its value on a lattice, its coupons added as the rollback reaches them.</summary>
/// <remarks>
/// <para>
/// A working type (<see cref="DiscretizedAsset"/>). Each coupon is added at its reset time, in the pre-adjustment or,
/// as its adjustment says, the post-adjustment: a fixed coupon as its amount times a discount bond from its payment
/// time, a floating one as the nominal times one less that bond, plus the nominal times its accrual time and spread
/// times the bond. A coupon whose reset time is before the reference date and whose payment isn't (or is on it, when
/// the context's <see cref="EvaluationContext.IncludeTodaysCashFlows"/> is <see langword="true"/>) is added in the
/// post-adjustment at its payment time instead, as its known amount; a floating one whose amount can't be computed is
/// refused there. The coupons' times are the given day counter's year fractions from the reference date.
/// </para>
/// <para>
/// <b>A floating coupon known by its fixing date</b> (the S-sr plan's SSR6 (xvi)): the public constructor, a swap's own
/// value as the tree swap engine prices it, knows a floating coupon whose fixing date is on or before the reference
/// date, adding its amount at its payment time, where QuantLib knows it by its accrual start (its reset date,
/// <c>vanillaswap.cpp:67</c>; <c>discretizedswap.cpp:84-87</c>), so between its fixing and its start the tree forecasts
/// a coupon whose fixing the discounting engine reads (<c>Divergences/TreeKnownCouponTests.cs</c>). The internal
/// constructor, which the discretized swaption calls, keeps QuantLib's rule, so a swaption exercised in that window
/// takes its floating leg at par, QuantLib's convention.
/// </para>
/// <para>
/// <b>Its discount bond.</b> QuantLib's swap builds a discount bond for each coupon it adds, initializes it at the
/// payment time and rolls it back to the swap's time. This swap keeps one bond, re-initialized for each coupon (a
/// discount bond has no adjustments, so the adjustment times its memo keeps skip nothing), whose buffers its
/// <see cref="Reset"/> reserves at the widest step of the swap's own rollback, from its initial step down to the
/// grid's start: every coupon's bond starts at or before the swap's initial time and rolls back to the swap's time, so
/// no coupon's bond allocates.
/// </para>
/// </remarks>
public sealed class DiscretizedSwap : DiscretizedAsset
{
    private readonly FixedVsFloatingSwapArguments arguments;
    private readonly double[] fixedResetTimes;
    private readonly double[] fixedPayTimes;
    private readonly CouponAdjustment[] fixedCouponAdjustments;
    private readonly bool[] fixedResetTimeIsInPast;
    private readonly double[] floatingResetTimes;
    private readonly double[] floatingPayTimes;
    private readonly CouponAdjustment[] floatingCouponAdjustments;
    private readonly bool[] floatingResetTimeIsInPast;
    private readonly DiscretizedDiscountBond bond = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscretizedSwap"/> class, every coupon added in the pre-adjustment,
    /// a floating one fixed on or before the reference date known by its amount.
    /// </summary>
    /// <param name="swap">The swap.</param>
    /// <param name="referenceDate">The date of time 0.</param>
    /// <param name="dayCounter">The day counter of the times.</param>
    /// <param name="context">
    /// The evaluation context: its <see cref="EvaluationContext.IncludeTodaysCashFlows"/>, and the fixings and curves
    /// the floating coupons' amounts read.
    /// </param>
    /// <exception cref="ArgumentNullException">The swap, the day counter or the context is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceDate"/> is <see langword="default"/>.</exception>
    public DiscretizedSwap(FixedVsFloatingSwap swap, Date referenceDate, DayCounter dayCounter, EvaluationContext context)
        : this(Arguments(swap, referenceDate, dayCounter, context), referenceDate, dayCounter, default, default, context, true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscretizedSwap"/> class with each coupon's adjustment: QuantLib's
    /// five-argument constructor, which the discretized swaption calls with its snapped swap's.
    /// </summary>
    /// <param name="arguments">The swap's arguments.</param>
    /// <param name="referenceDate">The date of time 0.</param>
    /// <param name="dayCounter">The day counter of the times.</param>
    /// <param name="fixedCouponAdjustments">Each fixed coupon's adjustment; <see langword="default"/> for all pre.</param>
    /// <param name="floatingCouponAdjustments">Each floating coupon's adjustment; <see langword="default"/> for all pre.</param>
    /// <param name="context">The evaluation context, whose <see cref="EvaluationContext.IncludeTodaysCashFlows"/> it reads.</param>
    /// <param name="knownByFixingDate">
    /// Whether a floating coupon is known by its fixing date, on or before the reference date; otherwise, as QuantLib's,
    /// by its accrual start before it.
    /// </param>
    /// <exception cref="NanoQLException">The adjustments aren't one per coupon.</exception>
    internal DiscretizedSwap(
        FixedVsFloatingSwapArguments arguments,
        Date referenceDate,
        DayCounter dayCounter,
        ImmutableArray<CouponAdjustment> fixedCouponAdjustments,
        ImmutableArray<CouponAdjustment> floatingCouponAdjustments,
        EvaluationContext context,
        bool knownByFixingDate)
    {
        this.arguments = arguments;
        this.fixedCouponAdjustments = fixedCouponAdjustments.IsDefault
            ? new CouponAdjustment[arguments.FixedPayDates.Length]
            : [.. fixedCouponAdjustments];
        this.floatingCouponAdjustments = floatingCouponAdjustments.IsDefault
            ? new CouponAdjustment[arguments.FloatingPayDates.Length]
            : [.. floatingCouponAdjustments];

        if (!(this.fixedCouponAdjustments.Length == arguments.FixedPayDates.Length))
        {
            throw new NanoQLException(
                "The fixed coupon adjustments must have the same size as the number of fixed coupons.");
        }

        if (!(this.floatingCouponAdjustments.Length == arguments.FloatingPayDates.Length))
        {
            throw new NanoQLException(
                "The floating coupon adjustments must have the same size as the number of floating coupons.");
        }

        bool includeTodaysCashFlows = context.IncludeTodaysCashFlows == true;

        int nrOfFixedCoupons = arguments.FixedResetDates.Length;
        fixedResetTimes = new double[nrOfFixedCoupons];
        fixedPayTimes = new double[nrOfFixedCoupons];
        fixedResetTimeIsInPast = new bool[nrOfFixedCoupons];
        for (int i = 0; i < nrOfFixedCoupons; ++i)
        {
            double resetTime = dayCounter.YearFraction(referenceDate, arguments.FixedResetDates[i]);
            double payTime = dayCounter.YearFraction(referenceDate, arguments.FixedPayDates[i]);
            bool resetIsInPast = IsResetTimeInPast(resetTime, payTime, includeTodaysCashFlows);

            fixedResetTimes[i] = resetTime;
            fixedPayTimes[i] = payTime;
            fixedResetTimeIsInPast[i] = resetIsInPast;
            if (resetIsInPast)
            {
                this.fixedCouponAdjustments[i] = CouponAdjustment.Post;
            }
        }

        int nrOfFloatingCoupons = arguments.FloatingResetDates.Length;
        floatingResetTimes = new double[nrOfFloatingCoupons];
        floatingPayTimes = new double[nrOfFloatingCoupons];
        floatingResetTimeIsInPast = new bool[nrOfFloatingCoupons];
        for (int i = 0; i < nrOfFloatingCoupons; ++i)
        {
            double resetTime = dayCounter.YearFraction(referenceDate, arguments.FloatingResetDates[i]);
            double payTime = dayCounter.YearFraction(referenceDate, arguments.FloatingPayDates[i]);
            bool resetIsInPast = knownByFixingDate
                ? IsFixed(dayCounter.YearFraction(referenceDate, arguments.FloatingFixingDates[i]), payTime, includeTodaysCashFlows)
                : IsResetTimeInPast(resetTime, payTime, includeTodaysCashFlows);

            floatingResetTimes[i] = resetTime;
            floatingPayTimes[i] = payTime;
            floatingResetTimeIsInPast[i] = resetIsInPast;
            if (resetIsInPast)
            {
                this.floatingCouponAdjustments[i] = CouponAdjustment.Post;
            }
        }
    }

    /// <summary>
    /// Sets the values to 0 and adjusts them; on a tree lattice, it first reserves its discount bond's buffers at the
    /// widest step from the swap's time down to the grid's start.
    /// </summary>
    /// <param name="size">The number of values.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    /// <exception cref="NanoQLException">The swap isn't initialized, or a past floating coupon's amount isn't known.</exception>
    public override void Reset(int size)
    {
        if (Method is TreeLattice lattice)
        {
            int widest = 0;
            for (int k = 0, i = lattice.TimeGrid.Index(Time); k <= i; k++)
            {
                int width = lattice.Size(k);
                if (width > widest)
                {
                    widest = width;
                }
            }

            bond.ReserveCapacity(widest);
        }

        ResetValues(size, 0.0);
        AdjustValues();
    }

    /// <summary>The swap's mandatory times: the non-negative fixed reset, fixed payment, floating reset and floating payment times, in that order.</summary>
    /// <returns>The times, not guaranteed to be sorted.</returns>
    public override ImmutableArray<double> MandatoryTimes()
    {
        var times = ImmutableArray.CreateBuilder<double>();
        foreach (double t in fixedResetTimes)
        {
            if (t >= 0.0)
            {
                times.Add(t);
            }
        }

        foreach (double t in fixedPayTimes)
        {
            if (t >= 0.0)
            {
                times.Add(t);
            }
        }

        foreach (double t in floatingResetTimes)
        {
            if (t >= 0.0)
            {
                times.Add(t);
            }
        }

        foreach (double t in floatingPayTimes)
        {
            if (t >= 0.0)
            {
                times.Add(t);
            }
        }

        return times.ToImmutable();
    }

    /// <summary>Adds the coupons adjusted before an option looks at the values, floating then fixed, whose reset is now.</summary>
    protected override void PreAdjustValuesImpl()
    {
        // floating payments
        for (int i = 0; i < floatingResetTimes.Length; i++)
        {
            double t = floatingResetTimes[i];
            if (floatingCouponAdjustments[i] == CouponAdjustment.Pre && !floatingResetTimeIsInPast[i] && t >= 0.0 && IsOnTime(t))
            {
                AddFloatingCoupon(i);
            }
        }

        // fixed payments
        for (int i = 0; i < fixedResetTimes.Length; i++)
        {
            double t = fixedResetTimes[i];
            if (fixedCouponAdjustments[i] == CouponAdjustment.Pre && t >= 0.0 && IsOnTime(t))
            {
                AddFixedCoupon(i);
            }
        }
    }

    /// <summary>
    /// Adds the coupons adjusted after an option looks at the values, floating then fixed, whose reset is now; then
    /// the known amounts of the coupons whose reset is past and whose payment is now.
    /// </summary>
    /// <exception cref="NanoQLException">A past floating coupon's amount isn't known.</exception>
    protected override void PostAdjustValuesImpl()
    {
        // floating payments
        for (int i = 0; i < floatingResetTimes.Length; i++)
        {
            double t = floatingResetTimes[i];
            if (floatingCouponAdjustments[i] == CouponAdjustment.Post && !floatingResetTimeIsInPast[i] && t >= 0.0 && IsOnTime(t))
            {
                AddFloatingCoupon(i);
            }
        }

        // fixed payments
        for (int i = 0; i < fixedResetTimes.Length; i++)
        {
            double t = fixedResetTimes[i];
            if (fixedCouponAdjustments[i] == CouponAdjustment.Post && t >= 0.0 && IsOnTime(t))
            {
                AddFixedCoupon(i);
            }
        }

        // fixed coupons whose reset time is in the past won't be managed in PreAdjustValues()
        for (int i = 0; i < fixedPayTimes.Length; i++)
        {
            double t = fixedPayTimes[i];
            if (fixedResetTimeIsInPast[i] && IsOnTime(t))
            {
                double fixedCoupon = arguments.FixedCoupons[i];
                Span<double> values = Values;
                for (int j = 0; j < values.Length; j++)
                {
                    values[j] = arguments.Type == SwapType.Payer ? values[j] - fixedCoupon : values[j] + fixedCoupon;
                }
            }
        }

        // the same applies to floating payments whose rate is already fixed
        for (int i = 0; i < floatingPayTimes.Length; i++)
        {
            double t = floatingPayTimes[i];
            if (floatingResetTimeIsInPast[i] && IsOnTime(t))
            {
                double currentFloatingCoupon = arguments.FloatingCoupons[i]
                    ?? throw new NanoQLException("current floating coupon not given");
                Span<double> values = Values;
                for (int j = 0; j < values.Length; j++)
                {
                    values[j] = arguments.Type == SwapType.Payer
                        ? values[j] + currentFloatingCoupon
                        : values[j] - currentFloatingCoupon;
                }
            }
        }
    }

    private static bool IsResetTimeInPast(double resetTime, double payTime, bool includeTodaysCashFlows) =>
        resetTime < 0.0 && (payTime > 0.0 || (includeTodaysCashFlows && payTime == 0.0));

    // SSR6 (xvi): a floating coupon fixed on or before the reference date, whose payment isn't past.
    private static bool IsFixed(double fixingTime, double payTime, bool includeTodaysCashFlows) =>
        fixingTime <= 0.0 && (payTime > 0.0 || (includeTodaysCashFlows && payTime == 0.0));

    // The public constructor's arguments, checked in its parameters' order.
    private static FixedVsFloatingSwapArguments Arguments(
        FixedVsFloatingSwap swap, Date referenceDate, DayCounter dayCounter, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(swap);
        Date.ThrowIfDefault(referenceDate, nameof(referenceDate));
        ArgumentNullException.ThrowIfNull(dayCounter);
        return FixedVsFloatingSwapArguments.Of(swap, context);
    }

    private void AddFixedCoupon(int i)
    {
        RollBond(fixedPayTimes[i]);

        double fixedCoupon = arguments.FixedCoupons[i];
        Span<double> values = Values;
        ReadOnlySpan<double> bondValues = bond.Values;
        for (int j = 0; j < values.Length; j++)
        {
            double coupon = fixedCoupon * bondValues[j];
            if (arguments.Type == SwapType.Payer)
            {
                values[j] -= coupon;
            }
            else
            {
                values[j] += coupon;
            }
        }
    }

    private void AddFloatingCoupon(int i)
    {
        RollBond(floatingPayTimes[i]);

        double nominal = arguments.Nominal ?? throw new NanoQLException("non-constant nominals are not supported yet");

        double T = arguments.FloatingAccrualTimes[i];
        double spread = arguments.FloatingSpreads[i];
        double accruedSpread = nominal * T * spread;
        Span<double> values = Values;
        ReadOnlySpan<double> bondValues = bond.Values;
        for (int j = 0; j < values.Length; j++)
        {
            double coupon = (nominal * (1.0 - bondValues[j])) + (accruedSpread * bondValues[j]);
            if (arguments.Type == SwapType.Payer)
            {
                values[j] += coupon;
            }
            else
            {
                values[j] -= coupon;
            }
        }
    }

    // QuantLib's DiscretizedDiscountBond, initialized at the payment time on the swap's lattice and rolled back to the
    // swap's time: here the swap's one bond, re-initialized.
    private void RollBond(double payTime)
    {
        // The swap is on time, so it is initialized.
        bond.Initialize(Method!, payTime);
        bond.Rollback(Time);
    }
}
