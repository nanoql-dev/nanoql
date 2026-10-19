// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/cashflow.hpp ql/cashflow.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL;

/// <summary>The base class of cash flows.</summary>
public abstract class CashFlow : Event
{
    /// <summary>Gets the date that the cash flow trades ex-coupon, or <see langword="null"/> when it has none.</summary>
    public virtual Date? ExCouponDate => null;

    /// <summary>The amount of the cash flow, not discounted: the amount paid at the cash-flow date.</summary>
    /// <param name="context">The evaluation context, which a cash flow whose amount depends on fixings or the
    /// evaluation date reads; one that knows its amount checks it and reads nothing.</param>
    /// <returns>The amount.</returns>
    public abstract double Amount(EvaluationContext context);

    /// <summary>
    /// Whether the cash flow has already occurred before a date. On the evaluation date, the context's
    /// <see cref="EvaluationContext.IncludeTodaysCashFlows"/>, when set, overrides <paramref name="includeRefDate"/>.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="refDate">The reference date; the evaluation date when <see langword="null"/>.</param>
    /// <param name="includeRefDate">Whether a cash flow on the reference date counts as not yet occurred.</param>
    /// <returns><see langword="true"/> when the cash flow has occurred.</returns>
    public override bool HasOccurred(EvaluationContext context, Date? refDate = null, bool? includeRefDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(refDate, nameof(refDate));

        // easy and quick handling of most cases
        if (refDate is Date reference)
        {
            Date cf = Date;
            if (reference < cf)
            {
                return false;
            }

            if (cf < reference)
            {
                return true;
            }
        }

        if (refDate is null || refDate == context.EvaluationDate)
        {
            // today's date; we override the bool with the one specified in the settings (if any)
            if (context.IncludeTodaysCashFlows is bool includeToday)
            {
                includeRefDate = includeToday;
            }
        }

        return base.HasOccurred(context, refDate, includeRefDate);
    }

    /// <summary>Whether the cash flow is trading ex-coupon on a date.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="refDate">The reference date; the evaluation date when <see langword="null"/>.</param>
    /// <returns><see langword="true"/> on or after the ex-coupon date.</returns>
    public bool TradingExCoupon(EvaluationContext context, Date? refDate = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        Date.ThrowIfDefault(refDate, nameof(refDate));
        return TradingExCouponOn(refDate ?? context.EvaluationDate);
    }

    /// <summary>Whether the cash flow is trading ex-coupon on a given date, which needs no context.</summary>
    /// <param name="refDate">The reference date.</param>
    /// <returns><see langword="true"/> on or after the ex-coupon date.</returns>
    internal bool TradingExCouponOn(Date refDate) => ExCouponDate is Date ecd && ecd <= refDate;
}
