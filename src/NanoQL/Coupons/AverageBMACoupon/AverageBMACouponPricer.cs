// SPDX-FileCopyrightText: Copyright (C) 2007 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/averagebmacoupon.hpp ql/cashflows/averagebmacoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>
/// The pricer every <see cref="AverageBMACoupon"/> holds: QuantLib's anonymous <c>AverageBMACouponPricer</c>, whose
/// swaplet rate averages the coupon's fixings by the days each is valid within the interest period.
/// </summary>
/// <remarks>
/// Stateless, as every nanoql pricer is: QuantLib keeps the coupon it was initialized with, which this pricer is given
/// on each call instead, and refuses a coupon of another type as QuantLib's <c>initialize</c> does.
/// </remarks>
internal sealed class AverageBMACouponPricer : FloatingRateCouponPricer
{
    /// <summary>The one instance the coupons share.</summary>
    internal static readonly AverageBMACouponPricer Instance = new();

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: the price isn't available.</exception>
    public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) =>
        throw new NanoQLException("not available");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The coupon isn't an average-BMA coupon, its fixing dates don't cover its period, or a fixing can't be read.
    /// </exception>
    public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentNullException.ThrowIfNull(context);
        var averageCoupon = coupon as AverageBMACoupon ?? throw new NanoQLException("wrong coupon type");
        ImmutableArray<Date> fixingDates = averageCoupon.FixingDates();
        InterestRateIndex index = averageCoupon.Index;

        const int cutoffDays = 0; // to be verified
        Date startDate = averageCoupon.AccrualStartDate - cutoffDays,
            endDate = averageCoupon.AccrualEndDate - cutoffDays,
            d1 = startDate;

        if (fixingDates.IsEmpty)
        {
            throw new NanoQLException("fixing date list empty");
        }

        if (!(index.ValueDate(fixingDates[0]) <= startDate))
        {
            throw new NanoQLException("first fixing date valid after period start");
        }

        if (!(index.ValueDate(fixingDates[^1]) >= endDate))
        {
            throw new NanoQLException("last fixing date valid before period end");
        }

        double avgBMA = 0.0;
        int days = 0;
        for (int i = 0; i < fixingDates.Length - 1; ++i)
        {
            Date valueDate = index.ValueDate(fixingDates[i]);
            Date nextValueDate = index.ValueDate(fixingDates[i + 1]);

            if (fixingDates[i] >= endDate || valueDate >= endDate)
            {
                break;
            }

            if (fixingDates[i + 1] < startDate || nextValueDate <= startDate)
            {
                continue;
            }

            // std::min(nextValueDate, endDate)
            Date d2 = endDate < nextValueDate ? endDate : nextValueDate;

            avgBMA += index.Fixing(fixingDates[i], context) * (d2 - d1);

            days += d2 - d1;
            d1 = d2;
        }

        avgBMA /= endDate - startDate;

        if (days != endDate - startDate)
        {
            throw new NanoQLException($"averaging days {days} differ from interest days {endDate - startDate}");
        }

        return (averageCoupon.Gearing * avgBMA) + averageCoupon.Spread;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: the price isn't available.</exception>
    public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        throw new NanoQLException("not available");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: the rate isn't available.</exception>
    public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) =>
        throw new NanoQLException("not available");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: the price isn't available.</exception>
    public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        throw new NanoQLException("not available");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: the rate isn't available.</exception>
    public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) =>
        throw new NanoQLException("not available");
}
