// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A float-float swap's fair spread on a capped or floored leg (the S-rx review's B1-1, fixed under R8). QuantLib
/// derives every fair spread from the leg's BPS, <c>spread[0] − NPV / (BPS / 1bp)</c>
/// (<c>floatfloatswap.cpp:545-571</c>), which is exact only while each coupon's amount moves one for one with its
/// spread: once a cap or floor binds, the swap rebuilt at QuantLib's fair spread is worth thousands. nanoql's engine
/// solves a capped or floored leg's fair spread on the swap's own pricing, and gives none where no spread zeroes the
/// NPV; an uncapped leg keeps QuantLib's BPS route and its bits. QuantLib's check: its own NPV of the swap rebuilt at
/// the solved spread, −2.9e-11 on 1,000,000 for each of the three solved cases [executed on the wheel: Brent on the
/// swap's NPV, accuracy 1e-12].
/// </summary>
public class FloatFloatSwapOptionalLegFairSpreadTests
{
    private static readonly Date Today = new(15, October, 2026);

    [Fact]
    public void ACappedOrFlooredLegsFairSpreadZeroesTheNpv()
    {
        // QuantLib's BPS route gives 0.004422610706828221 (capped at 3.2%), at which the swap is worth 9,306.97, and
        // -0.0019179501315491735 (floored at 3.1%), worth -6,231.31.
        (double? capped1, double? capped2) = FairSpreads(cap1: 0.032);
        Assert.Equal(0.014082174286210242, capped1!.Value, 1e-11);
        Assert.True(Math.Abs(Npv(capped1.Value, cap1: 0.032)) < 1e-6);
        Assert.Equal(-0.00240602806675674, capped2);

        (double? floored1, double? floored2) = FairSpreads(floor1: 0.031);
        Assert.Equal(-0.009815542908102898, floored1!.Value, 1e-11);
        Assert.True(Math.Abs(Npv(floored1.Value, floor1: 0.031)) < 1e-6);
        Assert.Equal(0.003910758750978999, floored2);
    }

    [Fact]
    public void NoFairSpreadWhereNoSpreadZeroesTheNpv()
    {
        // Capped at 2.8%, below the forward: up to the spread the lognormal caplet prices (the cap itself), the swap
        // stays worth more than 17,858; QuantLib's BPS route gives 0.0061625450250979335, worth 20,704.82.
        (double? fair1, double? fair2) = FairSpreads(cap1: 0.028);
        Assert.Null(fair1);
        Assert.Equal(-0.004139438477212603, fair2);
    }

    [Fact]
    public void ARootPastTheDomainsEndIsFound()
    {
        // Capped at 3.182%: the search doubles from QuantLib's BPS route, 0.0044777717434352545, to a spread past the
        // cap, where the lognormal caplet refuses, and finds the sign change only at the domain's end. QuantLib's
        // Brent root on its own NPV: 0.02353782059433483.
        (double? fair1, double? fair2) = FairSpreads(cap1: 0.03182);
        Assert.Equal(0.02353782059433483, fair1!.Value, 1e-11);
        Assert.True(Math.Abs(Npv(fair1.Value, cap1: 0.03182)) < 1e-6);
        Assert.Equal(-0.0024609822762933697, fair2);
    }

    [Fact]
    public void NoFairSpreadWhereTheSearchRunsOutAtAFullShift()
    {
        // Floored at 3.1% against Euribor 3M - 0.2%: QuantLib's NPV stays below -14,940 down to a spread of -100%,
        // where the floor pays in full, and the floorlet prices every lower spread; QuantLib's BPS route gives
        // -0.005933004633990126.
        (double? fair1, double? fair2) = FairSpreads(floor1: 0.031, spread2: -0.002);
        Assert.Null(fair1);
        Assert.Equal(0.003910758750978992, fair2);
    }

    private static (double? FairSpread1, double? FairSpread2) FairSpreads(
        double? cap1 = null, double? floor1 = null, double spread2 = 0.002)
    {
        EvaluationContext context = Context();
        FloatFloatSwapResults results =
            DiscountingSwapEngine.Calculate(Swap(0.0, cap1, floor1, spread2, context), context, Curve());
        return (results.FairSpread1, results.FairSpread2);
    }

    private static double Npv(double spread1, double? cap1 = null, double? floor1 = null)
    {
        EvaluationContext context = Context();
        FloatFloatSwap swap = Swap(spread1, cap1, floor1, 0.002, context);
        return DiscountingSwapEngine.Calculate(swap, context, Curve()).NPV!.Value;
    }

    private static EvaluationContext Context() => new() { EvaluationDate = Today };

    private static YieldTermStructure Curve() => new FlatForward(Today, 0.03, new Actual365Fixed());

    // A payer 5-year swap on 1,000,000 paying Euribor 6M plus spread1 against Euribor 3M plus spread2, each leg priced
    // by a Black pricer at a constant 20% optionlet volatility; the wheel's swap pays on Following, its default.
    private static FloatFloatSwap Swap(
        double spread1, double? cap1, double? floor1, double spread2, EvaluationContext context)
    {
        var calendar = new TARGET();
        YieldTermStructure curve = Curve();
        var e6 = new Euribor6M(curve);
        var e3 = new Euribor3M(curve);
        Date start = calendar.Advance(Today, 2, TimeUnit.Days);
        Date end = calendar.Advance(start, new Period(5, TimeUnit.Years));
        Schedule s6 = Schedule(start, end, 6, calendar);
        Schedule s3 = Schedule(start, end, 3, calendar);
        int n6 = s6.Size - 1, n3 = s3.Size - 1;

        var swap = new FloatFloatSwap(
            SwapType.Payer,
            Enumerable.Repeat(1e6, n6),
            Enumerable.Repeat(1e6, n3),
            s6,
            e6,
            new Actual360(),
            s3,
            e3,
            new Actual360(),
            context,
            gearing1: Enumerable.Repeat(1.0, n6),
            spread1: Enumerable.Repeat(spread1, n6),
            cappedRate1: cap1 is null ? [] : Enumerable.Repeat(cap1, n6),
            flooredRate1: floor1 is null ? [] : Enumerable.Repeat(floor1, n6),
            gearing2: Enumerable.Repeat(1.0, n3),
            spread2: Enumerable.Repeat(spread2, n3),
            paymentConvention1: BusinessDayConvention.Following,
            paymentConvention2: BusinessDayConvention.Following);

        var volatility = new ConstantOptionletVolatility(
            Today, calendar, BusinessDayConvention.Following, 0.20, new Actual365Fixed());
        var pricer = new BlackIborCouponPricer(context, volatility);
        swap = CouponPricerFunctions.SetCouponPricer(swap, 0, pricer);
        return CouponPricerFunctions.SetCouponPricer(swap, 1, pricer);
    }

    private static Schedule Schedule(Date start, Date end, int months, Calendar calendar) =>
        new MakeSchedule()
            .From(start)
            .To(end)
            .WithTenor(new Period(months, TimeUnit.Months))
            .WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .ToSchedule();
}
