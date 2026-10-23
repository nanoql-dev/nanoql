// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// G2's analytic swaption engine's coupons (the S-sr plan's SSR6 (xiii), round 1's O1-1): QuantLib's pricing function
/// takes each fixed coupon as the rate times the difference of its payment times on the curve's day counter
/// (<c>g2.cpp:161-162</c>), whatever the fixed leg's day counter, so a leg on another prices coupons it doesn't pay: on
/// the wheel, a 1Y into 5Y payer at 3% on Euribor 6M, exercised on the swap's start as the engine assumes
/// (<c>g2swaptionengine.hpp:36-37</c>), notional 1,000,000, on G2 (0.07, 0.011, 0.3, 0.008, −0.6) on a flat 2.7%
/// Actual/365 (Fixed) curve that discounts and forecasts, prices 8,570.124924904925 on an Actual/360 fixed leg and on a
/// 30/360 one alike; the same Actual/360 coupons restated on the curve's day counter (the rate times 365/360) price
/// 7,909.079470896306, and QuantLib's finite-difference engine (200 × 150 × 150) prices the Actual/360 leg
/// 7,906.788499287265; on the 30/360 leg the finite-difference engine reads 8,593.472518199607 (100 × 100 × 100) and
/// 8,595.225571597253 (200 × 150 × 150), its tree 8,580.247796560228 (100 steps). nanoql's coupons are the corrected
/// rate times the fixed coupons' accrual periods. QuantLib's check: its own analytic engine on the restated leg; its
/// finite-difference engine on the 30/360 leg, within its grid; and a zero fixed rate, whose only coupon is the
/// notional, 122,984.01710640144 on every day counter.
/// </summary>
public class G2AnalyticSwaptionCouponTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.027, new Actual365Fixed());
    private static readonly G2 Model = new(Curve, 0.07, 0.011, 0.3, 0.008, -0.6);

    [Fact]
    public void AnActual360LegIsItsCouponsRestatedOnTheCurvesDayCounter()
    {
        double price = Price(0.03, new Actual360());
        Assert.True(Tolerance.Within(7909.079470896306, price, 1e6), $"{price}");
    }

    [Fact]
    public void AThirty360LegIsQuantLibsFiniteDifferencePrice()
    {
        double price = Price(0.03, new Thirty360(Thirty360Convention.BondBasis));
        Assert.True(Math.Abs(price - 8595.225571597253) <= 3.0, $"{price}");
    }

    [Fact]
    public void AZeroFixedRateIsQuantLibsOnEveryDayCounter()
    {
        foreach (DayCounter dc in new DayCounter[] { new Thirty360(Thirty360Convention.BondBasis), new Actual360(), new Actual365Fixed() })
        {
            double price = Price(0.0, dc);
            Assert.True(Tolerance.Within(122984.01710640144, price, 1e6), $"{dc}: {price}");
        }
    }

    private static double Price(double fixedRate, DayCounter fixedDayCounter)
    {
        var calendar = new TARGET();
        Date exercise = calendar.Advance(Today, new Period(1, TimeUnit.Years));
        Date start = exercise;
        var fixedSchedule = new Schedule(
            start, start + new Period(5, TimeUnit.Years), new Period(1, TimeUnit.Years), calendar, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        var floatSchedule = new Schedule(
            start, start + new Period(5, TimeUnit.Years), new Period(6, TimeUnit.Months), calendar, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        var swap = new VanillaSwap(
            SwapType.Payer, 1e6, fixedSchedule, fixedRate, fixedDayCounter, floatSchedule, new Euribor6M(Curve), 0.0,
            new Actual360(), Context);
        return G2SwaptionEngine.Calculate(new Swaption(swap, new EuropeanExercise(exercise)), Context, Model, 6.0, 16).NPV!.Value;
    }
}
