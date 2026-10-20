// SPDX-FileCopyrightText: Copyright (C) 2007 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/averagebmacoupon.hpp ql/cashflows/averagebmacoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Coupons;

/// <summary>A builder of a sequence of average-BMA coupons.</summary>
/// <remarks>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToLeg"/> builds the leg, in place of QuantLib's conversion operator; it reads no context. A builder
/// has no value equality: its vectors compare by reference. A notional, gearing or spread vector shorter than the
/// schedule repeats its last element.
/// </remarks>
public sealed record AverageBMALeg
{
    /// <summary>Initializes a new instance of the <see cref="AverageBMALeg"/> class.</summary>
    /// <param name="schedule">The schedule of accrual dates.</param>
    /// <param name="index">The BMA index.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public AverageBMALeg(Schedule schedule, BMAIndex index)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        // QuantLib's leg never checks the index, and its coupons read it before their base checks it, which is
        // undefined for a null index: the refusal takes the base's text.
        if (index is null)
        {
            throw new ArgumentNullException(nameof(index), "no index provided");
        }

        Schedule = schedule;
        Index = index;
    }

    private Schedule Schedule { get; init; }

    private BMAIndex Index { get; init; }

    private ImmutableArray<double> Notionals { get; init; } = [];

    private DayCounter? PaymentDayCounter { get; init; }

    private BusinessDayConvention PaymentAdjustment { get; init; } = BusinessDayConvention.Following;

    private ImmutableArray<double> Gearings { get; init; } = [];

    private ImmutableArray<double> Spreads { get; init; } = [];

    /// <summary>Sets one notional for every coupon.</summary>
    /// <param name="notional">The notional.</param>
    /// <returns>The modified builder.</returns>
    public AverageBMALeg WithNotionals(double notional) => this with { Notionals = [notional] };

    /// <summary>Sets the coupons' notionals; the last one repeats for any further coupons.</summary>
    /// <param name="notionals">The notionals.</param>
    /// <returns>The modified builder.</returns>
    public AverageBMALeg WithNotionals(IEnumerable<double> notionals)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        return this with { Notionals = [.. notionals] };
    }

    /// <summary>Sets the accrual day counter; the index's by default.</summary>
    /// <param name="dayCounter">The day counter; the index's when <see langword="null"/>, QuantLib's empty day
    /// counter.</param>
    /// <returns>The modified builder.</returns>
    public AverageBMALeg WithPaymentDayCounter(DayCounter? dayCounter) => this with { PaymentDayCounter = dayCounter };

    /// <summary>Sets the business-day convention of the payment dates; following by default.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public AverageBMALeg WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets one gearing for every coupon; 1 by default.</summary>
    /// <param name="gearing">The gearing.</param>
    /// <returns>The modified builder.</returns>
    public AverageBMALeg WithGearings(double gearing) => this with { Gearings = [gearing] };

    /// <summary>Sets the coupons' gearings; the last one repeats for any further coupons.</summary>
    /// <param name="gearings">The gearings.</param>
    /// <returns>The modified builder.</returns>
    public AverageBMALeg WithGearings(IEnumerable<double> gearings)
    {
        ArgumentNullException.ThrowIfNull(gearings);
        return this with { Gearings = [.. gearings] };
    }

    /// <summary>Sets one spread for every coupon; 0 by default.</summary>
    /// <param name="spread">The spread.</param>
    /// <returns>The modified builder.</returns>
    public AverageBMALeg WithSpreads(double spread) => this with { Spreads = [spread] };

    /// <summary>Sets the coupons' spreads; the last one repeats for any further coupons.</summary>
    /// <param name="spreads">The spreads.</param>
    /// <returns>The modified builder.</returns>
    public AverageBMALeg WithSpreads(IEnumerable<double> spreads)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        return this with { Spreads = [.. spreads] };
    }

    /// <summary>Builds the leg.</summary>
    /// <returns>The leg, one coupon per schedule period.</returns>
    /// <exception cref="ArgumentException">A gearing is zero.</exception>
    /// <exception cref="NanoQLException">
    /// No notionals were given; the schedule is empty; or a date is computed on QuantLib's empty calendar where it
    /// can't give one.
    /// </exception>
    public ImmutableArray<CashFlow> ToLeg()
    {
        if (Notionals.IsEmpty)
        {
            throw new NanoQLException("no notional given");
        }

        if (Schedule.Empty)
        {
            // QuantLib's period count, schedule_.size()-1, is a Size that wraps to its largest value, and its first
            // read, schedule_.date(0), throws std::out_of_range.
            throw new NanoQLException("empty schedule");
        }

        var cashflows = ImmutableArray.CreateBuilder<CashFlow>();

        // the following is not always correct. QuantLib's empty calendar, null here, is used unchecked.
        Calendar calendar = Schedule.Calendar ?? EmptyCalendar.Instance;

        int n = Schedule.Size - 1;
        for (int i = 0; i < n; ++i)
        {
            Date refStart, start, refEnd, end;
            refStart = start = Schedule.Date(i);
            refEnd = end = Schedule.Date(i + 1);
            Date paymentDate = calendar.Adjust(end, PaymentAdjustment);
            if (i == 0 && Schedule.HasIsRegular && !Schedule.IsRegular(i + 1) && Schedule.HasTenor)
            {
                refStart = calendar.Adjust(end - Schedule.Tenor(), PaymentAdjustment);
            }

            if (i == n - 1 && Schedule.HasIsRegular && !Schedule.IsRegular(i + 1) && Schedule.HasTenor)
            {
                refEnd = calendar.Adjust(start + Schedule.Tenor(), PaymentAdjustment);
            }

            cashflows.Add(new AverageBMACoupon(
                paymentDate,
                VectorsFunctions.Get(Notionals, i, Notionals[^1]),
                start,
                end,
                Index,
                VectorsFunctions.Get(Gearings, i, 1.0),
                VectorsFunctions.Get(Spreads, i, 0.0),
                refStart,
                refEnd,
                PaymentDayCounter));
        }

        return cashflows.ToImmutable();
    }
}
