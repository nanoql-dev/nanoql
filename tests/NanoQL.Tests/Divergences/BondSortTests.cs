// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The order of a bond's equal-dated coupons. QuantLib's constructors sort the cash flows they are given with
/// <c>std::sort</c> (<c>bond.cpp:43-44, 74-75</c>), which isn't stable: MSVC's, which built the wheel, runs an
/// insertion sort up to 32 elements, which keeps equal-dated coupons in their order, and an introsort beyond, which
/// reorders them. nanoql sorts stably, so it gives QuantLib's order up to 32 cash flows and on any leg without equal
/// dates, and the coupons' own order beyond. The order is observable wherever a function reads the first coupon on a
/// date, or compares consecutive coupons' nominals.
/// </summary>
public class BondSortTests
{
    [Fact]
    public void EqualDatedCouponsKeepTheirOrderBeyondThirtyTwo()
    {
        // 33 coupons, three on each of 11 dates, given latest date first: rates 1%, 2% and 3% on each date, plus 1bp
        // per quarter.
        var dc = new Actual365Fixed();
        var first = new Date(16, Month.June, 2025);
        ImmutableArray<CashFlow>.Builder leg = ImmutableArray.CreateBuilder<CashFlow>();
        for (int d = 10; d >= 0; --d)
        {
            Date start = first + new Period(d * 3, TimeUnit.Months);
            Date end = start + new Period(3, TimeUnit.Months);
            for (int k = 0; k < 3; ++k)
            {
                leg.Add(new FixedRateCoupon(end, 100.0, (0.01 * (k + 1)) + (0.0001 * d), dc, start, end));
            }
        }

        var context = new EvaluationContext { EvaluationDate = first };
        double[] Rates(Bond bond) => [.. bond.Cashflows.OfType<FixedRateCoupon>().Select(c => c.Rate(context))];
        double[] rates = Rates(new Bond(0, new NullCalendar(), null, leg.ToImmutable()));

        // nanoql: each date's coupons in the order given. QuantLib's on the first date: 0.03, 0.02, 0.01, then 0.0301,
        // 0.0201 and 0.0101 on the second (the wheel), with 10 of the 11 dates reordered.
        Assert.Equal([0.01, 0.02, 0.03, 0.0101, 0.0201, 0.0301], rates[..6]);
        for (int d = 0; d < 11; ++d)
        {
            Assert.True(rates[3 * d] < rates[(3 * d) + 1] && rates[(3 * d) + 1] < rates[(3 * d) + 2], $"date {d}");
        }

        // Up to 32 cash flows, both keep each date's order: the first 30 coupons alone, on 10 dates (the wheel).
        double[] shorter = Rates(new Bond(0, new NullCalendar(), null, leg.ToImmutable()[..30]));
        Assert.Equal(30, shorter.Length);
        for (int d = 0; d < 10; ++d)
        {
            Assert.True(shorter[3 * d] < shorter[(3 * d) + 1] && shorter[(3 * d) + 1] < shorter[(3 * d) + 2], $"date {d}");
        }
    }
}
