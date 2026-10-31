// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Between coupon dates, <see cref="AmortizingBond.AmortizationValue"/> accrues on the carrying amount at the period's
/// start, over the coupon's own period: QLNet accrues every period on the cost, with no reference period
/// (<c>AmortizingBond.cs:163-172</c>), so its carrying amount jumps at each coupon date, by 28.28 on a quarterly
/// discount bond (96,952.32 the day before 15 June 2016, 96,980.60 on it) and by 68.96 before 15 December 2019. The
/// check is QLNet's own <c>AmortizationValue</c> at the period's start and its <c>FixedRateCoupon</c> amounts on that
/// carrying amount since then (the vectors' between-dates cases).
/// </summary>
public class AmortizedCostPartialPeriodTests
{
    [Theory]
    [InlineData(2016, 6, 15)]
    [InlineData(2019, 12, 15)]
    public void TheCarryingAmountIsContinuousAtACouponDate(int year, int month, int day)
    {
        AmortizingBond bond = AmortizedCostBonds.Bond(100000, 95000, 0.05, new Date(15, March, 2010),
            new Date(15, March, 2020), new Date(15, June, 2013), Frequency.Quarterly);
        var on = new Date(day, (Month)month, year);

        // QLNet: 96952.31810080902 -> 96980.6 and 99708.44810080901 -> 99777.41. A day's accretion here is the
        // effective rate's interest on about 97,000 less the coupon's on 100,000, about 2.
        double change = bond.AmortizationValue(on) - bond.AmortizationValue(on - 1);
        double before = bond.AmortizationValue(on - 1) - bond.AmortizationValue(on - 2);
        Assert.True(change is > 0 and < 3, $"a day's accretion {change:R}");
        Assert.True(Math.Abs(change - before) < 0.05, $"the day before's {before:R}");
    }
}
