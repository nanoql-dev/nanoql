// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: Copyright (C) 2021, 2022 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/discretizedcallablefixedratebond.hpp ql/experimental/callablebonds/discretizedcallablefixedratebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A callable fixed-rate bond discretized on a lattice: its redemption rolled back with its coupons added and its
/// callabilities applied at their times.
/// </summary>
/// <remarks>
/// <para>
/// A working type (<see cref="DiscretizedAsset"/>), built for one calculation from the bond's arguments at the
/// context's settlement date. Times are measured on the curve's day counter from its reference date. A callability
/// within a week before a coupon date is moved to that coupon's time, its price carried there by the discount factor
/// between the two dates, spread included, and that coupon is then added before the callabilities at its time; a
/// callability on the date of a coupon so moved ahead gets that coupon added to its price. Prices are scaled from per
/// 100 to the face amount.
/// </para>
/// <para>
/// Each live callability is applied by its own type, as QuantLib's fix after v1.44 does (PR #2875), where QuantLib 1.44
/// read the i-th live callability's type from the i-th entry of the whole schedule, so that after a callability on or
/// before the settlement date a call was applied as a put, or a put as a call. A call caps the values at its price, a
/// put floors them.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class DiscretizedCallableFixedRateBond : DiscretizedAsset
{
    private readonly CallableBondArguments arguments;
    private readonly double redemptionTime;
    private readonly double[] couponTimes;
    private readonly CouponAdjustment[] couponAdjustments;
    private readonly double[] callabilityTimes;
    private readonly double[] adjustedCallabilityPrices;

    /// <summary>Initializes a new instance of the <see cref="DiscretizedCallableFixedRateBond"/> class.</summary>
    /// <param name="bond">The bond, whose arguments are computed at the context's settlement date.</param>
    /// <param name="termStructure">The curve whose reference date and day counter give the asset's times.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="spread">
    /// The continuously compounded spread the snapped callabilities' discount factors carry, as the lattice's rates do
    /// under an option-adjusted spread.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The bond has no single redemption.</exception>
    public DiscretizedCallableFixedRateBond(
        CallableBond bond, YieldTermStructure termStructure, EvaluationContext context, double spread = 0.0)
        : this(ArgumentsOf(bond, context), termStructure, spread)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscretizedCallableFixedRateBond"/> class from the bond's arguments.
    /// </summary>
    /// <param name="args">The bond's arguments.</param>
    /// <param name="termStructure">The curve whose reference date and day counter give the asset's times.</param>
    /// <param name="spread">The continuously compounded spread of the snapped callabilities' discount factors.</param>
    internal DiscretizedCallableFixedRateBond(CallableBondArguments args, YieldTermStructure termStructure, double spread)
    {
        ArgumentNullException.ThrowIfNull(termStructure);
        arguments = args;
        adjustedCallabilityPrices = [.. args.CallabilityPrices];

        DayCounter dayCounter = termStructure.DayCounter;
        Date referenceDate = termStructure.ReferenceDate;

        redemptionTime = dayCounter.YearFraction(referenceDate, args.RedemptionDate);

        /* By default the coupon adjustment should take place in
         * DiscretizedCallableFixedRateBond::postAdjustValuesImpl(). */
        couponAdjustments = new CouponAdjustment[args.CouponDates.Length];
        Array.Fill(couponAdjustments, CouponAdjustment.Post);

        couponTimes = new double[args.CouponDates.Length];
        for (int i = 0; i < couponTimes.Length; ++i)
        {
            couponTimes[i] = dayCounter.YearFraction(referenceDate, args.CouponDates[i]);
        }

        callabilityTimes = new double[args.CallabilityDates.Length];
        for (int i = 0; i < callabilityTimes.Length; ++i)
        {
            Date callabilityDate = args.CallabilityDates[i];
            double callabilityTime = dayCounter.YearFraction(referenceDate, args.CallabilityDates[i]);

            // To avoid mispricing, we snap exercise dates to the closest coupon date.
            for (int j = 0; j < couponTimes.Length; j++)
            {
                double couponTime = couponTimes[j];
                Date couponDate = args.CouponDates[j];

                if (WithinNextWeek(callabilityTime, couponTime) && callabilityDate < couponDate)
                {
                    // Snap the exercise date.
                    callabilityTime = couponTime;

                    /* The order of events must be changed here. In
                     * DiscretizedCallableFixedRateBond::postAdjustValuesImpl() the callability is
                     * done before adding of the coupon. However from the
                     * DiscretizedAsset::rollback(Time to) perspective the coupon must be added
                     * before the callability as it is later in time. */
                    couponAdjustments[j] = CouponAdjustment.Pre;

                    /* We snapped the callabilityTime so we need to take into account the missing
                     * discount factor including any possible spread e.g. set in the OAS
                     * calculation. */
                    double dfTillCallDate = DiscountFactorInclSpread(termStructure, spread, callabilityDate);
                    double dfTillCouponDate = DiscountFactorInclSpread(termStructure, spread, couponDate);
                    adjustedCallabilityPrices[i] *= dfTillCallDate / dfTillCouponDate;

                    break;
                }
            }

            adjustedCallabilityPrices[i] *= args.FaceAmount / 100.0;
            callabilityTimes[i] = callabilityTime;
        }

        // not snapped itself, but a snapped neighbour may have moved this coupon ahead
        for (int i = 0; i < callabilityTimes.Length; ++i)
        {
            for (int j = 0; j < couponTimes.Length; ++j)
            {
                if (couponAdjustments[j] == CouponAdjustment.Pre && args.CallabilityDates[i] == args.CouponDates[j])
                {
                    adjustedCallabilityPrices[i] += args.CouponAmounts[j];
                }
            }
        }
    }

    /// <summary>Sets every value to the redemption, then adjusts them at the asset's time.</summary>
    /// <param name="size">The number of values.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public override void Reset(int size)
    {
        ResetValues(size, arguments.Redemption);
        AdjustValues();
    }

    /// <summary>The bond's mandatory times: the redemption's, the coupons' and the callabilities', those not negative.</summary>
    /// <returns>The times, in that order.</returns>
    public override ImmutableArray<double> MandatoryTimes()
    {
        ImmutableArray<double>.Builder times = ImmutableArray.CreateBuilder<double>();
        double t;
        int i;

        t = redemptionTime;
        if (t >= 0.0)
        {
            times.Add(t);
        }

        for (i = 0; i < couponTimes.Length; i++)
        {
            t = couponTimes[i];
            if (t >= 0.0)
            {
                times.Add(t);
            }
        }

        for (i = 0; i < callabilityTimes.Length; i++)
        {
            t = callabilityTimes[i];
            if (t >= 0.0)
            {
                times.Add(t);
            }
        }

        return times.ToImmutable();
    }

    /// <summary>Adds the coupons moved ahead of a snapped callability, at their times.</summary>
    protected override void PreAdjustValuesImpl()
    {
        for (int i = 0; i < couponTimes.Length; i++)
        {
            if (couponAdjustments[i] == CouponAdjustment.Pre)
            {
                double t = couponTimes[i];
                if (t >= 0.0 && IsOnTime(t))
                {
                    AddCoupon(i);
                }
            }
        }
    }

    /// <summary>Applies the callabilities at their times, then adds the other coupons at theirs.</summary>
    protected override void PostAdjustValuesImpl()
    {
        for (int i = 0; i < callabilityTimes.Length; i++)
        {
            double t = callabilityTimes[i];
            if (t >= 0.0 && IsOnTime(t))
            {
                ApplyCallability(i);
            }
        }

        for (int i = 0; i < couponTimes.Length; i++)
        {
            if (couponAdjustments[i] == CouponAdjustment.Post)
            {
                double t = couponTimes[i];
                if (t >= 0.0 && IsOnTime(t))
                {
                    /* Exercise and coupon date matches. */
                    AddCoupon(i);
                }
            }
        }
    }

    private static CallableBondArguments ArgumentsOf(CallableBond bond, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        return bond.SetupArguments(context);
    }

    private static bool WithinNextWeek(double t1, double t2)
    {
        const double dt = 1.0 / 52;
        return t1 <= t2 && t2 <= t1 + dt;
    }

    private static double DiscountFactorInclSpread(YieldTermStructure termStructure, double spread, Date date)
    {
        double time = termStructure.TimeFromReference(date);
        double zeroRateInclSpread =
            termStructure.ZeroRate(date, termStructure.DayCounter, Compounding.Continuous, Frequency.NoFrequency).Rate +
            spread;
        double df = Math.Exp(-zeroRateInclSpread * time);
        return df;
    }

    private void ApplyCallability(int i)
    {
        Span<double> values = Values;
        int j;
        switch (arguments.CallabilityTypes[i])
        {
            case CallabilityType.Call:
                for (j = 0; j < values.Length; j++)
                {
                    // std::min(adjustedCallabilityPrices_[i], values_[j])
                    double price = adjustedCallabilityPrices[i];
                    values[j] = values[j] < price ? values[j] : price;
                }

                break;
            case CallabilityType.Put:
                for (j = 0; j < values.Length; j++)
                {
                    // std::max(values_[j], adjustedCallabilityPrices_[i])
                    double price = adjustedCallabilityPrices[i];
                    values[j] = values[j] < price ? price : values[j];
                }

                break;
            default:
                throw new NanoQLException("unknown callability type");
        }
    }

    private void AddCoupon(int i)
    {
        Span<double> values = Values;
        double amount = arguments.CouponAmounts[i];
        for (int j = 0; j < values.Length; j++)
        {
            values[j] += amount;
        }
    }
}
