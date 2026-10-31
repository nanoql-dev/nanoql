// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="MBSFixedRateBond"/>'s flows pay on each period's end adjusted by its payment convention on its calendar.
/// QLNet's MBS takes the convention and ignores it (<c>MBSFixedRateBond.cs:67-69</c>): on TARGET with Following, its
/// default, the Fabozzi pool's second payment is 1 April 2007, a Sunday, and 148 of its 358 payment dates aren't
/// business days. The check is QLNet's level-payment base bond on the same calendar and convention, whose dates the
/// vectors' TARGET case reads; the amounts don't move.
/// </summary>
public class MbsPaymentDatesTests
{
    [Fact]
    public void FlowsPayOnTheAdjustedPeriodEnds()
    {
        MBSFixedRateBond adjusted = MbsPools.Pool(
            358, 360, 0.06, 0.055, new PSACurve(MbsPools.Start), Frequency.Monthly, new TARGET(),
            BusinessDayConvention.Following);
        var dates = adjusted.ExpectedCashflows.OfType<FixedRateCoupon>().Select(c => c.Date).ToList();
        var ends = adjusted.ExpectedCashflows.OfType<FixedRateCoupon>().Select(c => c.AccrualEndDate).ToList();

        // QLNet: 03/01/2007, 04/01/2007, 05/01/2007, its unadjusted ends.
        Assert.Equal(new Date(2, April, 2007), dates[1]);
        Assert.Equal(148, dates.Zip(ends).Count(p => p.First != p.Second));
        Assert.All(adjusted.ExpectedCashflows, c => Assert.True(new TARGET().IsBusinessDay(c.Date)));
    }

    [Fact]
    public void TheAmountsAndTheYieldDontMove()
    {
        MBSFixedRateBond adjusted = MbsPools.Pool(
            358, 360, 0.06, 0.055, new PSACurve(MbsPools.Start), Frequency.Monthly, new TARGET(),
            BusinessDayConvention.Following);
        MBSFixedRateBond unadjusted = MbsPools.Fabozzi();
        EvaluationContext context = MbsPools.At(MbsPools.Start);

        Assert.Equal(
            unadjusted.ExpectedCashflows.Select(c => c.Amount(context)),
            adjusted.ExpectedCashflows.Select(c => c.Amount(context)));
        Assert.Equal(unadjusted.MonthlyYield(context), adjusted.MonthlyYield(context));
    }
}
