// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The callable bond tree's snap, kept: QuantLib's discretized callable bond moves a call up to a week before a coupon
/// onto the coupon, its price discounted forward (<c>discretizedcallablefixedratebond.cpp:59-101</c>, "To avoid
/// mispricing, we snap exercise dates to the closest coupon date"). Against Jamshidian's decomposition of the issuer's
/// call on Hull-White (0.1, 0.01) on a flat 4% curve, on a 10-year 5% bond callable at a clean 100 once, the snap adds
/// about 0.0006 per day, 0.0046 per 100 at seven days (4.5e-5 relative): the closed form is 103.177955 a week before
/// the fifth coupon, where the 500-step tree prices 103.18258278794376 on the wheel; 103.191502 on the coupon (the tree
/// 103.19140397824488) and 103.176019 eight days before (103.17602442460134), beyond the week. A tree without the snap
/// is within 1e-4 of the closed form, but QuantLib's <c>testUnexercisableCallability</c>
/// (<c>callablebonds.cpp:673-735</c>) checks at 1e-10 that an unreachable call one to seven days before a coupon leaves
/// the price alone, which only the snap achieves: without it the grid gains a time and the price moves 2.4e-3. nanoql
/// keeps QuantLib's snap and its bits; QuantLib's own test stands in <c>TestSuite/CallableBondsTests.cs</c>.
/// </summary>
public class CallableSnapKeptTests
{
    [Theory]
    [InlineData(0, 103.19140397824488)]
    [InlineData(7, 103.18258278794376)]
    [InlineData(8, 103.17602442460134)]
    public void ACallInTheWeekBeforeACouponIsQuantLibsSnap(int daysBefore, double quantLib)
    {
        var today = new Date(23, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.04, new Actual365Fixed());
        var model = new HullWhite(curve, 0.1, 0.01);
        var schedule = new Schedule(
            today, today + new Period(10, TimeUnit.Years), new Period(1, TimeUnit.Years), new NullCalendar(),
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, context);
        var call = new Callability(new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call, schedule.Date(5) - daysBefore);
        var bond = new CallableFixedRateBond(
            0, 100.0, schedule, [0.05], new ActualActual(ActualActualConvention.ISMA), BusinessDayConvention.Unadjusted, 100.0,
            today, [call]);

        double npv = TreeCallableFixedRateBondEngine.Calculate(bond, context, model, 500).NPV!.Value;
        Assert.True(Math.Abs(npv - quantLib) <= 1e-10, $"{npv}");
    }
}
