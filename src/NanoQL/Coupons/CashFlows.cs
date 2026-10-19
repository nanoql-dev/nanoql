// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 Charles Whitmore
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2010, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2008 Toyin Akin
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2010, 2011, 2012 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflows/cashflows.hpp ql/cashflows/cashflows.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Coupons;

/// <summary>Cash-flow analysis functions.</summary>
/// <remarks>
/// <para>
/// QuantLib's iterators are positions in the leg: <see cref="PreviousCashFlow"/> returns −1 where QuantLib returns
/// <c>rend()</c>, and <see cref="NextCashFlow"/> the leg's length where it returns <c>end()</c>. A date QuantLib returns
/// as its null date is <see langword="null"/>.
/// </para>
/// <para>
/// The yield is solved for by <see cref="NewtonSafe"/>, QuantLib's default, or by <see cref="Brent"/>, the solvers its
/// template is instantiated with. A Z-spread is solved for on a <see cref="ZeroSpreadedTermStructure"/> built at each
/// evaluation, where QuantLib's rewrites the value of one curve's quote; the values are the same. QuantLib's Z-spread
/// functions that take a day counter, which they ignore, are deprecated and not ported.
/// </para>
/// </remarks>
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

    /// <summary>Whether every cash flow of the leg has occurred at the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when no cash flow is still to be paid, an empty leg among them.</returns>
    public static bool IsExpired(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        if (leg.IsDefaultOrEmpty)
        {
            return true;
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        for (int i = leg.Length; i > 0; --i)
        {
            if (!leg[i - 1].HasOccurred(context, settlement, includeSettlementDateFlows))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The position of the last cash flow paying before or at the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The position, counted from the leg's start; −1 when no cash flow has been paid.</returns>
    public static int PreviousCashFlow(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        if (leg.IsDefaultOrEmpty)
        {
            return -1;
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        for (int i = leg.Length - 1; i >= 0; --i)
        {
            if (leg[i].HasOccurred(context, settlement, includeSettlementDateFlows))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The position of the first cash flow paying after the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The position; the leg's length when every cash flow has been paid.</returns>
    public static int NextCashFlow(
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
        for (int i = 0; i < leg.Length; ++i)
        {
            if (!leg[i].HasOccurred(context, settlement, includeSettlementDateFlows))
            {
                return i;
            }
        }

        return leg.Length;
    }

    /// <summary>The date of the last cash flow paying before or at the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no cash flow has been paid.</returns>
    public static Date? PreviousCashFlowDate(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        int cf = PreviousCashFlow(leg, context, includeSettlementDateFlows, settlementDate);
        if (cf == -1)
        {
            return null;
        }

        return leg[cf].Date;
    }

    /// <summary>The date of the first cash flow paying after the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when every cash flow has been paid.</returns>
    public static Date? NextCashFlowDate(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        int cf = NextCashFlow(leg, context, includeSettlementDateFlows, settlementDate);
        if (cf == Length(leg))
        {
            return null;
        }

        return leg[cf].Date;
    }

    /// <summary>
    /// The amount paid on the date of the last cash flow paying before or at the settlement date: the sum of every
    /// cash flow on that date.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The amount; 0 when no cash flow has been paid.</returns>
    public static double PreviousCashFlowAmount(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        int cf = PreviousCashFlow(leg, context, includeSettlementDateFlows, settlementDate);
        if (cf == -1)
        {
            return 0.0;
        }

        Date paymentDate = leg[cf].Date;
        double result = 0.0;
        for (; cf >= 0 && leg[cf].Date == paymentDate; --cf)
        {
            result += leg[cf].Amount(context);
        }

        return result;
    }

    /// <summary>
    /// The amount paid on the date of the first cash flow paying after the settlement date: the sum of every cash
    /// flow on that date.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The amount; 0 when every cash flow has been paid.</returns>
    public static double NextCashFlowAmount(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        int cf = NextCashFlow(leg, context, includeSettlementDateFlows, settlementDate);
        if (cf == Length(leg))
        {
            return 0.0;
        }

        Date paymentDate = leg[cf].Date;
        double result = 0.0;
        for (; cf < leg.Length && leg[cf].Date == paymentDate; ++cf)
        {
            result += leg[cf].Amount(context);
        }

        return result;
    }

    /// <summary>
    /// The rate of the coupons paid on the date of the last cash flow paying before or at the settlement date: the sum
    /// of their rates, which must have the same nominal, accrual period and day counter.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The rate; 0 when no cash flow has been paid.</returns>
    /// <exception cref="ArgumentException">Two coupons on that date differ in nominal, accrual period or day counter.</exception>
    /// <exception cref="NanoQLException">No coupon is paid on that date.</exception>
    public static double PreviousCouponRate(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        int cf = PreviousCashFlow(leg, context, includeSettlementDateFlows, settlementDate);
        return CashFlowsHelpers.AggregateRate(leg, cf, -1, context);
    }

    /// <summary>
    /// The rate of the coupons paid on the date of the first cash flow paying after the settlement date: the sum of
    /// their rates, which must have the same nominal, accrual period and day counter.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The rate; 0 when every cash flow has been paid.</returns>
    /// <exception cref="ArgumentException">Two coupons on that date differ in nominal, accrual period or day counter.</exception>
    /// <exception cref="NanoQLException">No coupon is paid on that date.</exception>
    public static double NextCouponRate(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        int cf = NextCashFlow(leg, context, includeSettlementDateFlows, settlementDate);
        return CashFlowsHelpers.AggregateRate(leg, cf, 1, context);
    }

    /// <summary>The nominal of the first coupon paid on the next payment date after the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The nominal; 0 when no coupon is still to be paid on that date.</returns>
    public static double Nominal(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        int cf = NextCashFlow(leg, context, includeSettlementDateFlows, settlementDate);
        if (cf == Length(leg))
        {
            return 0.0;
        }

        Date paymentDate = leg[cf].Date;
        for (; cf < leg.Length && leg[cf].Date == paymentDate; ++cf)
        {
            if (leg[cf] is Coupon cp)
            {
                return cp.Nominal(context);
            }
        }

        return 0.0;
    }

    /// <summary>The accrual start date of the first coupon paid on the next payment date after the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no coupon is still to be paid on that date.</returns>
    public static Date? AccrualStartDate(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        Coupon? cp = NextCoupon(leg, context, includeSettlementDateFlows, settlementDate);
        return cp?.AccrualStartDate;
    }

    /// <summary>The accrual end date of the first coupon paid on the next payment date after the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no coupon is still to be paid on that date.</returns>
    public static Date? AccrualEndDate(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        Coupon? cp = NextCoupon(leg, context, includeSettlementDateFlows, settlementDate);
        return cp?.AccrualEndDate;
    }

    /// <summary>
    /// The reference period start of the first coupon paid on the next payment date after the settlement date.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no coupon is still to be paid on that date.</returns>
    public static Date? ReferencePeriodStart(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        Coupon? cp = NextCoupon(leg, context, includeSettlementDateFlows, settlementDate);
        return cp?.ReferencePeriodStart;
    }

    /// <summary>The reference period end of the first coupon paid on the next payment date after the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no coupon is still to be paid on that date.</returns>
    public static Date? ReferencePeriodEnd(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        Coupon? cp = NextCoupon(leg, context, includeSettlementDateFlows, settlementDate);
        return cp?.ReferencePeriodEnd;
    }

    /// <summary>
    /// The accrual period, as a fraction of a year, of the first coupon paid on the next payment date after the
    /// settlement date.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The accrual period; 0 when no coupon is still to be paid on that date.</returns>
    public static double AccrualPeriod(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        Coupon? cp = NextCoupon(leg, context, includeSettlementDateFlows, settlementDate);
        return cp is null ? 0 : cp.AccrualPeriod();
    }

    /// <summary>The accrual days of the first coupon paid on the next payment date after the settlement date.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count as not yet paid.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <returns>The accrual days; 0 when no coupon is still to be paid on that date.</returns>
    public static int AccrualDays(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null)
    {
        Coupon? cp = NextCoupon(leg, context, includeSettlementDateFlows, settlementDate);
        return cp is null ? 0 : cp.AccrualDays();
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
    /// The at-the-money rate of the cash flows: the fixed rate for which a fixed-rate leg equivalent to the cash flows
    /// has the required NPV on the given curve.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the required NPV is given at; the settlement date when <see langword="null"/>.</param>
    /// <param name="npv">The required NPV; the cash flows' own NPV when <see langword="null"/>.</param>
    /// <returns>The rate; 0 when the coupons' required NPV is 0.</returns>
    /// <exception cref="NanoQLException">The coupons' BPS is 0.</exception>
    public static double AtmRate(
        ImmutableArray<CashFlow> leg,
        YieldTermStructure discountCurve,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null,
        double? npv = null)
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

        double legNpv = 0.0;
        var calc = new BPSCalculator(discountCurve, context);
        foreach (CashFlow i in leg)
        {
            if (!i.HasOccurred(context, settlement, includeSettlementDateFlows) && !i.TradingExCoupon(context, settlement))
            {
                legNpv += i.Amount(context) * discountCurve.Discount(i.Date);
                calc.Add(i);
            }
        }

        double targetNpv;
        if (npv is not double required)
        {
            targetNpv = legNpv - calc.NonSensNPV;
        }
        else
        {
            targetNpv = required;
            targetNpv *= discountCurve.Discount(npvAt);
            targetNpv -= calc.NonSensNPV;
        }

        if (targetNpv == 0.0)
        {
            return 0.0;
        }

        double bps = calc.Bps;
        if (!(bps != 0.0))
        {
            throw new NanoQLException("null bps: impossible atm rate");
        }

        return targetNpv / bps;
    }

    /// <summary>
    /// The net present value of the cash flows: their sum, each discounted at a constant interest rate, period by
    /// period from the NPV date.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The interest rate; its compounding, frequency and day counter set the discounting.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The NPV.</returns>
    /// <remarks>
    /// A cash flow traded ex-coupon counts with no amount, though its period still discounts the later ones. A rate
    /// compounded after a simple first period, or simple in its final one, discounts that period simply.
    /// </remarks>
    public static double Npv(
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

        double npv = 0.0;
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

            double amount = cashFlow.Amount(context);
            if (cashFlow.TradingExCoupon(context, settlement))
            {
                amount = 0.0;
            }

            InterestRate yI = CashFlowsHelpers.Choose(
                yMain, yException, yield.Compounding, inFirstPeriod, cashFlow.Date, leg[^1].Date);

            double dt = CashFlowsHelpers.GetStepwiseDiscountTime(cashFlow, dc, npvAt, lastDate);
            discount *= yI.DiscountFactor(dt);
            npv += amount * discount;

            lastDate = cashFlow.Date;
            inFirstPeriod = false;
        }

        return npv;
    }

    /// <summary>
    /// The net present value of the cash flows at a constant interest rate, given by its rate and conventions:
    /// <see cref="Npv(ImmutableArray{CashFlow}, InterestRate, EvaluationContext, bool?, Date?, Date?)"/>.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The rate.</param>
    /// <param name="dayCounter">The rate's day counter.</param>
    /// <param name="compounding">The rate's compounding.</param>
    /// <param name="frequency">The rate's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The NPV.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">A compounded rule with no frequency or <see cref="Frequency.Once"/>.</exception>
    public static double Npv(
        ImmutableArray<CashFlow> leg,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        InterestRate y = CashFlowsHelpers.Rate(yield, dayCounter, compounding, frequency);
        return Npv(leg, y, context, includeSettlementDateFlows, settlementDate, npvDate);
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

    /// <summary>
    /// The basis-point sensitivity of the cash flows at a constant interest rate, given by its rate and conventions:
    /// <see cref="Bps(ImmutableArray{CashFlow}, InterestRate, EvaluationContext, bool?, Date?, Date?)"/>.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The rate.</param>
    /// <param name="dayCounter">The rate's day counter.</param>
    /// <param name="compounding">The rate's compounding.</param>
    /// <param name="frequency">The rate's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The BPS.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">A compounded rule with no frequency or <see cref="Frequency.Once"/>.</exception>
    public static double Bps(
        ImmutableArray<CashFlow> leg,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        InterestRate y = CashFlowsHelpers.Rate(yield, dayCounter, compounding, frequency);
        return Bps(leg, y, context, includeSettlementDateFlows, settlementDate, npvDate);
    }

    /// <summary>
    /// The implied internal rate of return: the rate at which the cash flows' NPV equals the given one, solved for by
    /// <see cref="NewtonSafe"/>.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="npv">The NPV, the dirty price, the rate must reproduce.</param>
    /// <param name="dayCounter">The rate's day counter.</param>
    /// <param name="compounding">The rate's compounding.</param>
    /// <param name="frequency">The rate's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the settlement date when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxIterations">The solver's maximum number of evaluations.</param>
    /// <param name="guess">The solver's initial guess; its first step is a tenth of it.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxIterations"/> is negative, or the compounding or the frequency isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// No cash flow still to be paid has the sign opposite the NPV's, or the accuracy isn't positive.
    /// </exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    public static double Yield(
        ImmutableArray<CashFlow> leg,
        double npv,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null,
        double accuracy = 1.0e-10,
        int maxIterations = 100,
        double guess = 0.05)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);
        NewtonSafe solver = new NewtonSafe().SetMaxEvaluations(maxIterations);
        return Yield(
            solver,
            leg,
            npv,
            dayCounter,
            compounding,
            frequency,
            context,
            includeSettlementDateFlows,
            settlementDate,
            npvDate,
            accuracy,
            guess);
    }

    /// <summary>
    /// The implied internal rate of return, solved for by the given <see cref="NewtonSafe"/> solver: QuantLib's
    /// <c>yield&lt;NewtonSafe&gt;</c>.
    /// </summary>
    /// <param name="solver">The solver, with its maximum number of evaluations.</param>
    /// <param name="leg">The leg.</param>
    /// <param name="npv">The NPV, the dirty price, the rate must reproduce.</param>
    /// <param name="dayCounter">The rate's day counter.</param>
    /// <param name="compounding">The rate's compounding.</param>
    /// <param name="frequency">The rate's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the settlement date when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="guess">The solver's initial guess; its first step is a tenth of it.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// No cash flow still to be paid has the sign opposite the NPV's, or the accuracy isn't positive.
    /// </exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    public static double Yield(
        NewtonSafe solver,
        ImmutableArray<CashFlow> leg,
        double npv,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null,
        double accuracy = 1.0e-10,
        double guess = 0.05)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(solver);
        var objFunction = new IrrFinder(
            leg, npv, dayCounter, compounding, frequency, includeSettlementDateFlows, settlementDate, npvDate, context);
        return solver.Solve(objFunction, accuracy, guess, guess / 10.0);
    }

    /// <summary>
    /// The implied internal rate of return, solved for by the given <see cref="Brent"/> solver: QuantLib's
    /// <c>yield&lt;Brent&gt;</c>.
    /// </summary>
    /// <param name="solver">The solver, with its maximum number of evaluations.</param>
    /// <param name="leg">The leg.</param>
    /// <param name="npv">The NPV, the dirty price, the rate must reproduce.</param>
    /// <param name="dayCounter">The rate's day counter.</param>
    /// <param name="compounding">The rate's compounding.</param>
    /// <param name="frequency">The rate's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the settlement date when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="guess">The solver's initial guess; its first step is a tenth of it.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// No cash flow still to be paid has the sign opposite the NPV's, or the accuracy isn't positive.
    /// </exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    public static double Yield(
        Brent solver,
        ImmutableArray<CashFlow> leg,
        double npv,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null,
        double accuracy = 1.0e-10,
        double guess = 0.05)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(solver);
        var objFunction = new IrrFinder(
            leg, npv, dayCounter, compounding, frequency, includeSettlementDateFlows, settlementDate, npvDate, context);
        return solver.Solve(objFunction, accuracy, guess, guess / 10.0);
    }

    /// <summary>The duration of the cash flows at a constant interest rate.</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The interest rate.</param>
    /// <param name="type">The duration: simple, the cash flows' times weighted by their present values; modified,
    /// −(1/P)·∂P/∂y; or Macaulay's, (1 + y/N) times the modified duration, for a compounded rate with N periods a
    /// year.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the times are measured from; the settlement date when <see langword="null"/>.</param>
    /// <returns>The duration; 0 when the cash flows' NPV is 0.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The type isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">Macaulay's duration of a rate that isn't compounded.</exception>
    public static double Duration(
        ImmutableArray<CashFlow> leg,
        InterestRate yield,
        DurationType type,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(yield);
        EnumArguments.Check(type, nameof(type));
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        Date.ThrowIfDefault(npvDate, nameof(npvDate));
        if (leg.IsDefaultOrEmpty)
        {
            return 0.0;
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        Date npvAt = npvDate ?? settlement;

        switch (type)
        {
            case DurationType.Simple:
                return CashFlowsHelpers.SimpleDuration(leg, yield, includeSettlementDateFlows, settlement, npvAt, context);
            case DurationType.Modified:
                return CashFlowsHelpers.ModifiedDuration(leg, yield, includeSettlementDateFlows, settlement, npvAt, context);
            case DurationType.Macaulay:
                return CashFlowsHelpers.MacaulayDuration(leg, yield, includeSettlementDateFlows, settlement, npvAt, context);
            default:
                throw new NanoQLException("unknown duration type");
        }
    }

    /// <summary>
    /// The duration of the cash flows at a constant interest rate, given by its rate and conventions:
    /// <see cref="Duration(ImmutableArray{CashFlow}, InterestRate, DurationType, EvaluationContext, bool?, Date?, Date?)"/>.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The rate.</param>
    /// <param name="dayCounter">The rate's day counter.</param>
    /// <param name="compounding">The rate's compounding.</param>
    /// <param name="frequency">The rate's frequency.</param>
    /// <param name="type">The duration's type.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the times are measured from; the settlement date when <see langword="null"/>.</param>
    /// <returns>The duration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The compounding, the frequency or the type isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A compounded rule with no frequency or <see cref="Frequency.Once"/>, or Macaulay's duration of a rate that isn't
    /// compounded.
    /// </exception>
    public static double Duration(
        ImmutableArray<CashFlow> leg,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        DurationType type,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        InterestRate y = CashFlowsHelpers.Rate(yield, dayCounter, compounding, frequency);
        return Duration(leg, y, type, context, includeSettlementDateFlows, settlementDate, npvDate);
    }

    /// <summary>The convexity of the cash flows at a constant interest rate: (1/P)·∂²P/∂y².</summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The interest rate.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the times are measured from; the settlement date when <see langword="null"/>.</param>
    /// <returns>The convexity; 0 when the cash flows' NPV is 0.</returns>
    public static double Convexity(
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

        CashFlowResults results = CashFlowsHelpers.Results(
            leg, yield, includeSettlementDateFlows, settlement, npvAt, context, true, true, false);
        if (results.Npv == 0.0)
        {
            // no cashflows
            return 0.0;
        }

        return results.SecondDerivative / results.Npv;
    }

    /// <summary>
    /// The convexity of the cash flows at a constant interest rate, given by its rate and conventions:
    /// <see cref="Convexity(ImmutableArray{CashFlow}, InterestRate, EvaluationContext, bool?, Date?, Date?)"/>.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The rate.</param>
    /// <param name="dayCounter">The rate's day counter.</param>
    /// <param name="compounding">The rate's compounding.</param>
    /// <param name="frequency">The rate's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the times are measured from; the settlement date when <see langword="null"/>.</param>
    /// <returns>The convexity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">A compounded rule with no frequency or <see cref="Frequency.Once"/>.</exception>
    public static double Convexity(
        ImmutableArray<CashFlow> leg,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        InterestRate y = CashFlowsHelpers.Rate(yield, dayCounter, compounding, frequency);
        return Convexity(leg, y, context, includeSettlementDateFlows, settlementDate, npvDate);
    }

    /// <summary>
    /// The basis-point value of the cash flows at a constant interest rate: the change in NPV for a one-basis-point
    /// change in the rate, from the second-order Taylor expansion.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The interest rate.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The basis-point value.</returns>
    public static double BasisPointValue(
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

        double npv = Npv(leg, yield, context, includeSettlementDateFlows, settlement, npvAt);
        double modifiedDuration = Duration(
            leg, yield, DurationType.Modified, context, includeSettlementDateFlows, settlement, npvAt);
        double convexity = Convexity(leg, yield, context, includeSettlementDateFlows, settlement, npvAt);
        double delta = -modifiedDuration * npv;
        double gamma = convexity * npv;

        double shift = 0.0001;
        delta *= shift;
        gamma *= shift * shift;

        return delta + (0.5 * gamma);
    }

    /// <summary>
    /// The basis-point value of the cash flows at a constant interest rate, given by its rate and conventions:
    /// <see cref="BasisPointValue(ImmutableArray{CashFlow}, InterestRate, EvaluationContext, bool?, Date?, Date?)"/>.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The rate.</param>
    /// <param name="dayCounter">The rate's day counter.</param>
    /// <param name="compounding">The rate's compounding.</param>
    /// <param name="frequency">The rate's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The basis-point value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">A compounded rule with no frequency or <see cref="Frequency.Once"/>.</exception>
    public static double BasisPointValue(
        ImmutableArray<CashFlow> leg,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        InterestRate y = CashFlowsHelpers.Rate(yield, dayCounter, compounding, frequency);
        return BasisPointValue(leg, y, context, includeSettlementDateFlows, settlementDate, npvDate);
    }

    /// <summary>
    /// The yield value of a basis point of the cash flows at a constant interest rate: the derivative of the rate with
    /// respect to the price, times 0.01.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The interest rate.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The yield value of a basis point.</returns>
    public static double YieldValueBasisPoint(
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

        double npv = Npv(leg, yield, context, includeSettlementDateFlows, settlement, npvAt);
        double modifiedDuration = Duration(
            leg, yield, DurationType.Modified, context, includeSettlementDateFlows, settlement, npvAt);

        double shift = 0.01;
        return (1.0 / (-npv * modifiedDuration)) * shift;
    }

    /// <summary>
    /// The yield value of a basis point of the cash flows at a constant interest rate, given by its rate and
    /// conventions:
    /// <see cref="YieldValueBasisPoint(ImmutableArray{CashFlow}, InterestRate, EvaluationContext, bool?, Date?, Date?)"/>.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="yield">The rate.</param>
    /// <param name="dayCounter">The rate's day counter.</param>
    /// <param name="compounding">The rate's compounding.</param>
    /// <param name="frequency">The rate's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The yield value of a basis point.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">A compounded rule with no frequency or <see cref="Frequency.Once"/>.</exception>
    public static double YieldValueBasisPoint(
        ImmutableArray<CashFlow> leg,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        InterestRate y = CashFlowsHelpers.Rate(yield, dayCounter, compounding, frequency);
        return YieldValueBasisPoint(leg, y, context, includeSettlementDateFlows, settlementDate, npvDate);
    }

    /// <summary>
    /// The net present value of the cash flows on a curve with a Z-spread: each discounted on the curve whose zero
    /// rates are the given curve's plus the spread (see "Credit Spreads Explained", Lehman Brothers European Fixed
    /// Income Research, March 2004, D. O'Kane).
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="discount">The curve the spread is added to.</param>
    /// <param name="zSpread">The Z-spread, in terms of the curve's day counter.</param>
    /// <param name="compounding">The compounding the spread is added in.</param>
    /// <param name="frequency">The frequency the spread is added in.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the value is given at; the settlement date when <see langword="null"/>.</param>
    /// <returns>The NPV.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">A compounded rule with no frequency or <see cref="Frequency.Once"/>.</exception>
    public static double Npv(
        ImmutableArray<CashFlow> leg,
        YieldTermStructure discount,
        double zSpread,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discount);
        EnumArguments.Check(compounding, nameof(compounding));
        EnumArguments.Check(frequency, nameof(frequency));
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        Date.ThrowIfDefault(npvDate, nameof(npvDate));
        if (leg.IsDefaultOrEmpty)
        {
            return 0.0;
        }

        Date settlement = settlementDate ?? context.EvaluationDate;
        Date npvAt = npvDate ?? settlement;

        var spreadedCurve = new ZeroSpreadedTermStructure(discount, zSpread, compounding, frequency);

        return Npv(leg, spreadedCurve, context, includeSettlementDateFlows, settlement, npvAt);
    }

    /// <summary>
    /// The implied Z-spread: the spread at which the cash flows' NPV on the spreaded curve equals the given one, solved
    /// for by <see cref="Brent"/> from the guess with a first step of 0.01.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="npv">The NPV the spread must reproduce.</param>
    /// <param name="discount">The curve the spread is added to.</param>
    /// <param name="compounding">The compounding the spread is added in.</param>
    /// <param name="frequency">The frequency the spread is added in.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="includeSettlementDateFlows">Whether cash flows on the settlement date count.</param>
    /// <param name="settlementDate">The settlement date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="npvDate">The date the NPV is given at; the settlement date when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxIterations">The solver's maximum number of evaluations.</param>
    /// <param name="guess">The solver's initial guess.</param>
    /// <returns>The Z-spread.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxIterations"/> is negative, or the compounding or the frequency isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The accuracy isn't positive, or a compounded rule has no frequency or <see cref="Frequency.Once"/>.
    /// </exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    /// <remarks>An empty leg isn't refused, as in QuantLib: its NPV is 0 at every spread.</remarks>
    public static double ZSpread(
        ImmutableArray<CashFlow> leg,
        double npv,
        YieldTermStructure discount,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        bool? includeSettlementDateFlows = null,
        Date? settlementDate = null,
        Date? npvDate = null,
        double accuracy = 1.0e-10,
        int maxIterations = 100,
        double guess = 0.0)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discount);
        EnumArguments.Check(compounding, nameof(compounding));
        EnumArguments.Check(frequency, nameof(frequency));
        Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
        Date.ThrowIfDefault(npvDate, nameof(npvDate));
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);

        Date settlement = settlementDate ?? context.EvaluationDate;
        Date npvAt = npvDate ?? settlement;

        // QuantLib rewrites the spread of one curve at each evaluation; here each evaluation builds its own curve.
        double ObjFunction(double zSpread)
        {
            var spreadedCurve = new ZeroSpreadedTermStructure(discount, zSpread, compounding, frequency);
            double spreadedNpv = Npv(leg, spreadedCurve, context, includeSettlementDateFlows, settlement, npvAt);
            return npv - spreadedNpv;
        }

        Brent solver = new Brent().SetMaxEvaluations(maxIterations);
        double step = 0.01;
        return solver.Solve(ObjFunction, accuracy, guess, step);
    }

    // The leg's length, 0 for a default array.
    private static int Length(ImmutableArray<CashFlow> leg) => leg.IsDefault ? 0 : leg.Length;

    // The first coupon paid on the next payment date after the settlement date, or null for none.
    private static Coupon? NextCoupon(
        ImmutableArray<CashFlow> leg,
        EvaluationContext context,
        bool? includeSettlementDateFlows,
        Date? settlementDate)
    {
        int cf = NextCashFlow(leg, context, includeSettlementDateFlows, settlementDate);
        if (cf == Length(leg))
        {
            return null;
        }

        Date paymentDate = leg[cf].Date;
        for (; cf < leg.Length && leg[cf].Date == paymentDate; ++cf)
        {
            if (leg[cf] is Coupon cp)
            {
                return cp;
            }
        }

        return null;
    }

    /// <summary>
    /// The function whose zero is the internal rate of return: the cash flows' NPV at a rate less the given one, and
    /// its derivative, −(modified duration)·NPV.
    /// </summary>
    private readonly struct IrrFinder : IDerivativeSolver1DFunction
    {
        private readonly ImmutableArray<CashFlow> leg;
        private readonly double npv;
        private readonly DayCounter dayCounter;
        private readonly Compounding compounding;
        private readonly Frequency frequency;
        private readonly bool? includeSettlementDateFlows;
        private readonly Date settlementDate;
        private readonly Date npvDate;
        private readonly EvaluationContext context;

        public IrrFinder(
            ImmutableArray<CashFlow> leg,
            double npv,
            DayCounter dayCounter,
            Compounding comp,
            Frequency freq,
            bool? includeSettlementDateFlows,
            Date? settlementDate,
            Date? npvDate,
            EvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(dayCounter);
            EnumArguments.Check(comp, "compounding");
            EnumArguments.Check(freq, "frequency");
            Date.ThrowIfDefault(settlementDate, nameof(settlementDate));
            Date.ThrowIfDefault(npvDate, nameof(npvDate));
            this.leg = leg.IsDefault ? [] : leg;
            this.npv = npv;
            this.dayCounter = dayCounter;
            compounding = comp;
            frequency = freq;
            this.includeSettlementDateFlows = includeSettlementDateFlows;
            this.context = context;

            this.settlementDate = settlementDate ?? context.EvaluationDate;
            this.npvDate = npvDate ?? this.settlementDate;

            CheckSign();
        }

        public double Evaluate(double y)
        {
            var yield = new InterestRate(y, dayCounter, compounding, frequency);
            double nPV = Npv(leg, yield, context, includeSettlementDateFlows, settlementDate, npvDate);
            return nPV - npv;
        }

        public double Derivative(double y)
        {
            var yield = new InterestRate(y, dayCounter, compounding, frequency);
            double p = Npv(leg, yield, context, includeSettlementDateFlows, settlementDate, npvDate);
            return -CashFlowsHelpers.ModifiedDuration(
                leg, yield, includeSettlementDateFlows, settlementDate, npvDate, context) * p;
        }

        private void CheckSign()
        {
            // depending on the sign of the market price, check that cash
            // flows of the opposite sign have been specified (otherwise
            // IRR is nonsensical.)

            int lastSign = CashFlowsHelpers.Sign(-npv), signChanges = 0;
            foreach (CashFlow i in leg)
            {
                if (!i.HasOccurred(context, settlementDate, includeSettlementDateFlows)
                    && !i.TradingExCoupon(context, settlementDate))
                {
                    int thisSign = CashFlowsHelpers.Sign(i.Amount(context));
                    if (lastSign * thisSign < 0)
                    {
                        // sign change
                        signChanges++;
                    }

                    if (thisSign != 0)
                    {
                        lastSign = thisSign;
                    }
                }
            }

            if (!(signChanges > 0))
            {
                throw new ArgumentException(
                    "the given cash flows cannot result in the given market price due to their sign", nameof(npv));
            }
        }
    }
}

/// <summary>The NPV and the derivatives of the cash flows at a constant interest rate, and their weighted times.</summary>
file struct CashFlowResults
{
    public double Npv;
    public double FirstDerivative;
    public double SecondDerivative;
    public double WeightedTime;
}

/// <summary>
/// QuantLib's coupon and IRR utility functions that the functions share: the coupon rate aggregated on a date, the
/// time each cash flow is discounted over, the rate each period is discounted at, the NPV and its derivatives at a
/// constant rate, and the durations.
/// </summary>
file static class CashFlowsHelpers
{
    /// <summary>
    /// The sum of the rates of the coupons on the date of the cash flow at a position, walking the leg in a direction:
    /// QuantLib's <c>aggregateRate</c>, which every coupon on that date must share its nominal, accrual period and day
    /// counter with.
    /// </summary>
    public static double AggregateRate(ImmutableArray<CashFlow> leg, int first, int step, EvaluationContext context)
    {
        int length = leg.IsDefault ? 0 : leg.Length;
        if (!(first >= 0 && first < length))
        {
            return 0.0;
        }

        Date paymentDate = leg[first].Date;
        bool firstCouponFound = false;
        double nominal = 0.0;
        double accrualPeriod = 0.0;
        DayCounter? dc = null;
        double result = 0.0;
        for (; first >= 0 && first < length && leg[first].Date == paymentDate; first += step)
        {
            if (leg[first] is Coupon cp)
            {
                if (firstCouponFound)
                {
                    if (!(nominal == cp.Nominal(context) && accrualPeriod == cp.AccrualPeriod() && dc == cp.DayCounter))
                    {
                        throw new ArgumentException($"cannot aggregate two different coupons on {paymentDate}", nameof(leg));
                    }
                }
                else
                {
                    firstCouponFound = true;
                    nominal = cp.Nominal(context);
                    accrualPeriod = cp.AccrualPeriod();
                    dc = cp.DayCounter;
                }

                result += cp.Rate(context);
            }
        }

        if (!firstCouponFound)
        {
            throw new NanoQLException($"no coupon paid at cashflow date {paymentDate}");
        }

        return result;
    }

    /// <summary>QuantLib's <c>sign</c>: 0 for zero, 1 for a positive value, and −1 otherwise, NaN among them.</summary>
    public static int Sign(double x)
    {
        const double zero = 0.0;
        if (x == zero)
        {
            return 0;
        }
        else if (x > zero)
        {
            return 1;
        }
        else
        {
            return -1;
        }
    }

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

    /// <summary>
    /// The NPV of the cash flows at a constant rate and, as asked, its first and second derivatives with respect to
    /// the rate and the cash flows' times weighted by their present values: QuantLib's <c>cashFlowResults</c>, each
    /// period's discount factor multiplied into the earlier ones' and differentiated as a product.
    /// </summary>
    public static CashFlowResults Results(
        ImmutableArray<CashFlow> leg,
        InterestRate yield,
        bool? includeSettlementDateFlows,
        Date settlementDate,
        Date npvDate,
        EvaluationContext context,
        bool computeFirstDerivative,
        bool computeSecondDerivative,
        bool computeWeightedTime)
    {
        var results = default(CashFlowResults);
        double discount = 1.0;
        double firstDerivative = 0.0;
        double secondDerivative = 0.0;
        double t = 0.0;
        Date lastDate = npvDate;
        DayCounter dc = yield.DayCounter;
        bool inFirstPeriod = true;
        double previousDiscount;
        double previousFirstDerivative = 0.0;
        double previousSecondDerivative = 0.0;
        double thisDiscount;
        double thisFirstDerivative;
        double thisSecondDerivative;

        (InterestRate yMain, InterestRate yException) = StepwiseYields(yield);

        foreach (CashFlow cashFlow in leg)
        {
            if (cashFlow.HasOccurred(context, settlementDate, includeSettlementDateFlows))
            {
                continue;
            }

            double amount = cashFlow.Amount(context);
            if (cashFlow.TradingExCoupon(context, settlementDate))
            {
                amount = 0.0;
            }

            double dt = GetStepwiseDiscountTime(cashFlow, dc, npvDate, lastDate);
            t += dt;

            previousDiscount = discount;
            if (computeFirstDerivative)
            {
                previousFirstDerivative = firstDerivative;
                if (computeSecondDerivative)
                {
                    previousSecondDerivative = secondDerivative;
                }
            }

            InterestRate y = Choose(yMain, yException, yield.Compounding, inFirstPeriod, cashFlow.Date, leg[^1].Date);

            thisDiscount = y.DiscountFactor(dt);
            discount = previousDiscount * thisDiscount;

            if (computeFirstDerivative)
            {
                // Differentiate the product of this interval's discount
                // factor and all preceding factors.
                thisFirstDerivative = y.DiscountFactorFirstDerivative(dt);
                firstDerivative = (previousFirstDerivative * thisDiscount) + (previousDiscount * thisFirstDerivative);
                results.FirstDerivative += amount * firstDerivative;
                if (computeSecondDerivative)
                {
                    thisSecondDerivative = y.DiscountFactorSecondDerivative(dt);
                    secondDerivative = (previousSecondDerivative * thisDiscount)
                        + (2.0 * previousFirstDerivative * thisFirstDerivative)
                        + (previousDiscount * thisSecondDerivative);
                    results.SecondDerivative += amount * secondDerivative;
                }
            }

            results.Npv += amount * discount;

            if (computeWeightedTime)
            {
                results.WeightedTime += t * amount * discount;
            }

            lastDate = cashFlow.Date;
            inFirstPeriod = false;
        }

        return results;
    }

    /// <summary>The simple duration: the cash flows' times weighted by their present values.</summary>
    public static double SimpleDuration(
        ImmutableArray<CashFlow> leg,
        InterestRate y,
        bool? includeSettlementDateFlows,
        Date settlementDate,
        Date npvDate,
        EvaluationContext context)
    {
        if (leg.IsDefaultOrEmpty)
        {
            return 0.0;
        }

        CashFlowResults results = Results(
            leg, y, includeSettlementDateFlows, settlementDate, npvDate, context, false, false, true);
        if (results.Npv == 0.0)
        {
            // no cashflows
            return 0.0;
        }

        return results.WeightedTime / results.Npv;
    }

    /// <summary>The modified duration: −(1/P)·∂P/∂y.</summary>
    public static double ModifiedDuration(
        ImmutableArray<CashFlow> leg,
        InterestRate y,
        bool? includeSettlementDateFlows,
        Date settlementDate,
        Date npvDate,
        EvaluationContext context)
    {
        if (leg.IsDefaultOrEmpty)
        {
            return 0.0;
        }

        CashFlowResults results = Results(
            leg, y, includeSettlementDateFlows, settlementDate, npvDate, context, true, false, false);
        if (results.Npv == 0.0)
        {
            // no cashflows
            return 0.0;
        }

        return -results.FirstDerivative / results.Npv;
    }

    /// <summary>Macaulay's duration of a compounded rate with N periods a year: (1 + y/N) times the modified one.</summary>
    public static double MacaulayDuration(
        ImmutableArray<CashFlow> leg,
        InterestRate y,
        bool? includeSettlementDateFlows,
        Date settlementDate,
        Date npvDate,
        EvaluationContext context)
    {
        if (!(y.Compounding == Compounding.Compounded))
        {
            throw new ArgumentException("compounded rate required", "yield");
        }

        return (1.0 + (y.Rate / (int)y.Frequency))
            * ModifiedDuration(leg, y, includeSettlementDateFlows, settlementDate, npvDate, context);
    }

    /// <summary>
    /// The interest rate a function given a rate and its conventions computes with, its compounding and frequency
    /// checked under the function's own parameter names.
    /// </summary>
    public static InterestRate Rate(double yield, DayCounter dayCounter, Compounding compounding, Frequency frequency)
    {
        ArgumentNullException.ThrowIfNull(dayCounter);
        EnumArguments.Check(compounding, nameof(compounding));
        EnumArguments.Check(frequency, nameof(frequency));
        return new InterestRate(yield, dayCounter, compounding, frequency);
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
