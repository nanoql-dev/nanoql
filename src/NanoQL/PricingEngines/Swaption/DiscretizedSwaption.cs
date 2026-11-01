// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2021, 2022 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/swaption/discretizedswaption.hpp ql/pricingengines/swaption/discretizedswaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>A discretized swaption: an option, exercised at its exercise times, on a discretized swap.</summary>
/// <remarks>
/// <para>
/// A working type (<see cref="DiscretizedAsset"/>). Date adjustments can get the swap's times out of step with the
/// exercise times, which could misprice the swaption, so the swap's schedule dates in the week before an exercise
/// date (but the last) are snapped forward to it, for each exercise date in turn, a later one comparing against the
/// dates earlier ones snapped; a coupon whose date is snapped is adjusted in the swap's post-adjustment. The underlying
/// is a <see cref="VanillaSwap"/> on the snapped schedules, a <see cref="NullCalendar"/>'s, and on the swap's index (an
/// overnight-indexed swap's index too), so its Ibor coupons read the context's at-par switch.
/// </para>
/// <para>
/// <b>The snapping, fixed</b> (the S-cms plan's CMR4): QuantLib also snaps a date in the week after an exercise date
/// back to it (<c>discretizedswaption.cpp:93-111</c>), so a swap starting on its usual two-day lag after the exercise
/// is valued from the exercise date, its first coupon accruing days it doesn't; and its snapped swap pays every coupon
/// on its <see cref="NullCalendar"/> schedule's dates (<c>:113-119</c>), without the swap's payment calendar,
/// convention or lag. Here no date after an exercise date is snapped, and each coupon pays on the swap's own payment
/// date, a snapped one on the swap's payment rule (its payment calendar, lag and convention) applied to its snapped
/// date; with QuantLib's checks, Jamshidian's and the finite-difference engines and QuantLib 1.25's cached tree values
/// (<c>Divergences/TreeSwaptionSnappingTests.cs</c>). A swap without a lag on business-day schedules keeps QuantLib's
/// values wherever nothing snaps.
/// </para>
/// <para>
/// Its <see cref="Reset"/> initializes the underlying at the last payment time on the swaption's lattice, then resets
/// the option.
/// </para>
/// </remarks>
[WorkingType]
public sealed class DiscretizedSwaption : DiscretizedOption
{
    private readonly double lastPayment;

    /// <summary>Initializes a new instance of the <see cref="DiscretizedSwaption"/> class.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="referenceDate">The date of time 0.</param>
    /// <param name="dayCounter">The day counter of the times.</param>
    /// <param name="context">
    /// The evaluation context: the at-par switch of the snapped swap's Ibor leg, the fixings and curves its coupons'
    /// amounts read, and its <see cref="EvaluationContext.IncludeTodaysCashFlows"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">The swaption, the day counter or the context is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="referenceDate"/> is <see langword="default"/>.</exception>
    /// <exception cref="NanoQLException">The swap's nominal isn't constant, or the snapped swap can't be built.</exception>
    public DiscretizedSwaption(Swaption swaption, Date referenceDate, DayCounter dayCounter, EvaluationContext context)
        : this(Prepare(swaption, referenceDate, dayCounter, context))
    {
    }

    private DiscretizedSwaption(Parts parts)
        : base(parts.Underlying, parts.ExerciseType, parts.ExerciseTimes)
    {
        lastPayment = parts.LastPayment;
    }

    /// <summary>
    /// Initializes the underlying swap at the last payment time on the swaption's lattice, then sets the values to 0
    /// and adjusts them.
    /// </summary>
    /// <param name="size">The number of values.</param>
    /// <exception cref="NanoQLException">The swaption isn't initialized.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public override void Reset(int size)
    {
        // QuantLib initializes the underlying on its null method_ here.
        Lattice method = Method ?? throw new NanoQLException("asset not initialized");
        Underlying.Initialize(method, lastPayment);
        base.Reset(size);
    }

    // What QuantLib's constructor body computes before its members are set: the snapped swap, its asset, the exercise
    // times and the last payment time.
    private static Parts Prepare(Swaption swaption, Date referenceDate, DayCounter dayCounter, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(swaption);
        Date.ThrowIfDefault(referenceDate, nameof(referenceDate));
        ArgumentNullException.ThrowIfNull(dayCounter);

        (FixedVsFloatingSwapArguments snappedArgs, ImmutableArray<CouponAdjustment> fixedCouponAdjustments,
            ImmutableArray<CouponAdjustment> floatingCouponAdjustments) = PrepareSwaptionWithSnappedDates(swaption, context);

        Exercise exercise = swaption.Exercise;
        double[] exerciseTimes = new double[exercise.Dates.Length];
        for (int i = 0; i < exerciseTimes.Length; ++i)
        {
            exerciseTimes[i] = dayCounter.YearFraction(referenceDate, exercise.Date(i));
        }

        double lastFixedPayment = dayCounter.YearFraction(referenceDate, snappedArgs.FixedPayDates[^1]);
        double lastFloatingPayment = dayCounter.YearFraction(referenceDate, snappedArgs.FloatingPayDates[^1]);

        // std::max
        double lastPayment = lastFixedPayment < lastFloatingPayment ? lastFloatingPayment : lastFixedPayment;

        var underlying = new DiscretizedSwap(
            snappedArgs, referenceDate, dayCounter, fixedCouponAdjustments, floatingCouponAdjustments, context, false);
        return new Parts(underlying, exercise.Type, exerciseTimes, lastPayment);
    }

    private static (FixedVsFloatingSwapArguments SnappedArgs, ImmutableArray<CouponAdjustment> Fixed,
        ImmutableArray<CouponAdjustment> Floating) PrepareSwaptionWithSnappedDates(
            Swaption swaption, EvaluationContext context)
    {
        FixedVsFloatingSwap swap = swaption.Underlying;
        Date[] fixedDates = [.. swap.FixedSchedule.Dates];
        Date[] floatDates = [.. swap.FloatingSchedule.Dates];

        var fixedCouponAdjustments = new CouponAdjustment[swap.FixedLeg.Length];
        var floatingCouponAdjustments = new CouponAdjustment[swap.FloatingLeg.Length];

        foreach (Date exerciseDate in swaption.Exercise.Dates)
        {
            // CMR4: QuantLib snaps a date within one week either side; a date after the exercise isn't snapped
            for (int j = 0; j < fixedDates.Length - 1; j++)
            {
                Date unadjustedDate = fixedDates[j];
                if (exerciseDate != unadjustedDate && WithinPreviousWeek(exerciseDate, unadjustedDate))
                {
                    fixedDates[j] = exerciseDate;
                    fixedCouponAdjustments[j] = CouponAdjustment.Post;
                }
            }

            for (int j = 0; j < floatDates.Length - 1; j++)
            {
                Date unadjustedDate = floatDates[j];
                if (exerciseDate != unadjustedDate && WithinPreviousWeek(exerciseDate, unadjustedDate))
                {
                    floatDates[j] = exerciseDate;
                    floatingCouponAdjustments[j] = CouponAdjustment.Post;
                }
            }
        }

        var snappedFixedSchedule = new Schedule(fixedDates);
        var snappedFloatSchedule = new Schedule(floatDates);

        var snappedSwap = new VanillaSwap(
            swap.Type,
            swap.Nominal(),
            snappedFixedSchedule,
            swap.FixedRate,
            swap.FixedDayCount,
            snappedFloatSchedule,
            swap.IborIndex,
            swap.Spread,
            swap.FloatingDayCount,
            context,
            swap.PaymentConvention);

        // QuantLib's snapped swaption's arguments: the snapped swap's, with the swaption's exercise and settlement;
        // CMR4: each coupon paid on the swap's own payment date, a snapped one on the swap's rule from its snapped date
        FixedVsFloatingSwapArguments snappedArgs = FixedVsFloatingSwapArguments.Of(snappedSwap, context);
        FixedVsFloatingSwapArguments own = FixedVsFloatingSwapArguments.Of(swap, context);
        Calendar fixedPaymentCalendar = swap.PaymentCalendar ?? swap.FixedSchedule.Calendar ?? EmptyCalendar.Instance;
        Calendar floatingPaymentCalendar =
            swap.PaymentCalendar ?? swap.FloatingSchedule.Calendar ?? EmptyCalendar.Instance;
        var fixedPayDates = new Date[snappedArgs.FixedPayDates.Length];
        for (int j = 0; j < fixedPayDates.Length; j++)
        {
            fixedPayDates[j] = fixedDates[j + 1] == swap.FixedSchedule.Dates[j + 1]
                ? own.FixedPayDates[j]
                : fixedPaymentCalendar.Advance(
                    fixedDates[j + 1], swap.PaymentLag, TimeUnit.Days, swap.PaymentConvention);
        }

        var floatingPayDates = new Date[snappedArgs.FloatingPayDates.Length];
        for (int j = 0; j < floatingPayDates.Length; j++)
        {
            floatingPayDates[j] = floatDates[j + 1] == swap.FloatingSchedule.Dates[j + 1]
                ? own.FloatingPayDates[j]
                : floatingPaymentCalendar.Advance(
                    floatDates[j + 1], swap.PaymentLag, TimeUnit.Days, swap.PaymentConvention);
        }

        return (snappedArgs.WithPayDates([.. fixedPayDates], [.. floatingPayDates]), [.. fixedCouponAdjustments],
            [.. floatingCouponAdjustments]);
    }

    private static bool WithinPreviousWeek(Date d1, Date d2) => d2 >= d1 - 7 && d2 <= d1;

    private sealed record Parts(DiscretizedSwap Underlying, ExerciseType ExerciseType, double[] ExerciseTimes, double LastPayment);
}
