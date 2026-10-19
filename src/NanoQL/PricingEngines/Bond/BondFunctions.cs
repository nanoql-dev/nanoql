// SPDX-FileCopyrightText: Copyright (C) 2007, 2008, 2009, 2010 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Nathan Abbott
// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: Copyright (C) 2004 M-Dimension Consulting Inc.
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007, 2008, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Jeff Yu
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/bond/bondfunctions.hpp ql/pricingengines/bond/bondfunctions.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// Bond adapters of the <see cref="CashFlows"/> functions: each passes the bond's cash flows, its settlement date and
/// <c>includeSettlementDateFlows = false</c>, and gives prices per 100 of the notional at the settlement date.
/// </summary>
/// <remarks>
/// <para>
/// A null settlement date is the bond's settlement date. Every function takes the context: the settlement date reads
/// the evaluation date, and whether a cash flow on the settlement date has occurred reads the context's
/// <see cref="EvaluationContext.IncludeTodaysCashFlows"/> when that date is the evaluation date, despite the
/// <c>false</c> passed. The bond's <see cref="Bond.Notional"/> and <see cref="Bond.AccruedAmount"/> are called
/// virtually, as QuantLib's are, so a derived bond's own accrued amount reaches these functions.
/// </para>
/// <para>
/// A price or yield function refuses a settlement date at which the bond isn't tradable, its notional being 0, with
/// QuantLib's text ("non tradable at …", with " settlement date" in the curve-based dirty price's alone); the accrued
/// amount is 0 there instead, and the cash-flow and coupon-rate inspectors don't check. The yield is solved for by
/// <see cref="NewtonSafe"/>, QuantLib's default, or by the <see cref="NewtonSafe"/> or <see cref="Brent"/> solver
/// given, the two solvers nanoql has of those QuantLib's template takes. QuantLib's three overloads taking a day
/// counter they ignore are deprecated and not ported.
/// </para>
/// </remarks>
public static class BondFunctions
{
    /// <summary>The bond's start date: its cash flows'.</summary>
    /// <param name="bond">The bond.</param>
    /// <returns>The start date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bond"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The bond has no cash flows.</exception>
    public static Date StartDate(Bond bond)
    {
        ArgumentNullException.ThrowIfNull(bond);
        return CashFlows.StartDate(bond.Cashflows);
    }

    /// <summary>The bond's cash flows' maturity date.</summary>
    /// <param name="bond">The bond.</param>
    /// <returns>The maturity date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bond"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The bond has no cash flows.</exception>
    public static Date MaturityDate(Bond bond)
    {
        ArgumentNullException.ThrowIfNull(bond);
        return CashFlows.MaturityDate(bond.Cashflows);
    }

    /// <summary>Whether the bond is tradable at the settlement date: whether its notional there isn't 0.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the notional isn't 0.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="settlementDate"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The bond has no cash flows.</exception>
    public static bool IsTradable(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Settlement(bond, context, settlementDate);
        return bond.Notional(context, settlement) != 0.0;
    }

    /// <summary>The position of the last cash flow paid at the reference date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="refDate">The reference date; the bond's settlement date when <see langword="null"/>.</param>
    /// <returns>The position in the bond's cash flows; −1 when no cash flow has been paid.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="refDate"/> is <c>default(Date)</c>.</exception>
    public static int PreviousCashFlow(Bond bond, EvaluationContext context, Date? refDate = null)
    {
        Date settlement = Settlement(bond, context, refDate, nameof(refDate));
        return CashFlows.PreviousCashFlow(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The position of the next cash flow after the reference date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="refDate">The reference date; the bond's settlement date when <see langword="null"/>.</param>
    /// <returns>The position in the bond's cash flows; their number when every cash flow has been paid.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="refDate"/> is <c>default(Date)</c>.</exception>
    public static int NextCashFlow(Bond bond, EvaluationContext context, Date? refDate = null)
    {
        Date settlement = Settlement(bond, context, refDate, nameof(refDate));
        return CashFlows.NextCashFlow(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The date of the last cash flow paid at the reference date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="refDate">The reference date; the bond's settlement date when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no cash flow has been paid.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="refDate"/> is <c>default(Date)</c>.</exception>
    public static Date? PreviousCashFlowDate(Bond bond, EvaluationContext context, Date? refDate = null)
    {
        Date settlement = Settlement(bond, context, refDate, nameof(refDate));
        return CashFlows.PreviousCashFlowDate(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The date of the next cash flow after the reference date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="refDate">The reference date; the bond's settlement date when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when every cash flow has been paid.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="refDate"/> is <c>default(Date)</c>.</exception>
    public static Date? NextCashFlowDate(Bond bond, EvaluationContext context, Date? refDate = null)
    {
        Date settlement = Settlement(bond, context, refDate, nameof(refDate));
        return CashFlows.NextCashFlowDate(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The amount paid on the date of the last cash flow paid at the reference date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="refDate">The reference date; the bond's settlement date when <see langword="null"/>.</param>
    /// <returns>The sum of the cash flows on that date; 0 when no cash flow has been paid.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="refDate"/> is <c>default(Date)</c>.</exception>
    public static double PreviousCashFlowAmount(Bond bond, EvaluationContext context, Date? refDate = null)
    {
        Date settlement = Settlement(bond, context, refDate, nameof(refDate));
        return CashFlows.PreviousCashFlowAmount(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The amount paid on the date of the next cash flow after the reference date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="refDate">The reference date; the bond's settlement date when <see langword="null"/>.</param>
    /// <returns>The sum of the cash flows on that date; 0 when every cash flow has been paid.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="refDate"/> is <c>default(Date)</c>.</exception>
    public static double NextCashFlowAmount(Bond bond, EvaluationContext context, Date? refDate = null)
    {
        Date settlement = Settlement(bond, context, refDate, nameof(refDate));
        return CashFlows.NextCashFlowAmount(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The rate of the coupons paid on the date of the last cash flow paid at the settlement date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The rate; 0 when no cash flow has been paid.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or coupons on that date differ in nominal, accrual
    /// period or day counter.
    /// </exception>
    /// <exception cref="NanoQLException">No coupon is paid on that date.</exception>
    public static double PreviousCouponRate(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Settlement(bond, context, settlementDate);
        return CashFlows.PreviousCouponRate(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The rate of the coupons paid on the date of the next cash flow after the settlement date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The rate; 0 when every cash flow has been paid.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or coupons on that date differ in nominal, accrual
    /// period or day counter.
    /// </exception>
    /// <exception cref="NanoQLException">No coupon is paid on that date.</exception>
    public static double NextCouponRate(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Settlement(bond, context, settlementDate);
        return CashFlows.NextCouponRate(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The accrual start date of the first coupon paid on the next payment date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no coupon is paid on that date.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static Date? AccrualStartDate(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.AccrualStartDate(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The accrual end date of the first coupon paid on the next payment date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no coupon is paid on that date.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static Date? AccrualEndDate(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.AccrualEndDate(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The reference period start of the first coupon paid on the next payment date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no coupon is paid on that date.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static Date? ReferencePeriodStart(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.ReferencePeriodStart(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The reference period end of the first coupon paid on the next payment date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The date; <see langword="null"/> when no coupon is paid on that date.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static Date? ReferencePeriodEnd(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.ReferencePeriodEnd(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The accrual period, as a fraction of a year, of the first coupon paid on the next payment date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The accrual period; 0 when no coupon is paid on that date.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double AccrualPeriod(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.AccrualPeriod(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The accrual days of the first coupon paid on the next payment date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The accrual days; 0 when no coupon is paid on that date.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static int AccrualDays(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.AccrualDays(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The accrued period, as a fraction of a year, of the first coupon paid on the next payment date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The accrued period; 0 when no coupon is still to be paid.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double AccruedPeriod(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.AccruedPeriod(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The accrued days of the first coupon paid on the next payment date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The accrued days; 0 when no coupon is still to be paid.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static int AccruedDays(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.AccruedDays(bond.Cashflows, context, false, settlement);
    }

    /// <summary>The accrued amount, per 100 of notional, at the settlement date.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The accrued amount; 0 when the bond isn't tradable at the settlement date.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="settlementDate"/> is <c>default(Date)</c>.</exception>
    public static double AccruedAmount(Bond bond, EvaluationContext context, Date? settlementDate = null)
    {
        Date settlement = Settlement(bond, context, settlementDate);

        if (!IsTradable(bond, context, settlement))
        {
            return 0.0;
        }

        return CashFlows.AccruedAmount(bond.Cashflows, context, false, settlement)
            * 100.0 / bond.Notional(context, settlement);
    }

    /// <summary>The clean price, per 100 of notional, on a discount curve: the dirty price less the accrued amount.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The clean price.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double CleanPrice(
        Bond bond,
        YieldTermStructure discountCurve,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        Date settlement = Settlement(bond, context, settlementDate);
        return DirtyPrice(bond, discountCurve, context, settlement) - bond.AccruedAmount(context, settlement);
    }

    /// <summary>The dirty price, per 100 of notional, on a discount curve.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The dirty price: the cash flows' NPV at the settlement date.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double DirtyPrice(
        Bond bond,
        YieldTermStructure discountCurve,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        Date settlement = Settlement(bond, context, settlementDate);

        if (!IsTradable(bond, context, settlement))
        {
            throw new ArgumentException(
                $"non tradable at {settlement} settlement date (maturity being {bond.MaturityDate()})",
                nameof(settlementDate));
        }

        double dirtyPrice = CashFlows.Npv(bond.Cashflows, discountCurve, context, false, settlement)
            * 100.0 / bond.Notional(context, settlement);
        return dirtyPrice;
    }

    /// <summary>The basis-point sensitivity, per 100 of notional, on a discount curve.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The BPS.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double Bps(
        Bond bond,
        YieldTermStructure discountCurve,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.Bps(bond.Cashflows, discountCurve, context, false, settlement)
            * 100.0 / bond.Notional(context, settlement);
    }

    /// <summary>
    /// The at-the-money rate on a discount curve: the fixed rate for which a fixed-rate bond equivalent to the bond
    /// has the given price, or its own value.
    /// </summary>
    /// <param name="bond">The bond.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <param name="price">
    /// The price, clean or dirty, per 100 of notional; <see langword="null"/>, QuantLib's invalid price, for the bond's
    /// own value on the curve.
    /// </param>
    /// <returns>The rate.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    /// <exception cref="NanoQLException">The coupons' BPS is 0.</exception>
    public static double AtmRate(
        Bond bond,
        YieldTermStructure discountCurve,
        EvaluationContext context,
        Date? settlementDate = null,
        BondPrice? price = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        Date settlement = Tradable(bond, context, settlementDate);

        double? npv = null;
        if (price is not null)
        {
            double dirtyPrice =
                price.Amount + (price.Type == BondPriceType.Clean ? bond.AccruedAmount(context, settlement) : 0);

            double currentNotional = bond.Notional(context, settlement);
            npv = dirtyPrice / 100.0 * currentNotional;
        }

        return CashFlows.AtmRate(bond.Cashflows, discountCurve, context, false, settlement, settlement, npv);
    }

    /// <summary>The clean price, per 100 of notional, for a yield: the dirty price less the accrued amount.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The clean price.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double CleanPrice(Bond bond, InterestRate yield, EvaluationContext context, Date? settlementDate = null) =>
        DirtyPrice(bond, yield, context, settlementDate) - bond.AccruedAmount(context, settlementDate);

    /// <summary>The clean price, per 100 of notional, for a yield given by its rate and conventions.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="compounding">The yield's compounding.</param>
    /// <param name="frequency">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The clean price.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// A compounded yield has no frequency; <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't
    /// tradable at the settlement date.
    /// </exception>
    public static double CleanPrice(
        Bond bond,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        var y = new InterestRate(yield, dayCounter, compounding, frequency);
        return CleanPrice(bond, y, context, settlementDate);
    }

    /// <summary>The dirty price, per 100 of notional, for a yield.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The dirty price: the cash flows' NPV at the yield, at the settlement date.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double DirtyPrice(Bond bond, InterestRate yield, EvaluationContext context, Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(yield);
        Date settlement = Tradable(bond, context, settlementDate);

        double dirtyPrice = CashFlows.Npv(bond.Cashflows, yield, context, false, settlement)
            * 100.0 / bond.Notional(context, settlement);
        return dirtyPrice;
    }

    /// <summary>The dirty price, per 100 of notional, for a yield given by its rate and conventions.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="compounding">The yield's compounding.</param>
    /// <param name="frequency">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The dirty price.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// A compounded yield has no frequency; <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't
    /// tradable at the settlement date.
    /// </exception>
    public static double DirtyPrice(
        Bond bond,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        var y = new InterestRate(yield, dayCounter, compounding, frequency);
        return DirtyPrice(bond, y, context, settlementDate);
    }

    /// <summary>The basis-point sensitivity, per 100 of notional, at a yield.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The BPS.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double Bps(Bond bond, InterestRate yield, EvaluationContext context, Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(yield);
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.Bps(bond.Cashflows, yield, context, false, settlement)
            * 100.0 / bond.Notional(context, settlement);
    }

    /// <summary>The basis-point sensitivity, per 100 of notional, at a yield given by its rate and conventions.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="compounding">The yield's compounding.</param>
    /// <param name="frequency">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The BPS.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// A compounded yield has no frequency; <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't
    /// tradable at the settlement date.
    /// </exception>
    public static double Bps(
        Bond bond,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        var y = new InterestRate(yield, dayCounter, compounding, frequency);
        return Bps(bond, y, context, settlementDate);
    }

    /// <summary>The yield that reproduces a price, solved for by <see cref="NewtonSafe"/>.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="price">The clean or dirty price, per 100 of notional.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="compounding">The yield's compounding.</param>
    /// <param name="frequency">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxIterations">The solver's maximum number of evaluations.</param>
    /// <param name="guess">The solver's initial guess; its first step is a tenth of it.</param>
    /// <returns>The yield.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxIterations"/> is negative, or the compounding or the frequency isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>; the bond isn't tradable at the settlement date; or no
    /// cash flow still to be paid has the sign opposite the price's.
    /// </exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    public static double Yield(
        Bond bond,
        BondPrice price,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null,
        double accuracy = 1.0e-10,
        int maxIterations = 100,
        double guess = 0.05)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);
        NewtonSafe solver = new NewtonSafe().SetMaxEvaluations(maxIterations);
        return Yield(solver, bond, price, dayCounter, compounding, frequency, context, settlementDate, accuracy, guess);
    }

    /// <summary>
    /// The yield that reproduces a price, solved for by the given <see cref="NewtonSafe"/> solver: QuantLib's
    /// <c>yield&lt;NewtonSafe&gt;</c>.
    /// </summary>
    /// <param name="solver">The solver, with its maximum number of evaluations.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="price">The clean or dirty price, per 100 of notional.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="compounding">The yield's compounding.</param>
    /// <param name="frequency">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="guess">The solver's initial guess; its first step is a tenth of it.</param>
    /// <returns>The yield.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>; the bond isn't tradable at the settlement date; or no
    /// cash flow still to be paid has the sign opposite the price's.
    /// </exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    public static double Yield(
        NewtonSafe solver,
        Bond bond,
        BondPrice price,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null,
        double accuracy = 1.0e-10,
        double guess = 0.05)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(solver);
        (Date settlement, double amount) = DirtyAmount(bond, price, context, settlementDate);
        return CashFlows.Yield(
            solver, bond.Cashflows, amount, dayCounter, compounding, frequency, context, false, settlement, settlement,
            accuracy, guess);
    }

    /// <summary>
    /// The yield that reproduces a price, solved for by the given <see cref="Brent"/> solver: QuantLib's
    /// <c>yield&lt;Brent&gt;</c>.
    /// </summary>
    /// <param name="solver">The solver, with its maximum number of evaluations.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="price">The clean or dirty price, per 100 of notional.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="compounding">The yield's compounding.</param>
    /// <param name="frequency">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="guess">The solver's initial guess; its first step is a tenth of it.</param>
    /// <returns>The yield.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>; the bond isn't tradable at the settlement date; or no
    /// cash flow still to be paid has the sign opposite the price's.
    /// </exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    public static double Yield(
        Brent solver,
        Bond bond,
        BondPrice price,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null,
        double accuracy = 1.0e-10,
        double guess = 0.05)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(solver);
        (Date settlement, double amount) = DirtyAmount(bond, price, context, settlementDate);
        return CashFlows.Yield(
            solver, bond.Cashflows, amount, dayCounter, compounding, frequency, context, false, settlement, settlement,
            accuracy, guess);
    }

    /// <summary>The duration at a yield.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="type">The duration: simple, modified or Macaulay's.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The duration.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>; the bond isn't tradable at the settlement date; or
    /// Macaulay's duration of a yield that isn't compounded.
    /// </exception>
    public static double Duration(
        Bond bond,
        InterestRate yield,
        EvaluationContext context,
        DurationType type = DurationType.Modified,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(yield);
        EnumArguments.Check(type, nameof(type));
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.Duration(bond.Cashflows, yield, type, context, false, settlement);
    }

    /// <summary>The duration at a yield given by its rate and conventions.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="compounding">The yield's compounding.</param>
    /// <param name="frequency">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="type">The duration: simple, modified or Macaulay's.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The duration.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The compounding, the frequency or the type isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A compounded yield has no frequency; <paramref name="settlementDate"/> is <c>default(Date)</c>; the bond isn't
    /// tradable at the settlement date; or Macaulay's duration of a yield that isn't compounded.
    /// </exception>
    public static double Duration(
        Bond bond,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        DurationType type = DurationType.Modified,
        Date? settlementDate = null)
    {
        var y = new InterestRate(yield, dayCounter, compounding, frequency);
        return Duration(bond, y, context, type, settlementDate);
    }

    /// <summary>The convexity at a yield.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The convexity.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double Convexity(Bond bond, InterestRate yield, EvaluationContext context, Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(yield);
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.Convexity(bond.Cashflows, yield, context, false, settlement);
    }

    /// <summary>The convexity at a yield given by its rate and conventions.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="compounding">The yield's compounding.</param>
    /// <param name="frequency">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The convexity.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// A compounded yield has no frequency; <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't
    /// tradable at the settlement date.
    /// </exception>
    public static double Convexity(
        Bond bond,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        var y = new InterestRate(yield, dayCounter, compounding, frequency);
        return Convexity(bond, y, context, settlementDate);
    }

    /// <summary>The basis-point value at a yield: the change in NPV for a one-basis-point change in the yield.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The basis-point value, in the cash flows' units.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double BasisPointValue(
        Bond bond,
        InterestRate yield,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(yield);
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.BasisPointValue(bond.Cashflows, yield, context, false, settlement);
    }

    /// <summary>The basis-point value at a yield given by its rate and conventions.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="compounding">The yield's compounding.</param>
    /// <param name="frequency">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The basis-point value, in the cash flows' units.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// A compounded yield has no frequency; <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't
    /// tradable at the settlement date.
    /// </exception>
    public static double BasisPointValue(
        Bond bond,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        var y = new InterestRate(yield, dayCounter, compounding, frequency);
        return BasisPointValue(bond, y, context, settlementDate);
    }

    /// <summary>
    /// The yield value of a basis point at a yield: the change in yield for a change in price of 0.01 of the cash
    /// flows' units.
    /// </summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The yield value of a basis point.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't tradable at the settlement date.
    /// </exception>
    public static double YieldValueBasisPoint(
        Bond bond,
        InterestRate yield,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(yield);
        Date settlement = Tradable(bond, context, settlementDate);
        return CashFlows.YieldValueBasisPoint(bond.Cashflows, yield, context, false, settlement);
    }

    /// <summary>The yield value of a basis point at a yield given by its rate and conventions.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="yield">The yield.</param>
    /// <param name="dayCounter">The yield's day counter.</param>
    /// <param name="compounding">The yield's compounding.</param>
    /// <param name="frequency">The yield's frequency.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The yield value of a basis point.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// A compounded yield has no frequency; <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond isn't
    /// tradable at the settlement date.
    /// </exception>
    public static double YieldValueBasisPoint(
        Bond bond,
        double yield,
        DayCounter dayCounter,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        var y = new InterestRate(yield, dayCounter, compounding, frequency);
        return YieldValueBasisPoint(bond, y, context, settlementDate);
    }

    /// <summary>
    /// The clean price, per 100 of notional, on a discount curve with a Z-spread added: the dirty price less the
    /// accrued amount.
    /// </summary>
    /// <param name="bond">The bond.</param>
    /// <param name="discount">The curve the spread is added to.</param>
    /// <param name="zSpread">The Z-spread.</param>
    /// <param name="compounding">The compounding the spread is added in.</param>
    /// <param name="frequency">The frequency the spread is added in.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The clean price.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// A compounded spread has no frequency; <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond
    /// isn't tradable at the settlement date.
    /// </exception>
    public static double CleanPrice(
        Bond bond,
        YieldTermStructure discount,
        double zSpread,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        Date settlement = Settlement(bond, context, settlementDate);
        return DirtyPrice(bond, discount, zSpread, compounding, frequency, context, settlement)
            - bond.AccruedAmount(context, settlement);
    }

    /// <summary>The dirty price, per 100 of notional, on a discount curve with a Z-spread added.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="discount">The curve the spread is added to.</param>
    /// <param name="zSpread">The Z-spread.</param>
    /// <param name="compounding">The compounding the spread is added in.</param>
    /// <param name="frequency">The frequency the spread is added in.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <returns>The dirty price.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">
    /// A compounded spread has no frequency; <paramref name="settlementDate"/> is <c>default(Date)</c>, or the bond
    /// isn't tradable at the settlement date.
    /// </exception>
    public static double DirtyPrice(
        Bond bond,
        YieldTermStructure discount,
        double zSpread,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discount);
        Date settlement = Tradable(bond, context, settlementDate);

        double dirtyPrice = CashFlows.Npv(
                bond.Cashflows, discount, zSpread, compounding, frequency, context, false, settlement)
            * 100.0 / bond.Notional(context, settlement);
        return dirtyPrice;
    }

    /// <summary>The Z-spread that reproduces a price on a discount curve, solved for by <see cref="Brent"/>.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="price">The clean or dirty price, per 100 of notional.</param>
    /// <param name="discount">The curve the spread is added to.</param>
    /// <param name="compounding">The compounding the spread is added in.</param>
    /// <param name="frequency">The frequency the spread is added in.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="settlementDate">The settlement date; the bond's when <see langword="null"/>.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxIterations">The solver's maximum number of evaluations.</param>
    /// <param name="guess">The solver's initial guess.</param>
    /// <returns>The Z-spread.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxIterations"/> is negative, or the compounding or the frequency isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="settlementDate"/> is <c>default(Date)</c>; the bond isn't tradable at the settlement date; or a
    /// compounded spread has no frequency.
    /// </exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    public static double ZSpread(
        Bond bond,
        BondPrice price,
        YieldTermStructure discount,
        Compounding compounding,
        Frequency frequency,
        EvaluationContext context,
        Date? settlementDate = null,
        double accuracy = 1.0e-10,
        int maxIterations = 100,
        double guess = 0.0)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(discount);
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);
        Date settlement = Tradable(bond, context, settlementDate);

        double dirtyPrice =
            price.Amount + (price.Type == BondPriceType.Clean ? bond.AccruedAmount(context, settlement) : 0);

        dirtyPrice /= 100.0 / bond.Notional(context, settlement);

        return CashFlows.ZSpread(
            bond.Cashflows,
            dirtyPrice,
            discount,
            compounding,
            frequency,
            context,
            false,
            settlement,
            settlement,
            accuracy,
            maxIterations,
            guess);
    }

    // The settlement date: the given one, or the bond's, after the checks every member makes first.
    private static Date Settlement(
        Bond bond,
        EvaluationContext context,
        Date? settlementDate,
        string paramName = "settlementDate")
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(settlementDate, paramName);
        return settlementDate ?? bond.SettlementDate(context);
    }

    // The settlement date, refused where the bond isn't tradable, with QuantLib's text.
    private static Date Tradable(Bond bond, EvaluationContext context, Date? settlementDate)
    {
        Date settlement = Settlement(bond, context, settlementDate);
        if (!IsTradable(bond, context, settlement))
        {
            throw new ArgumentException(
                $"non tradable at {settlement} (maturity being {bond.MaturityDate()})", nameof(settlementDate));
        }

        return settlement;
    }

    // The yield template's settlement date and dirty amount per unit of notional, from a price.
    private static (Date Settlement, double Amount) DirtyAmount(
        Bond bond,
        BondPrice price,
        EvaluationContext context,
        Date? settlementDate)
    {
        ArgumentNullException.ThrowIfNull(bond);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(price);
        Date settlement = Tradable(bond, context, settlementDate);

        double amount = price.Amount;

        if (price.Type == BondPriceType.Clean)
        {
            amount += bond.AccruedAmount(context, settlement);
        }

        amount /= 100.0 / bond.Notional(context, settlement);

        return (settlement, amount);
    }
}
