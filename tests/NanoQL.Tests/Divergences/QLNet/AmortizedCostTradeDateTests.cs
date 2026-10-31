// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="AmortizingBond"/> solves its effective rate on the trade date, at initial recognition, as IFRS 9's and
/// ASC 310-20's interest method require: the price paid, the cost plus the accrued interest purchased, against the
/// remaining flows. QLNet takes the cost as the carrying amount on the coupon date on or before the trade, and solves
/// there (<c>AmortizingBond.cs:205-207</c>); its first period then runs from that coupon date while its
/// <c>AmortizationValue</c> accrues from the trade, so on AB2 its carrying amount jumps 96.34 at the first coupon date
/// (471,724.66 the day before, 471,821.00 on it), and with no coupon before the trade it solves at its global
/// evaluation date, a trade on 1 May 2010 of a bond issued 15 March 2010 yielding 0.0546 evaluated at the issue date,
/// 0.0620 evaluated on 1 January 2012, and throwing evaluated after maturity. On a purchase on a coupon date the two
/// methods agree, which the vectors check. The checks here: the rate is QuantLib's own <c>CashFlows.Yield</c> on the
/// price paid at the trade date; the carrying amount is the cost on the trade date, continuous at the coupon dates, and
/// the face amount at maturity; and each coupon date's is the remaining flows' value at the effective rate.
/// </summary>
public class AmortizedCostTradeDateTests
{
    [Fact]
    public void TheRateIsTheYieldOfThePricePaidAtTheTradeDate()
    {
        AmortizingBond ab2 = AmortizedCostBonds.Ab2();
        EvaluationContext context = AmortizedCostBonds.Context;
        var trade = new Date(31, December, 2007);
        double accrued = CashFlows.AccruedAmount(ab2.Cashflows, context, false, trade);
        double y = CashFlows.Yield(ab2.Cashflows, 471444 + accrued, new Thirty360(Thirty360Convention.USA),
            Compounding.Simple, Frequency.Semiannual, context, false, trade);

        // QLNet: 0.056749061726033695, solved at 2007-11-15 on the cost.
        Assert.Equal(3322.222222222248, accrued, 9);
        Assert.Equal(y, ab2.Yield());
    }

    [Fact]
    public void TheCarryingAmountIsContinuousFromTheCost()
    {
        AmortizingBond ab2 = AmortizedCostBonds.Ab2();

        // QLNet: 471724.6572887593 on 2008-05-14 and 471821 on 2008-05-15; 500000.02 at maturity.
        Assert.Equal(471444, ab2.AmortizationValue(new Date(31, December, 2007)));
        double before = ab2.AmortizationValue(new Date(14, May, 2008));
        double on = ab2.AmortizationValue(new Date(15, May, 2008));
        Assert.True(on - before is > 0 and < 3, $"{before:R} -> {on:R}");
        Assert.Equal(500000, ab2.AmortizationValue(new Date(15, May, 2028)));
    }

    [Fact]
    public void EachCouponDatesCarryingAmountIsTheRemainingFlowsValue()
    {
        AmortizingBond ab2 = AmortizedCostBonds.Ab2();
        EvaluationContext context = AmortizedCostBonds.Context;
        ImmutableArray<CashFlow> flows = ab2.Cashflows;
        foreach (AmortizingPayment entry in ab2.Amortizations.Take(ab2.Amortizations.Length - 1))
        {
            // The flows after the date at the effective rate, simple within each period, valued at the date; the
            // carrying amount's cents of rounding accumulate, so within a dollar.
            double pv = CashFlows.Npv(flows, ab2.Yield(), new Thirty360(Thirty360Convention.USA), Compounding.Simple,
                Frequency.Semiannual, context, false, entry.Date);
            Assert.True(Math.Abs(ab2.AmortizationValue(entry.Date) - pv) <= 1.0, $"{entry.Date}");
        }
    }

    [Fact]
    public void ATradeInTheFirstPeriodDoesntReadTheEvaluationDate()
    {
        // QLNet: 0.054625133946537965 evaluated at 2010-03-15; 0.06204030083864927 at 2012-01-01; throws at 2015-09-16.
        double[] yields = new[] { new Date(15, March, 2010), new Date(1, January, 2012), new Date(16, September, 2015) }
            .Select(e => AmortizedCostBonds.Bond(
                100000, 98000, 0.05, new Date(15, March, 2010), new Date(15, March, 2015), new Date(1, May, 2010),
                context: new EvaluationContext { EvaluationDate = e }).Yield())
            .ToArray();

        Assert.Equal(yields[0], yields[1]);
        Assert.Equal(yields[0], yields[2]);
    }
}
