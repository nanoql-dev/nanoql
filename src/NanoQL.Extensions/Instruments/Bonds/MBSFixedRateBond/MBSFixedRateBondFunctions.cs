// SPDX-FileCopyrightText: Copyright (C) 2008-2016  Andrea Maggiulli (a.maggiulli@gmail.com)
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: src/QLNet/Instruments/Bonds/MBSFixedRateBond.cs @ QLNet-v1.13.2
// Ported from QLNet and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Extensions.Instruments;

/// <summary>QLNet's free function of <c>MBSFixedRateBond.cs</c>, <c>Utils.PVDifference</c>.</summary>
public static class MBSFixedRateBondFunctions
{
    /// <summary>
    /// The present value of the cash flows still to come at a yield per payment date, less a face amount: each date's
    /// flows discounted by <c>(1 + yield / 100)^k</c>, <c>k</c> counting the distinct payment dates after the
    /// settlement from 1. It counts whole periods per payment date, with no day count and no delay, as QLNet does
    /// (<see cref="MBSFixedRateBond.MonthlyYield"/>'s yield); SIFMA's yield is <see cref="MBSFixedRateBond.Yield"/>.
    /// </summary>
    /// <param name="faceAmount">The amount subtracted, the pool's balance for a yield at par.</param>
    /// <param name="cashflows">The cash flows, sorted by date.</param>
    /// <param name="yield">The yield per period, in percent.</param>
    /// <param name="settlement">The settlement date; a flow that has occurred at it is left out.</param>
    /// <param name="context">The evaluation context, whose settings say whether a flow on the settlement date has
    /// occurred.</param>
    /// <returns>The present value less the face amount.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static double PVDifference(
        double faceAmount,
        ImmutableArray<CashFlow> cashflows,
        double yield,
        Date settlement,
        EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double price = 0.0;
        Date actualDate = new(1, Month.January, 1970);
        int cashflowindex = 0;

        foreach (CashFlow c in cashflows)
        {
            if (c.HasOccurred(context, settlement))
            {
                continue;
            }

            // QLNet: TODO use daycounter to find cashflowindex
            if (c.Date != actualDate)
            {
                actualDate = c.Date;
                cashflowindex++;
            }

            double amount = c.Amount(context);
            price += amount / Math.Pow(1 + (yield / 100), cashflowindex);
        }

        return price - faceAmount;
    }
}
