// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CDS engines' reads before a curve's reference date (the S-core plan's CR7 (xxxiv)), on curves that settle after
/// the evaluation date and on curves dated the evaluation date alike: QuantLib's integral engine integrates the default
/// leg from the evaluation date, reading the discount and the default probability before a T+2 curve's reference date,
/// both engines read the survival at a coupon paying before a T+2 hazard curve's date and the mid-point engine its
/// default date or its period's end there, and both read a live coupon's past period (one paid after a weekend that
/// ended it) before curves dated the evaluation date, where QuantLib's curves refuse a negative time.
/// nanoql's engines read nothing accrued before a curve's reference date: a discount of 1, a survival of 1 and a default
/// probability of 0, QuantLib's own premise for its two-date default probability. Each value is QuantLib's on the
/// market restated from the evaluation date, a curve dated then that accrues nothing up to the T+2 date and is the T+2
/// curve after it (the planner's <c>r1_cdshazard.cpp</c>, <c>r1_cdsshort.cpp</c> and the reviewer's <c>r8d.cpp</c>,
/// <c>r8g.cpp</c>, rerun by the planner), or, for curves dated the evaluation date, restated alike from the past
/// period's start (the reviewer's <c>r8j.cpp</c>); QuantLib's refusals, with their times, are its values on the curves
/// given.
/// </summary>
public class CdsEngineReferenceDateTests
{
    private static readonly Date Today = new(9, Month.June, 2006);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly Calendar Target = new TARGET();

    // testCachedValue's swap: a year old, ten years from its issue, semiannual on TARGET.
    private static CreditDefaultSwap CachedValueSwap(bool paysAtDefaultTime = true)
    {
        Date issueDate = Target.Advance(Today, -1, TimeUnit.Years);
        var schedule = new Schedule(
            issueDate, Target.Advance(issueDate, 10, TimeUnit.Years), new Period(Frequency.Semiannual), Target,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward,
            false, Context);
        return new CreditDefaultSwap(
            ProtectionSide.Seller, 10000.0, 0.0120, schedule, BusinessDayConvention.ModifiedFollowing, new Actual360(),
            true, paysAtDefaultTime);
    }

    // A swap whose first period ends on a date: back from it six months, five years after it, unadjusted.
    private static CreditDefaultSwap FirstPeriodEnding(Date end, bool paysAtDefaultTime = true)
    {
        var schedule = new Schedule(
            end - new Period(6, TimeUnit.Months), end + new Period(5, TimeUnit.Years), new Period(Frequency.Semiannual),
            new NullCalendar(), BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward, false, Context);
        return new CreditDefaultSwap(
            ProtectionSide.Seller, 10000.0, 0.0120, schedule, BusinessDayConvention.Unadjusted, new Actual360(), true,
            paysAtDefaultTime);
    }

    private static FlatForward DiscountToday() => new(Today, 0.06, new Actual360());

    private static FlatForward DiscountT2() => FlatForward.Create(2, Target, 0.06, new Actual360(), Context);

    private static FlatHazardRate HazardToday() => FlatHazardRate.Create(0, Target, 0.01234, new Actual360(), Context);

    private static FlatHazardRate HazardT2() => FlatHazardRate.Create(2, Target, 0.01234, new Actual360(), Context);

    private static double Integral(CreditDefaultSwap cds, DefaultProbabilityTermStructure p, YieldTermStructure d) =>
        IntegralCdsEngine.Calculate(cds, Context, new Period(1, TimeUnit.Days), p, 0.4, d).NPV!.Value;

    private static double MidPoint(CreditDefaultSwap cds, DefaultProbabilityTermStructure p, YieldTermStructure d) =>
        MidPointCdsEngine.Calculate(cds, Context, p, 0.4, d).NPV!.Value;

    [Fact]
    public void TheIntegralEngineOnAT2DiscountCurveIsQuantLibsOnTheRestatedCurve()
    {
        // QuantLib: "negative time (-0.00833333) given" on the T+2 curve; 295.2467598289 on the restated one (r8d)
        Assert.Equal(new Date(13, Month.June, 2006), DiscountT2().ReferenceDate);
        Assert.Equal(295.2467598289, Integral(CachedValueSwap(), HazardToday(), DiscountT2()), 9);
    }

    [Fact]
    public void TheMidPointEngineOnAT2DiscountCurveIsQuantLibs()
    {
        // the default dates follow the T+2 date: QuantLib's 295.2120822231 on both curves (r8d)
        Assert.Equal(295.2120822231, MidPoint(CachedValueSwap(), HazardToday(), DiscountT2()), 9);
    }

    [Fact]
    public void TheMidPointEngineOnAShortFirstPeriodIsQuantLibsOnTheRestatedCurve()
    {
        // the first period ends 2006-06-14, a day after the T+2 date, its default date before it: QuantLib "negative
        // time (-0.00555556) given"; 246.9991725019 on the restated curve (r1_cdsshort)
        Assert.Equal(
            246.9991725019, MidPoint(FirstPeriodEnding(new Date(14, Month.June, 2006)), HazardToday(), DiscountT2()), 9);
    }

    [Fact]
    public void TheIntegralEngineOnAT2HazardCurveIsQuantLibsOnTheRestatedCurve()
    {
        // QuantLib: "negative time (-0.0111111) given" on the T+2 hazard curve; on the restated one 295.9125122369 paid
        // at default and 305.7762878569 at the period's end (r1_cdshazard)
        Assert.Equal(295.9125122369, Integral(CachedValueSwap(true), HazardT2(), DiscountToday()), 9);
        Assert.Equal(305.7762878569, Integral(CachedValueSwap(false), HazardT2(), DiscountToday()), 9);

        // the mid-point engine reads nothing before the T+2 date there: QuantLib's own values on the T+2 hazard curve
        Assert.Equal(295.8618555707, MidPoint(CachedValueSwap(true), HazardT2(), DiscountToday()), 9);
        Assert.Equal(305.7762878569, MidPoint(CachedValueSwap(false), HazardT2(), DiscountToday()), 9);
    }

    [Theory]
    [InlineData(12, true, 248.1849018887, 248.2183843441)]
    [InlineData(12, false, 254.4930431865, 254.4930431865)]
    [InlineData(10, true, 248.6835464721, 248.7250525719)]
    [InlineData(10, false, 254.9860516927, 254.9860516927)]
    public void ACouponPayingBeforeAT2HazardCurveIsQuantLibsOnTheRestatedCurve(
        int day, bool paysAtDefaultTime, double midPoint, double integral)
    {
        // a first period paying before the T+2 hazard date: QuantLib refuses its survival read, "negative time
        // (-0.00277778) given" on 2006-06-12 and (-0.00833333) on 2006-06-10, in both engines; the restated values (r8g)
        CreditDefaultSwap cds = FirstPeriodEnding(new Date(day, Month.June, 2006), paysAtDefaultTime);
        Assert.Equal(midPoint, MidPoint(cds, HazardT2(), DiscountToday()), 9);
        Assert.Equal(integral, Integral(cds, HazardT2(), DiscountToday()), 9);
    }

    [Fact]
    public void ACouponPayingAfterAT2HazardCurveIsQuantLibs()
    {
        // a first period paying 2006-06-20: the mid-point engine's QuantLib value on the T+2 curve and the restated one
        // alike, 246.1629690661; the integral engine QuantLib refuses ("-0.0111111"), its restated value 246.1932834535
        CreditDefaultSwap cds = FirstPeriodEnding(new Date(20, Month.June, 2006));
        Assert.Equal(246.1629690661, MidPoint(cds, HazardT2(), DiscountToday()), 9);
        Assert.Equal(246.1932834535, Integral(cds, HazardT2(), DiscountToday()), 9);
    }

    [Theory]
    [InlineData(true, 248.3131033115, 248.3384711903)]
    [InlineData(false, 254.6290433832, 254.6290433832)]
    public void ALiveCouponWhosePeriodEndedBeforeCurvesDatedTheEvaluationDateIsQuantLibsOnTheRestatedCurves(
        bool paysAtDefaultTime, double midPoint, double integral)
    {
        // valued on Sunday 2006-06-11, its first period ending Saturday 2006-06-10 and paid Monday 2006-06-12, both
        // curves dated that Sunday: the coupon is alive, and QuantLib reads its default probability (and, paid at
        // default, its discount) at the period's dates before the curves' reference date, refused, "negative time
        // (-0.00277778) given" in the mid-point engine and "(-0.508333)" in the integral one; nanoql's values are
        // QuantLib's on the curves restated from the period's start (the reviewer's r8j.cpp)
        var sunday = new Date(11, Month.June, 2006);
        var context = new EvaluationContext { EvaluationDate = sunday };
        var schedule = new Schedule(
            new Date(10, Month.December, 2005), new Date(10, Month.June, 2011), new Period(Frequency.Semiannual), Target,
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, context);
        var cds = new CreditDefaultSwap(
            ProtectionSide.Seller, 10000.0, 0.0120, schedule, BusinessDayConvention.Following, new Actual360(), true,
            paysAtDefaultTime);
        var discount = new FlatForward(sunday, 0.06, new Actual360());
        var hazard = new FlatHazardRate(sunday, 0.01234, new Actual360());
        Assert.False(cds.IsExpired(context));

        Assert.Equal(midPoint, MidPointCdsEngine.Calculate(cds, context, hazard, 0.4, discount).NPV!.Value, 9);
        Assert.Equal(
            integral,
            IntegralCdsEngine.Calculate(cds, context, new Period(1, TimeUnit.Days), hazard, 0.4, discount).NPV!.Value,
            9);
    }

    [Fact]
    public void QuantLibsReadsBeforeTheReferenceDateAreRefused()
    {
        // the reads QuantLib makes, which nanoql's engines no longer make: each refused by the curves
        Date beforeT2 = new(10, Month.June, 2006);
        Assert.ThrowsAny<Exception>(() => DiscountT2().Discount(beforeT2));
        Assert.ThrowsAny<Exception>(() => HazardT2().SurvivalProbability(beforeT2));
        Assert.ThrowsAny<Exception>(() => HazardT2().DefaultProbability(Today, new Date(12, Month.June, 2006)));
    }
}
