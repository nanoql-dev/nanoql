// SPDX-FileCopyrightText: Copyright (C) 2008-2013 Andrea Maggiulli (a.maggiulli@gmail.com)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: src/QLNet/Instruments/Bonds/AmortizingBond.cs @ QLNet-v1.13.2
// Ported from QLNet and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Extensions.Instruments;

/// <summary>
/// A fixed-rate bond held at amortised cost: the effective rate of its purchase, and its carrying amount over its life,
/// the difference between its cost and its face amount amortised by the effective interest method.
/// </summary>
/// <remarks>
/// <para>
/// The effective rate is solved on the trade date, at initial recognition, as IFRS 9 and ASC 310-20's interest method
/// require: the rate, simple within each coupon period and compounded at each coupon date, at which the bond's
/// remaining coupons and redemption are worth the price paid, the cost plus the accrued interest purchased
/// (<see cref="CashFlows.Yield(ImmutableArray{CashFlow}, double, DayCounter, Compounding, Frequency,
/// EvaluationContext, bool?, Date?, Date?, double, int, double)"/>, settled on the trade date). Each coupon date after
/// the trade books an amortisation (<see cref="Amortizations"/>): the interest at the effective rate on the carrying
/// amount, over the yield's own time, less the coupon (less the accrued interest purchased, in the first period),
/// rounded to the cent, positive when the carrying amount accretes toward a face above it; the last closes the carrying
/// amount to the face amount. <see cref="AmortizationValue"/> is the carrying amount, without accrued interest, at
/// any date of the bond's life: the cost on the trade date, continuous, and the face amount at maturity.
/// </para>
/// <para>
/// Ported from QLNet. Its departures from QLNet: the rate is solved at the trade date with the accrued interest
/// purchased, where QLNet solves at the coupon date on or before the trade (or at its global evaluation date, with no
/// coupon before it) and its carrying amount jumps at the first coupon date; a partial period accrues on the carrying
/// amount at its start, where QLNet's accrues on the cost; the coupon is the leg's own, day-counted, where QLNet's
/// amortisation pays the face amount times the rate over the frequency; an amortisation's sign is its interest less
/// its coupon, where QLNet's is the bond's being at a premium; the last amortisation closes to the face amount, where
/// QLNet's cents leave it off; a given yield of 0 is taken, where QLNet solves instead; the bond's calendar is the one
/// given, where QLNet's base uses TARGET; and the bond's cash flows are its coupons and redemption, where QLNet's hold
/// the accounting entries and coupons whose amounts it overrides. On a purchase on a coupon date the method is QLNet's.
/// </para>
/// </remarks>
public sealed class AmortizingBond : Bond
{
    private readonly double faceValue;
    private readonly double marketValue;
    private readonly double couponRate;
    private readonly Date tradeDate;
    private readonly DayCounter dCounter;
    private readonly double accruedPurchased;
    private readonly double yield;
    private readonly ImmutableArray<FixedRateCoupon> periods;
    private readonly ImmutableArray<double> carrying;

    /// <summary>Initializes a new instance of the <see cref="AmortizingBond"/> class.</summary>
    /// <param name="faceValue">The face amount held.</param>
    /// <param name="marketValue">The cost: the price paid, without the accrued interest purchased.</param>
    /// <param name="couponRate">The coupon rate, simple and annual.</param>
    /// <param name="issueDate">The issue date, the start of the coupon schedule.</param>
    /// <param name="maturityDate">The maturity date.</param>
    /// <param name="tradeDate">The trade date, the purchase's initial recognition.</param>
    /// <param name="payFrequency">The coupon frequency.</param>
    /// <param name="dCounter">The coupons' day counter, and the effective rate's.</param>
    /// <param name="method">The amortisation method.</param>
    /// <param name="calendar">The bond's calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <param name="context">The evaluation context, which the yield's solve reads for a flow's occurrence.</param>
    /// <param name="gYield">The effective rate, or <see langword="null"/> to solve it.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="dCounter"/> or <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="method"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// The trade date isn't before the maturity date, or the frequency gives no coupon period.
    /// </exception>
    /// <exception cref="NanoQLException">The yield's solver fails.</exception>
    public AmortizingBond(
        double faceValue,
        double marketValue,
        double couponRate,
        Date issueDate,
        Date maturityDate,
        Date tradeDate,
        Frequency payFrequency,
        DayCounter dCounter,
        AmortizingMethod method,
        Calendar? calendar,
        EvaluationContext context,
        double? gYield = null)
        : base(0, calendar, issueDate)
    {
        ArgumentNullException.ThrowIfNull(dCounter);
        ArgumentNullException.ThrowIfNull(context);
        if (method != AmortizingMethod.EffectiveInterestRate)
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "not an amortising method");
        }

        if (!(tradeDate < maturityDate))
        {
            throw new ArgumentException(
                $"trade date ({tradeDate}) must be before the maturity date ({maturityDate})", nameof(tradeDate));
        }

        this.faceValue = faceValue;
        this.marketValue = marketValue;
        this.couponRate = couponRate;
        this.tradeDate = tradeDate;
        this.dCounter = dCounter;
        IsPremium = marketValue > faceValue;

        // The leg QLNet builds for its yield: coupons on the face amount from the issue date, and the redemption.
        var schedule = new Schedule(
            issueDate,
            maturityDate,
            new Period(payFrequency),
            calendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            context);
        ImmutableArray<CashFlow> leg = new FixedRateLeg(schedule)
            .WithCouponRates([couponRate], dCounter)
            .WithPaymentCalendar(calendar)
            .WithNotionals([faceValue])
            .WithPaymentAdjustment(BusinessDayConvention.Unadjusted)
            .ToLeg();
        cashflows = leg;
        this.maturityDate = maturityDate;
        SetSingleRedemption(faceValue, 100, maturityDate);

        // The effective rate at initial recognition: the price paid, cost plus accrued interest, on the trade date.
        accruedPurchased = CashFlows.AccruedAmount(leg, context, false, tradeDate);
        yield = gYield ?? CashFlows.Yield(
            cashflows, marketValue + accruedPurchased, dCounter, Compounding.Simple, payFrequency, context, false,
            tradeDate);

        // The amortisation at each coupon date after the trade: the interest on the carrying amount over the yield's
        // stepwise time, less the coupon, to the cent; the last closes to the face amount.
        List<FixedRateCoupon> coupons = [.. leg.OfType<FixedRateCoupon>().Where(c => c.Date > tradeDate)];
        var entries = ImmutableArray.CreateBuilder<AmortizingPayment>(coupons.Count);
        var amounts = ImmutableArray.CreateBuilder<double>(coupons.Count);
        // QLNet's two readings of the carrying amount: the loop's running nominal, which the next period's interest
        // accrues on, and the cost plus the amortisations' sum, which AmortizationValue returns.
        double nominal = marketValue + accruedPurchased;
        double amortized = 0;
        Date last = tradeDate;
        for (int k = 0; k < coupons.Count; k++)
        {
            FixedRateCoupon coupon = coupons[k];
            double interest = nominal * ((1.0 + (yield * StepwiseTime(coupon, last, coupon.Date))) - 1.0);
            double payment = coupon.Amount(context) - (k == 0 ? accruedPurchased : 0.0);
            bool final = k == coupons.Count - 1;
            double amort = final ? faceValue - (marketValue + amortized) : Math.Round(interest - payment, 2);
            entries.Add(new AmortizingPayment(amort, coupon.Date));
            amortized += amort;
            amounts.Add(final ? faceValue : marketValue + amortized);
            nominal = (k == 0 ? marketValue : nominal) + amort;
            last = coupon.Date;
        }

        Amortizations = entries.MoveToImmutable();
        carrying = amounts.MoveToImmutable();
        periods = [.. coupons];
    }

    /// <summary>Gets a value indicating whether the cost is above the face amount.</summary>
    public bool IsPremium { get; }

    /// <summary>
    /// Gets the amortisations, one at each coupon date after the trade, in date order: the change in the carrying
    /// amount, positive where it accretes, negative where a premium amortises. They are accounting entries, not
    /// payments, so they aren't among the bond's cash flows.
    /// </summary>
    public ImmutableArray<AmortizingPayment> Amortizations { get; }

    /// <summary>The effective rate, simple within each coupon period and compounded at the coupon frequency.</summary>
    /// <returns>The rate.</returns>
    public double Yield() => yield;

    /// <summary>
    /// The carrying amount at a date, without accrued interest: the cost on the trade date; at a coupon date the
    /// amortisations booked; between them the last carrying amount plus the interest on it at the effective rate, less
    /// the coupon's, since that date, both over the coupon period's stepwise time, as the effective rate's solve counts
    /// it; the face amount at maturity.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <returns>The carrying amount; 0 before the trade date and after maturity, as QLNet returns.</returns>
    public double AmortizationValue(Date d)
    {
        if (d < tradeDate || d > MaturityDate())
        {
            return 0;
        }

        int k = -1;
        while (k + 1 < Amortizations.Length && Amortizations[k + 1].Date <= d)
        {
            k++;
        }

        Date lastDate = k >= 0 ? Amortizations[k].Date : tradeDate;
        double cleanLast = k >= 0 ? carrying[k] : marketValue;
        if (lastDate == d)
        {
            return cleanLast;
        }

        // The interest since the last date, on the face amount at the coupon rate and on the carrying amount at the
        // effective rate (in the first period of a purchase between coupon dates, the price paid, cost plus accrued),
        // over the period's time from the last date: QLNet's form, on the coupon's own period and reference period.
        double nominal = k >= 0 ? cleanLast : marketValue + accruedPurchased;
        double t = StepwiseTime(periods[k + 1], lastDate, d);
        double baseInterest = faceValue * ((1.0 + (couponRate * t)) - 1.0);
        double interest = nominal * ((1.0 + (yield * t)) - 1.0);
        return cleanLast + (interest - baseInterest);
    }

    // QuantLib's stepwise discount time (CashFlows' getStepwiseDiscountTime) to a date of the coupon's period: the
    // period from the last date to it, on the coupon's reference period, as the part accrued by it less the part
    // accrued by the last date when the period started before that.
    private double StepwiseTime(FixedRateCoupon coupon, Date lastDate, Date d)
    {
        Date refStart = coupon.ReferencePeriodStart, refEnd = coupon.ReferencePeriodEnd;
        if (lastDate != coupon.AccrualStartDate)
        {
            double couponPeriod = dCounter.YearFraction(coupon.AccrualStartDate, d, refStart, refEnd);
            double accruedPeriod = dCounter.YearFraction(coupon.AccrualStartDate, lastDate, refStart, refEnd);
            return couponPeriod - accruedPeriod;
        }

        return dCounter.YearFraction(lastDate, d, refStart, refEnd);
    }
}
