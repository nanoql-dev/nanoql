// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="AmortizingBond"/>'s amortisations. Each pays the leg's own coupon, day-counted, where QLNet's pays the
/// face amount times the rate over the frequency (<c>AmortizingBond.cs:58</c>), so that on Actual/365 (Fixed) QLNet's
/// carrying amount misses the face amount at maturity, 1,000,367.33 on a million; nanoql's is the remaining flows'
/// value at the effective rate at each coupon date. Each is signed, the interest less the coupon, where QLNet's sign is
/// the bond's being at a premium (<c>:118-124</c>): a premium given a rate above its coupon accretes, where QLNet's
/// amortises it, 42,875.52 on AB1 at 8% after a period where nanoql's is 43,948.48. A given rate of 0 is taken, where
/// QLNet solves instead (<c>:60</c>; −0.0238 on AB1). The last closes to the face amount, where QLNet's cents leave
/// AB2 at 500,000.02. The bond's cash flows are its coupons and redemption, its calendar the one given, where QLNet's
/// hold 41 accounting entries and coupons with overridden amounts among its 83, on TARGET (<c>:43</c>,
/// <c>:127-128</c>).
/// </summary>
public class AmortizedCostEntriesTests
{
    [Fact]
    public void ACouponDatesCarryingAmountIsTheRemainingFlowsValueOnAnyDayCounter()
    {
        var dc = new Actual365Fixed();
        AmortizingBond bond = AmortizedCostBonds.Bond(1000000, 950000, 0.05, new Date(15, March, 2010),
            new Date(15, March, 2020), new Date(15, March, 2012), dayCounter: dc);
        EvaluationContext context = AmortizedCostBonds.Context;

        // QLNet: 1000367.33 at maturity, 996598.38 on 2019-09-15.
        Assert.Equal(1000000, bond.AmortizationValue(new Date(15, March, 2020)));
        foreach (AmortizingPayment entry in bond.Amortizations.Take(bond.Amortizations.Length - 1))
        {
            double pv = CashFlows.Npv(
                bond.Cashflows, bond.Yield(), dc, Compounding.Simple, Frequency.Semiannual, context, false, entry.Date);
            Assert.True(Math.Abs(bond.AmortizationValue(entry.Date) - pv) <= 0.5, $"{entry.Date}");
        }
    }

    [Fact]
    public void AnAmortisationsSignIsItsInterestLessItsCoupon()
    {
        AmortizingBond bond = AmortizedCostBonds.Ab1(0.08);
        EvaluationContext context = AmortizedCostBonds.Context;

        // QLNet: amortisations 536.48, 515.02, subtracted from the premium: 42875.52 on 2004-07-01.
        Assert.Equal(536.48, bond.Amortizations[0].Amount(context));
        Assert.Equal(43948.48, bond.AmortizationValue(new Date(1, July, 2004)), 9);
    }

    [Fact]
    public void AGivenRateOfZeroIsTaken()
    {
        AmortizingBond bond = AmortizedCostBonds.Ab1(0.0);
        EvaluationContext context = AmortizedCostBonds.Context;

        // QLNet: solves instead, -0.02378160506632178. At 0 each amortisation is its coupon, less.
        Assert.Equal(0.0, bond.Yield());
        Assert.Equal(-1200.0, bond.Amortizations[0].Amount(context));
    }

    [Fact]
    public void TheLastAmortisationClosesToTheFaceAmount()
    {
        // QLNet: 500000.02.
        Assert.Equal(500000, AmortizedCostBonds.Ab2().AmortizationValue(new Date(15, May, 2028)));
        Assert.Equal(40000, AmortizedCostBonds.Ab1().AmortizationValue(new Date(1, January, 2005)));
    }

    [Fact]
    public void TheCashFlowsAreTheBondsLeg()
    {
        AmortizingBond ab2 = AmortizedCostBonds.Ab2();

        // QLNet: 83 cash flows, 41 AmortizingPayment, 41 FixedRateCoupon, 1 Redemption; TARGET.
        Assert.Equal(59, ab2.Cashflows.OfType<FixedRateCoupon>().Count());
        Assert.Single(ab2.Cashflows.OfType<Redemption>());
        Assert.Empty(ab2.Cashflows.OfType<AmortizingPayment>());
        Assert.Equal(41, ab2.Amortizations.Length);
        Assert.IsType<NullCalendar>(ab2.Calendar);
    }
}
