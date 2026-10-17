// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Piter Dias
// SPDX-FileCopyrightText: Copyright (C) 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2017 Joseph Jeisman
// SPDX-FileCopyrightText: Copyright (C) 2017 Fabrice Lecuyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/fixedratecoupon.hpp ql/cashflows/fixedratecoupon.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: withLastPeriodDayCounter, which waits for the credit-default-swap slot, whose tests use it

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>A helper class building a sequence of fixed-rate coupons.</summary>
/// <remarks>
/// An immutable builder: each method returns a modified copy, so a builder kept and reused never changes under its
/// holder. <see cref="ToLeg"/> builds the leg, in place of QuantLib's conversion operator. A builder has no value
/// equality: its notionals and rates compare by reference.
/// </remarks>
public sealed record FixedRateLeg
{
    /// <summary>Initializes a new instance of the <see cref="FixedRateLeg"/> class.</summary>
    /// <param name="schedule">The schedule of accrual dates.</param>
    public FixedRateLeg(Schedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        Schedule = schedule;
        PaymentCalendar = schedule.Calendar;
    }

    private Schedule Schedule { get; init; }

    private ImmutableArray<double> Notionals { get; init; } = [];

    private ImmutableArray<InterestRate> CouponRates { get; init; } = [];

    private DayCounter? FirstPeriodDC { get; init; }

    private Calendar PaymentCalendar { get; init; }

    private BusinessDayConvention PaymentAdjustment { get; init; } = BusinessDayConvention.Following;

    private int PaymentLag { get; init; }

    private Period ExCouponPeriod { get; init; }

    private Calendar? ExCouponCalendar { get; init; }

    private BusinessDayConvention ExCouponAdjustment { get; init; } = BusinessDayConvention.Following;

    private bool ExCouponEndOfMonth { get; init; }

    /// <summary>Sets one notional for every coupon.</summary>
    /// <param name="notional">The notional.</param>
    /// <returns>The modified builder.</returns>
    public FixedRateLeg WithNotionals(double notional) => this with { Notionals = [notional] };

    /// <summary>Sets the coupons' notionals; the last one repeats for any further coupons.</summary>
    /// <param name="notionals">The notionals.</param>
    /// <returns>The modified builder.</returns>
    public FixedRateLeg WithNotionals(IEnumerable<double> notionals)
    {
        ArgumentNullException.ThrowIfNull(notionals);
        return this with { Notionals = [.. notionals] };
    }

    /// <summary>Sets one coupon rate for every coupon.</summary>
    /// <param name="rate">The rate.</param>
    /// <param name="paymentDayCounter">The day counter.</param>
    /// <param name="comp">The compounding rule.</param>
    /// <param name="freq">The compounding frequency.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public FixedRateLeg WithCouponRates(
        double rate,
        DayCounter paymentDayCounter,
        Compounding comp = Compounding.Simple,
        Frequency freq = Frequency.Annual) =>
        this with { CouponRates = [new InterestRate(rate, paymentDayCounter, comp, freq)] };

    /// <summary>Sets the coupon rates; the last one repeats for any further coupons.</summary>
    /// <param name="rates">The rates.</param>
    /// <param name="paymentDayCounter">The day counter.</param>
    /// <param name="comp">The compounding rule.</param>
    /// <param name="freq">The compounding frequency.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public FixedRateLeg WithCouponRates(
        IEnumerable<double> rates,
        DayCounter paymentDayCounter,
        Compounding comp = Compounding.Simple,
        Frequency freq = Frequency.Annual)
    {
        ArgumentNullException.ThrowIfNull(rates);
        return this with { CouponRates = [.. rates.Select(r => new InterestRate(r, paymentDayCounter, comp, freq))] };
    }

    /// <summary>Sets one interest rate for every coupon.</summary>
    /// <param name="i">The interest rate.</param>
    /// <returns>The modified builder.</returns>
    public FixedRateLeg WithCouponRates(InterestRate i)
    {
        ArgumentNullException.ThrowIfNull(i);
        return this with { CouponRates = [i] };
    }

    /// <summary>Sets the interest rates; the last one repeats for any further coupons.</summary>
    /// <param name="interestRates">The interest rates.</param>
    /// <returns>The modified builder.</returns>
    public FixedRateLeg WithCouponRates(IEnumerable<InterestRate> interestRates)
    {
        ArgumentNullException.ThrowIfNull(interestRates);
        ImmutableArray<InterestRate> rates = [.. interestRates];
        if (rates.Any(r => r is null))
        {
            throw new ArgumentException("null interest rate given", nameof(interestRates));
        }

        return this with { CouponRates = rates };
    }

    /// <summary>Sets the business-day convention of the payment dates.</summary>
    /// <param name="convention">The convention.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public FixedRateLeg WithPaymentAdjustment(BusinessDayConvention convention)
    {
        EnumArguments.Check(convention, nameof(convention));
        return this with { PaymentAdjustment = convention };
    }

    /// <summary>Sets the day counter of the first period, in place of the rate's.</summary>
    /// <param name="dayCounter">The day counter.</param>
    /// <returns>The modified builder.</returns>
    public FixedRateLeg WithFirstPeriodDayCounter(DayCounter dayCounter)
    {
        ArgumentNullException.ThrowIfNull(dayCounter);
        return this with { FirstPeriodDC = dayCounter };
    }

    /// <summary>Sets the calendar of the payment dates; the schedule's calendar by default.</summary>
    /// <param name="cal">The calendar.</param>
    /// <returns>The modified builder.</returns>
    public FixedRateLeg WithPaymentCalendar(Calendar cal)
    {
        ArgumentNullException.ThrowIfNull(cal);
        return this with { PaymentCalendar = cal };
    }

    /// <summary>Sets the number of business days from each accrual end to its payment date.</summary>
    /// <param name="lag">The lag.</param>
    /// <returns>The modified builder.</returns>
    public FixedRateLeg WithPaymentLag(int lag) => this with { PaymentLag = lag };

    /// <summary>Sets the ex-coupon period, counted back from each payment date.</summary>
    /// <param name="period">The period; a zero-length period means no ex-coupon dates.</param>
    /// <param name="cal">The calendar the period is counted on.</param>
    /// <param name="convention">The business-day convention of the ex-coupon dates.</param>
    /// <param name="endOfMonth">Whether the ex-coupon dates stay at month end.</param>
    /// <returns>The modified builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The convention isn't a member of the enum.</exception>
    public FixedRateLeg WithExCouponPeriod(
        Period period,
        Calendar cal,
        BusinessDayConvention convention,
        bool endOfMonth = false)
    {
        ArgumentNullException.ThrowIfNull(cal);
        EnumArguments.Check(convention, nameof(convention));
        return this with
        {
            ExCouponPeriod = period,
            ExCouponCalendar = cal,
            ExCouponAdjustment = convention,
            ExCouponEndOfMonth = endOfMonth,
        };
    }

    /// <summary>Builds the leg.</summary>
    /// <returns>The leg, one coupon per schedule period.</returns>
    /// <exception cref="NanoQLException">No coupon rates or no notionals were given.</exception>
    public ImmutableArray<CashFlow> ToLeg()
    {
        if (CouponRates.IsEmpty)
        {
            throw new NanoQLException("no coupon rates given");
        }

        if (Notionals.IsEmpty)
        {
            throw new NanoQLException("no notional given");
        }

        Schedule schedule = Schedule;
        var leg = ImmutableArray.CreateBuilder<CashFlow>();

        // first period might be short or long
        Date start = schedule.Date(0), end = schedule.Date(1);
        Date paymentDate = PaymentCalendar.Advance(end, PaymentLag, TimeUnit.Days, PaymentAdjustment);
        Date? exCouponDate = ExCouponDateFor(paymentDate);
        InterestRate rate = CouponRates[0];
        double nominal = Notionals[0];
        Date reference = schedule.HasTenor && schedule.HasIsRegular && !schedule.IsRegular(1)
            ? schedule.Calendar.Advance(end, -schedule.Tenor(), schedule.BusinessDayConvention, schedule.EndOfMonth())
            : start;
        var r = new InterestRate(rate.Rate, FirstPeriodDC ?? rate.DayCounter, rate.Compounding, rate.Frequency);
        leg.Add(new FixedRateCoupon(paymentDate, nominal, r, start, end, reference, end, exCouponDate));

        // regular periods
        for (int i = 2; i < schedule.Size - 1; ++i)
        {
            start = end;
            end = schedule.Date(i);
            paymentDate = PaymentCalendar.Advance(end, PaymentLag, TimeUnit.Days, PaymentAdjustment);
            exCouponDate = ExCouponDateFor(paymentDate);
            rate = (i - 1) < CouponRates.Length ? CouponRates[i - 1] : CouponRates[^1];
            nominal = (i - 1) < Notionals.Length ? Notionals[i - 1] : Notionals[^1];
            leg.Add(new FixedRateCoupon(paymentDate, nominal, rate, start, end, start, end, exCouponDate));
        }

        if (schedule.Size > 2)
        {
            // last period might be short or long
            int n = schedule.Size;
            start = end;
            end = schedule.Date(n - 1);
            paymentDate = PaymentCalendar.Advance(end, PaymentLag, TimeUnit.Days, PaymentAdjustment);
            exCouponDate = ExCouponDateFor(paymentDate);
            rate = (n - 2) < CouponRates.Length ? CouponRates[n - 2] : CouponRates[^1];
            nominal = (n - 2) < Notionals.Length ? Notionals[n - 2] : Notionals[^1];
            if ((schedule.HasIsRegular && schedule.IsRegular(n - 1)) || !schedule.HasTenor)
            {
                leg.Add(new FixedRateCoupon(paymentDate, nominal, rate, start, end, start, end, exCouponDate));
            }
            else
            {
                Date referenceEnd = schedule.Calendar.Advance(
                    start, schedule.Tenor(), schedule.BusinessDayConvention, schedule.EndOfMonth());
                leg.Add(new FixedRateCoupon(paymentDate, nominal, rate, start, end, start, referenceEnd, exCouponDate));
            }
        }

        return leg.ToImmutable();
    }

    private Date? ExCouponDateFor(Date paymentDate) =>
        ExCouponPeriod != default
            ? ExCouponCalendar!.Advance(paymentDate, -ExCouponPeriod, ExCouponAdjustment, ExCouponEndOfMonth)
            : null;
}
