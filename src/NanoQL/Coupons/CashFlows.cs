// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Charles Whitmore
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2010, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2008 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2010, 2011, 2012 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/cashflows.hpp ql/cashflows/cashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: every function but startDate, maturityDate, accruedPeriod, accruedDays, accruedAmount, npv, bps and npvbps on a yield term structure, and bps on an interest rate, which wait for the slots whose code and tests use them

using System.Collections.Immutable;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>Cash-flow analysis functions.</summary>
public static class CashFlows
{
    private const double BasisPoint = 1.0e-4;

    /// <summary>The earliest accrual start date of the coupons, or the earliest date of the other cash flows.</summary>
    /// <param name="leg">The leg.</param>
    /// <returns>The start date.</returns>
    /// <exception cref="ArgumentException">The leg is empty.</exception>
    public static Date StartDate(ImmutableArray<CashFlow> leg)
    {
        if (leg.IsDefaultOrEmpty)
        {
            throw new ArgumentException("empty leg", nameof(leg));
        }

        Date d = Date.MaxDate;
        foreach (CashFlow i in leg)
        {
            Date candidate = i is Coupon c ? c.AccrualStartDate : i.Date;
            d = candidate < d ? candidate : d;
        }

        return d;
    }

    /// <summary>The latest accrual end date of the coupons, or the latest date of the other cash flows.</summary>
    /// <param name="leg">The leg.</param>
    /// <returns>The maturity date.</returns>
    /// <exception cref="ArgumentException">The leg is empty.</exception>
    public static Date MaturityDate(ImmutableArray<CashFlow> leg)
    {
        if (leg.IsDefaultOrEmpty)
        {
            throw new ArgumentException("empty leg", nameof(leg));
        }

        Date d = Date.MinDate;
        foreach (CashFlow i in leg)
        {
            Date candidate = i is Coupon c ? c.AccrualEndDate : i.Date;
            d = candidate > d ? candidate : d;
        }

        return d;
    }

    /// <summary>
    /// The accrued period, as a fraction of a year, of the first coupon paid on the next payment date after the
    /// settlement date.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The accrued period; 0 when no coupon is still to be paid.</returns>
    public static double AccruedPeriod(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        if (leg.IsDefaultOrEmpty)
        {
            return 0;
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        int cf = NextCashFlow(leg, context, includeSettlementDateFlows, settlement);
        if (cf == leg.Length)
        {
            return 0;
        }

        Date paymentDate = leg[cf].Date;
        for (; cf < leg.Length && leg[cf].Date == paymentDate; ++cf)
        {
            if (leg[cf] is Coupon cp)
            {
                return cp.AccruedPeriod(settlement);
            }
        }

        return 0;
    }

    /// <summary>The accrued days of the first coupon paid on the next payment date after the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The accrued days; 0 when no coupon is still to be paid.</returns>
    public static int AccruedDays(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        if (leg.IsDefaultOrEmpty)
        {
            return 0;
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        int cf = NextCashFlow(leg, context, includeSettlementDateFlows, settlement);
        if (cf == leg.Length)
        {
            return 0;
        }

        Date paymentDate = leg[cf].Date;
        for (; cf < leg.Length && leg[cf].Date == paymentDate; ++cf)
        {
            if (leg[cf] is Coupon cp)
            {
                return cp.AccruedDays(settlement);
            }
        }

        return 0;
    }

    /// <summary>The accrued amount of the coupons paid on the next payment date after the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The accrued amount; 0 when no coupon is still to be paid.</returns>
    public static double AccruedAmount(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        if (leg.IsDefaultOrEmpty)
        {
            return 0;
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        int cf = NextCashFlow(leg, context, includeSettlementDateFlows, settlement);
        if (cf == leg.Length)
        {
            return 0.0;
        }

        Date paymentDate = leg[cf].Date;
        double result = 0.0;
        for (; cf < leg.Length && leg[cf].Date == paymentDate; ++cf)
        {
            if (leg[cf] is Coupon cp)
            {
                result += cp.AccruedAmount(settlement, context);
            }
        }

        return result;
    }

    /// <summary>
    /// The net present value of the cash flows: their sum, each discounted on the given curve, then forwarded to the
    /// NPV date.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The NPV.</returns>
    public static double Npv(
        ImmutableArray<CashFlow> leg,
        YieldTermStructure discountCurve,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        Date.ThrowIfDefault(npvDate, nameof(npvDate));
        if (leg.IsDefaultOrEmpty)
        {
            return 0.0;
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        Date npvAt = npvDate ?? settlement;

        double totalNPV = 0.0;
        foreach (CashFlow i in leg)
        {
            if (!i.HasOccurred(context, settlement, includeSettlementDateFlows) && !i.TradingExCoupon(context, settlement))
            {
                totalNPV += i.Amount(context) * discountCurve.Discount(i.Date);
            }
        }

        return totalNPV / discountCurve.Discount(npvAt);
    }

    /// <summary>
    /// The basis-point sensitivity of the cash flows: the change in NPV for a one-basis-point change in the coupon
    /// rates.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The BPS.</returns>
    public static double Bps(
        ImmutableArray<CashFlow> leg,
        YieldTermStructure discountCurve,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        Date.ThrowIfDefault(npvDate, nameof(npvDate));
        if (leg.IsDefaultOrEmpty)
        {
            return 0.0;
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        Date npvAt = npvDate ?? settlement;

        var calc = new BPSCalculator(discountCurve, context);
        foreach (CashFlow i in leg)
        {
            if (!i.HasOccurred(context, settlement, includeSettlementDateFlows) && !i.TradingExCoupon(context, settlement))
            {
                calc.Add(i);
            }
        }

        return BasisPoint * calc.Bps / discountCurve.Discount(npvAt);
    }

    /// <summary>
    /// The basis-point sensitivity of the cash flows: the change in NPV for a one-basis-point change in the coupon
    /// rates, each coupon's change discounted at a constant interest rate, period by period from the NPV date.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The interest rate the changes are discounted at; its compounding, frequency and day counter
    /// set the discounting.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The BPS.</returns>
    /// <remarks>
    /// Only the coupons count, and QuantLib checks no ex-coupon date here: a coupon traded ex-coupon still counts, as
    /// in QuantLib. A rate compounded after a simple first period, or simple in its final one, discounts that period
    /// simply.
    /// </remarks>
    public static double Bps(
        ImmutableArray<CashFlow> leg,
        InterestRate yield,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(yield);
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        Date.ThrowIfDefault(npvDate, nameof(npvDate));
        if (leg.IsDefaultOrEmpty)
        {
            return 0.0;
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        Date npvAt = npvDate ?? settlement;

        double bps = 0.0;
        double discount = 1.0;
        Date lastDate = npvAt;
        DayCounter dc = yield.DayCounter;
        bool inFirstPeriod = true;

        (InterestRate yMain, InterestRate yException) = CashFlowsHelpers.StepwiseYields(yield);

        foreach (CashFlow cashFlow in leg)
        {
            if (cashFlow.HasOccurred(context, settlement, includeSettlementDateFlows))
            {
                continue;
            }

            if (cashFlow is not Coupon coupon)
            {
                continue;
            }

            InterestRate yI = CashFlowsHelpers.Choose(
                yMain, yException, yield.Compounding, inFirstPeriod, cashFlow.Date, leg[^1].Date);

            double dt = CashFlowsHelpers.GetStepwiseDiscountTime(cashFlow, dc, npvAt, lastDate);
            discount *= yI.DiscountFactor(dt);
            bps += coupon.Nominal(context) * coupon.AccrualPeriod() * discount;
            lastDate = cashFlow.Date;
            inFirstPeriod = false;
        }

        return BasisPoint * bps;
    }

    /// <summary>The NPV and the BPS of the cash flows, computed together.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the values are given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The NPV and the BPS.</returns>
    public static (double Npv, double Bps) NpvBps(
        ImmutableArray<CashFlow> leg,
        YieldTermStructure discountCurve,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        Date.ThrowIfDefault(npvDate, nameof(npvDate));
        double npv = 0.0;
        double bps = 0.0;

        if (leg.IsDefaultOrEmpty)
        {
            return (npv, bps);
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        Date npvAt = npvDate ?? settlement;

        foreach (CashFlow cf in leg)
        {
            if (!cf.HasOccurred(context, settlement, includeSettlementDateFlows) && !cf.TradingExCoupon(context, settlement))
            {
                double df = discountCurve.Discount(cf.Date);
                npv += cf.Amount(context) * df;
                if (cf is Coupon cp)
                {
                    bps += cp.Nominal(context) * cp.AccrualPeriod() * df;
                }
            }
        }

        double d = discountCurve.Discount(npvAt);
        npv /= d;
        bps = BasisPoint * bps / d;

        return (npv, bps);
    }

    /// <summary>
    /// The position of the first cash flow that hasn't occurred at the settlement date, or the leg's length when
    /// there is none: QuantLib's <c>nextCashFlow</c>, whose iterator is a position here.
    /// </summary>
    private static int NextCashFlow(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows,
        Date settlementDate)
    {
        for (int i = 0; i < leg.Length; ++i)
        {
            if (!leg[i].HasOccurred(context, settlementDate, includeSettlementDateFlows))
            {
                return i;
            }
        }

        return leg.Length;
    }
}

/// <summary>
/// QuantLib's IRR utility functions that the functions on an interest rate share: the time each cash flow is discounted
/// over, and the rate each period is discounted at.
/// </summary>
file static class CashFlowsHelpers
{
    /// <summary>
    /// The time a cash flow is discounted over, from the previous one: QuantLib's <c>getStepwiseDiscountTime</c>, the
    /// year fraction with the coupon's reference period, or, for another flow, a reference period ending on its date.
    /// </summary>
    public static double GetStepwiseDiscountTime(CashFlow cashFlow, DayCounter dc, Date npvDate, Date lastDate)
    {
        Date cashFlowDate = cashFlow.Date;
        Date refStartDate, refEndDate;
        Coupon? coupon = cashFlow as Coupon;
        if (coupon is not null)
        {
            refStartDate = coupon.ReferencePeriodStart;
            refEndDate = coupon.ReferencePeriodEnd;
        }
        else
        {
            if (lastDate == npvDate)
            {
                // we don't have a previous coupon date,
                // so we fake it
                refStartDate = cashFlowDate - new Period(1, TimeUnit.Years);
            }
            else
            {
                refStartDate = lastDate;
            }

            refEndDate = cashFlowDate;
        }

        if (coupon is not null && lastDate != coupon.AccrualStartDate)
        {
            double couponPeriod = dc.YearFraction(coupon.AccrualStartDate, cashFlowDate, refStartDate, refEndDate);
            double accruedPeriod = dc.YearFraction(coupon.AccrualStartDate, lastDate, refStartDate, refEndDate);
            return couponPeriod - accruedPeriod;
        }

        return dc.YearFraction(lastDate, cashFlowDate, refStartDate, refEndDate);
    }

    /// <summary>
    /// The rate for the usual periods and, if any, the one for the exceptional period: QuantLib's
    /// <c>stepwiseYields</c>, compounded and simple for the rules that mix them.
    /// </summary>
    public static (InterestRate Main, InterestRate Exception) StepwiseYields(InterestRate y)
    {
        // we return the yield to be used in the normal case and,
        // if any, the one to be used in the exceptional case.
        switch (y.Compounding)
        {
            case Compounding.SimpleThenCompounded:
            case Compounding.CompoundedThenSimple:
                return (Clone(y, Compounding.Compounded), Clone(y, Compounding.Simple));
            default:
                return (y, y);
        }
    }

    /// <summary>
    /// The rate a period is discounted at: QuantLib's <c>choose</c>, the exceptional rate for the first period of a
    /// simple-then-compounded rule and for the final one of a compounded-then-simple rule.
    /// </summary>
    public static InterestRate Choose(
        InterestRate main,
        InterestRate exception,
        Compounding compounding,
        bool inFirstPeriod,
        Date cashFlowDate,
        Date finalCashFlowDate)
    {
        // The exceptions are positional: the first interval is
        // simple for SimpleThenCompounded and the final interval is
        // simple for CompoundedThenSimple.
        switch (compounding)
        {
            case Compounding.SimpleThenCompounded:
                return inFirstPeriod ? exception : main;
            case Compounding.CompoundedThenSimple:
                return cashFlowDate == finalCashFlowDate ? exception : main;
            default:
                return main;
        }
    }

    private static InterestRate Clone(InterestRate y, Compounding c) => new(y.Rate, y.DayCounter, c, y.Frequency);
}

/// <summary>Sums the coupons' basis-point sensitivity, and the other cash flows' NPV apart.</summary>
file sealed class BPSCalculator(YieldTermStructure discountCurve, EvaluationContext context)
{
    public double Bps { get; private set; }

    public double NonSensNPV { get; private set; }

    public void Add(CashFlow c)
    {
        if (c is Coupon cpn)
        {
            Bps += cpn.Nominal(context) * cpn.AccrualPeriod() * discountCurve.Discount(c.Date);
        }
        else
        {
            NonSensNPV += c.Amount(context) * discountCurve.Discount(c.Date);
        }
    }
}
