// SPDX-FileCopyrightText: Copyright (C) 2008 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2021 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/multipleresetscoupon.hpp ql/cashflows/multipleresetscoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Coupons;

/// <summary>A helper class building a sequence of multiple-resets coupons: QuantLib's
/// <c>MultipleResetsLeg</c>.</summary>
/// <remarks>
/// An immutable builder: each method returns a modified copy. <see cref="ToLeg"/> builds the leg, in place of
/// QuantLib's conversion operator, each coupon taking the reset schedule's next <c>resetsPerCoupon</c> periods and the
/// compounding pricer, or the averaging one for simple averaging. A builder has no value equality.
/// </remarks>
public sealed record MultipleResetsLeg
{
    /// <summary>Initializes a new instance of the <see cref="MultipleResetsLeg"/> record.</summary>
    /// <param name="fullResetSchedule">The schedule of every coupon's reset periods.</param>
    /// <param name="index">The index whose fixings are compounded or averaged; it should have the resets'
    /// tenor.</param>
    /// <param name="resetsPerCoupon">The number of resets per coupon, which divides the schedule's periods
    /// exactly.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fullResetSchedule"/> is null, or
    /// <paramref name="index"/> is, QuantLib's "no index provided".</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="resetsPerCoupon"/> isn't positive, where
    /// QuantLib's check divides by it (the S-cms plan's CMR5 (xiv)).</exception>
    /// <exception cref="ArgumentException">The schedule is empty, or <paramref name="resetsPerCoupon"/> doesn't divide
    /// its periods, each with QuantLib's text.</exception>
    public MultipleResetsLeg(Schedule fullResetSchedule, IborIndex index, int resetsPerCoupon)
    {
        ArgumentNullException.ThrowIfNull(fullResetSchedule);
        if (index is null)
        {
            throw new ArgumentNullException(nameof(index), "no index provided");
        }

        if (fullResetSchedule.Empty)
        {
            throw new ArgumentException("empty schedule provided", nameof(fullResetSchedule));
        }

        if (!(resetsPerCoupon > 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resetsPerCoupon), resetsPerCoupon, "a coupon needs at least one reset, so resetsPerCoupon must be positive");
        }

        if (!((fullResetSchedule.Size - 1) % resetsPerCoupon == 0))
        {
            throw new ArgumentException(
                "number of resets per coupon does not divide exactly number of periods in schedule", nameof(resetsPerCoupon));
        }

        Schedule = fullResetSchedule;
        Index = index;
        ResetsPerCoupon = resetsPerCoupon;
        PaymentCalendar = fullResetSchedule.Calendar;
    }

    private Schedule Schedule { get; init; }

    private IborIndex Index { get; init; }

    private int ResetsPerCoupon { get; init; }

    private ImmutableArray<double> Notionals { get; init; } = [];

    private DayCounter? PaymentDayCounter { get; init; }

    private Calendar? PaymentCalendar { get; init; }

    private BusinessDayConvention PaymentAdjustment { get; init; } = BusinessDayConvention.Following;

    private int PaymentLag { get; init; }

    private ImmutableArray<int> FixingDays { get; init; } = [];

    private ImmutableArray<double> Gearings { get; init; } = [];

    private ImmutableArray<double> CouponSpreads { get; init; } = [];

    private ImmutableArray<double> RateSpreads { get; init; } = [];

    private RateAveragingType AveragingMethod { get; init; } = RateAveragingType.Compound;

    private Period ExCouponPeriod { get; init; }

    private Calendar? ExCouponCalendar { get; init; }

    private BusinessDayConvention ExCouponAdjustment { get; init; } = BusinessDayConvention.Unadjusted;

    private bool ExCouponEndOfMonth { get; init; }

    /// <summary>Sets one notional for every coupon.</summary>
    /// <param name="notional">The notional.</param>
    /// <returns>The modified builder.</returns>
    public MultipleResetsLeg WithNotionals(double notional) => this with { Notionals = [notional] };

    /// <summary>Sets the coupons' notionals, the coupons after the last taking the last.</summary>
    /// <param name="notionals">The notionals.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="notionals"/> is null.</exception>
    public MultipleResetsLeg WithNotionals(IEnumerable<double> notionals)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        return this with { Notionals = [.. notionals] };
    }

    /// <summary>Sets the payment day counter, the index's when <see langword="null"/>.</summary>
    /// <param name="dayCounter">The day counter.</param>
    /// <returns>The modified builder.</returns>
    public MultipleResetsLeg WithPaymentDayCounter(DayCounter? dayCounter) => this with { PaymentDayCounter = dayCounter };

    /// <summary>Sets the convention adjusting the payment dates.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MultipleResetsLeg WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets the payment calendar, the schedule's by default.</summary>
    /// <param name="calendar">The calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <returns>The modified builder.</returns>
    public MultipleResetsLeg WithPaymentCalendar(Calendar? calendar) => this with { PaymentCalendar = calendar };

    /// <summary>Sets the payment lag, in business days after each coupon's end.</summary>
    /// <param name="lag">The lag.</param>
    /// <returns>The modified builder.</returns>
    public MultipleResetsLeg WithPaymentLag(int lag) => this with { PaymentLag = lag };

    /// <summary>Sets one number of fixing days for every coupon.</summary>
    /// <param name="fixingDays">The fixing days.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fixingDays"/> is negative.</exception>
    public MultipleResetsLeg WithFixingDays(int fixingDays) => WithFixingDays([fixingDays]);

    /// <summary>Sets the coupons' fixing days, the coupons after the last taking the index's.</summary>
    /// <param name="fixingDays">The fixing days.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fixingDays"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number of fixing days is negative.</exception>
    public MultipleResetsLeg WithFixingDays(IEnumerable<int> fixingDays)
    {
        ArgumentNullException.ThrowIfNull(fixingDays);
        ImmutableArray<int> days = [.. fixingDays];
        foreach (int d in days)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(d, nameof(fixingDays));
        }

        return this with { FixingDays = days };
    }

    /// <summary>Sets one gearing for every coupon.</summary>
    /// <param name="gearing">The gearing.</param>
    /// <returns>The modified builder.</returns>
    public MultipleResetsLeg WithGearings(double gearing) => this with { Gearings = [gearing] };

    /// <summary>Sets the coupons' gearings, the coupons after the last taking 1.</summary>
    /// <param name="gearings">The gearings.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="gearings"/> is null.</exception>
    public MultipleResetsLeg WithGearings(IEnumerable<double> gearings)
    {
        ArgumentNullException.ThrowIfNull(gearings);
        return this with { Gearings = [.. gearings] };
    }

    /// <summary>Sets one coupon spread, added to the final rate, for every coupon.</summary>
    /// <param name="spread">The spread.</param>
    /// <returns>The modified builder.</returns>
    public MultipleResetsLeg WithCouponSpreads(double spread) => this with { CouponSpreads = [spread] };

    /// <summary>Sets the coupons' coupon spreads, the coupons after the last taking 0.</summary>
    /// <param name="spreads">The spreads.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spreads"/> is null.</exception>
    public MultipleResetsLeg WithCouponSpreads(IEnumerable<double> spreads)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        return this with { CouponSpreads = [.. spreads] };
    }

    /// <summary>Sets one rate spread, added to each fixing, for every coupon.</summary>
    /// <param name="spread">The spread.</param>
    /// <returns>The modified builder.</returns>
    public MultipleResetsLeg WithRateSpreads(double spread) => this with { RateSpreads = [spread] };

    /// <summary>Sets the coupons' rate spreads, the coupons after the last taking 0.</summary>
    /// <param name="spreads">The spreads.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spreads"/> is null.</exception>
    public MultipleResetsLeg WithRateSpreads(IEnumerable<double> spreads)
    {
        ArgumentNullException.ThrowIfNull(spreads);
        return this with { RateSpreads = [.. spreads] };
    }

    /// <summary>Sets the ex-coupon period, counted back from each payment date.</summary>
    /// <param name="period">The period; a zero-length period means no ex-coupon dates.</param>
    /// <param name="cal">The calendar the period is counted on; the schedule's when <see langword="null"/>, QuantLib's
    /// empty calendar.</param>
    /// <param name="convention">The business-day convention of the ex-coupon dates.</param>
    /// <param name="endOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public MultipleResetsLeg WithExCouponPeriod(
        Period period,
        Calendar? cal,
        BusinessDayConvention convention,
        bool endOfMonth = false)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with
        {
            ExCouponPeriod = period,
            ExCouponCalendar = cal,
            ExCouponAdjustment = convention,
            ExCouponEndOfMonth = endOfMonth,
        };
    }

    /// <summary>Sets how the resets combine: compounded, the default, or averaged.</summary>
    /// <param name="averagingMethod">The method.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The method isn't a member of the enum, where QuantLib refuses
    /// it building the leg ("unknown compounding convention").</exception>
    public MultipleResetsLeg WithAveragingMethod(RateAveragingType averagingMethod)
    {
        EnumArguments.Check(averagingMethod, nameof(averagingMethod));
        return this with { AveragingMethod = averagingMethod };
    }

    /// <summary>Builds the leg, with the compounding or averaging pricer set on each coupon.</summary>
    /// <returns>The leg, one coupon per <c>resetsPerCoupon</c> periods of the schedule.</returns>
    /// <exception cref="NanoQLException">No notional was given; more notionals, gearings, coupon spreads, rate spreads
    /// or fixing days were given than there are coupons; or a date is computed on QuantLib's empty calendar where it
    /// can't give one.</exception>
    public ImmutableArray<CashFlow> ToLeg()
    {
        Schedule schedule = Schedule;
        Calendar calendar = schedule.Calendar ?? EmptyCalendar.Instance;
        int n = (schedule.Size - 1) / ResetsPerCoupon;
        if (Notionals.IsEmpty)
        {
            throw new NanoQLException("no notional given");
        }

        CheckSize(Notionals.Length, n, "nominals");
        CheckSize(Gearings.Length, n, "gearings");
        CheckSize(CouponSpreads.Length, n, "coupon spreads");
        CheckSize(RateSpreads.Length, n, "rate spreads");
        CheckSize(FixingDays.Length, n, "fixing days");

        MultipleResetsPricer pricer = AveragingMethod == RateAveragingType.Simple
            ? new AveragingMultipleResetsPricer()
            : new CompoundingMultipleResetsPricer();
        Calendar paymentCalendar = PaymentCalendar ?? EmptyCalendar.Instance;
        var cashflows = ImmutableArray.CreateBuilder<CashFlow>(n);
        for (int i = 0; i < n; ++i)
        {
            Date start = schedule.Date(i * ResetsPerCoupon);
            Date end = schedule.Date((i + 1) * ResetsPerCoupon);
            Schedule subSchedule = schedule.After(start).Until(end);
            Date paymentDate = paymentCalendar.Advance(end, PaymentLag, TimeUnit.Days, PaymentAdjustment);
            Date? exCouponDate = null;
            if (ExCouponPeriod != default)
            {
                exCouponDate = (ExCouponCalendar ?? calendar).Advance(
                    paymentDate, -ExCouponPeriod, ExCouponAdjustment, ExCouponEndOfMonth);
            }

            cashflows.Add(new MultipleResetsCoupon(
                paymentDate,
                VectorsFunctions.Get(Notionals, i, Notionals[^1]),
                subSchedule,
                VectorsFunctions.Get(FixingDays, i, Index.FixingDays),
                Index,
                VectorsFunctions.Get(Gearings, i, 1.0),
                VectorsFunctions.Get(CouponSpreads, i, 0.0),
                VectorsFunctions.Get(RateSpreads, i, 0.0),
                start,
                end,
                PaymentDayCounter,
                exCouponDate,
                pricer));
        }

        return cashflows.MoveToImmutable();
    }

    private static void CheckSize(int given, int n, string what)
    {
        if (!(given <= n))
        {
            throw new NanoQLException($"too many {what} ({given}), only {n} required");
        }
    }
}
