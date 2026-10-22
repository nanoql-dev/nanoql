// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// CDI's annual fixings in the overnight coupons. <see cref="Cdi"/> forecasts an annually compounded rate on
/// Business/252 (<c>cdi.cpp:20-33</c>), CDI's quoting convention, and its published fixings are annual rates, while
/// QuantLib's overnight pricers grow every fixing as a simple rate, <c>1 + r·τ</c>
/// (<c>overnightindexedcouponpricer.cpp:118-126</c>), and sum <c>r·τ</c> in the averaging ones. A coupon's amount then
/// depends on its route: on a flat 10% curve a coupon locked out for 2 or 5 days moves off the curve's telescopic
/// amount by +37.986528 and +94.968959 on 1,000,000, where a generic index's doesn't; a seasoned coupon's past grows by
/// 1.0083664854 where CDI's convention gives 1.0079741404; and the arithmetic pricer sums the annual rates as simple
/// ones. nanoql's compounding pricers grow a fixing by the index's <see cref="OvernightIndex.GrowthFactor"/>, and its
/// averaging pricers sum the index's <see cref="OvernightIndex.SimpleAccrual"/>: for <see cref="Cdi"/>
/// <c>(1 + r)^τ</c> and <c>(1 + r)^τ − 1</c>, CDI's daily factor as B3's DI factor states it, and by default
/// <c>1 + r·τ</c> and <c>r·τ</c>, so every other index keeps QuantLib's bits. The Black pricers' forward parts compare a
/// daily cap's or floor's strike with a simple rate estimated from the curve; a strike quoted in CDI's annual
/// convention, which caps the past fixings as quoted, is compared as its simple equivalent, where QuantLib compares it
/// as quoted, about 47bp too high at 10%. QuantLib's checks: its own telescopic amount;
/// <c>InterestRate(r, Business252, Compounded, Annual)</c>'s compound factor, over the past and, for the Black pricers'
/// past parts, on the capped fixing; a generic index fed the simple-equivalent fixings <c>((1 + r)^τ − 1)/τ</c>, in
/// the arithmetic pricer, and at the simple-equivalent strike, in the Black pricers' forward parts [executed on the
/// wheel]. QuantLib's amounts are recorded beside nanoql's.
/// </summary>
public class CdiCompoundingTests
{
    private static readonly Date Today = new(15, June, 2026);
    private static readonly Calendar Brazil = new Brazil(BrazilMarket.Settlement);
    private static readonly DayCounter B252 = new Business252(Brazil);
    private static readonly YieldTermStructure Curve =
        new FlatForward(Today, 0.10, B252, Compounding.Compounded, Frequency.Annual);

    // 21 business days before today, 14 May 2026, the start of the seasoned coupons
    private static readonly Date Seasoned = Brazil.Advance(Today, -21, TimeUnit.Days);

    // The Black pricers' volatility, which a coupon fixed in the past never reads, and the cap's annual rate
    private static readonly OptionletVolatilityStructure Vol =
        new ConstantOptionletVolatility(Today, Brazil, BusinessDayConvention.Following, 0.2, B252);

    private static readonly InterestRate Capped = new(0.08, B252, Compounding.Compounded, Frequency.Annual);

    [Fact]
    public void ALockedOutCouponIsTheCurvesTelescopicAmount()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Date start = Brazil.Advance(Today, 5, TimeUnit.Days);
        Date end = Brazil.Advance(start, new Period(3, TimeUnit.Months));
        double telescopic = 1e6 * ((Curve.Discount(start) / Curve.Discount(end)) - 1.0);
        Assert.Equal(24888.65246651173, telescopic, 1e-8);

        // QuantLib: 24888.65246651173, 24926.638994162695 (+37.986528) and 24983.621425534562 (+94.968959)
        var cdi = new Cdi(Curve);
        foreach (int lockout in new[] { 0, 2, 5 })
        {
            Assert.Equal(telescopic, Compounded(cdi, start, end, lockout).Amount(context), telescopic * 1e-8);
        }
    }

    [Fact]
    public void ASeasonedCouponGrowsItsPastAtTheAnnualRate()
    {
        Date end = Brazil.Advance(Seasoned, new Period(3, TimeUnit.Months));
        var cdi = new Cdi(Curve);
        EvaluationContext context = WithFixings((cdi, 0.10));
        double past = new InterestRate(0.10, B252, Compounding.Compounded, Frequency.Annual)
            .CompoundFactor(Seasoned, Today);
        double expected = 1e6 * ((past * Curve.Discount(Today) / Curve.Discount(end)) - 1.0);

        // QuantLib: 25287.581234970436, its past grown by (1 + 0.10/252)^21, 1.0083664853743877
        Assert.Equal(1.0079741404289038, past, 1e-15);
        Assert.Equal(24888.65246651195, expected, 1e-8);
        Assert.Equal(expected, Compounded(cdi, Seasoned, end, 0).Amount(context), expected * 1e-10);
    }

    [Fact]
    public void TheArithmeticPricerSumsTheSimpleEquivalentFixings()
    {
        Date end = Brazil.Advance(Seasoned, new Period(3, TimeUnit.Months));
        var cdi = new Cdi(Curve);
        var generic = new OvernightIndex("CTRL", 0, new BRLCurrency(), Brazil, B252, Curve);

        // each business day spans 1/252
        double simple = (Math.Pow(1.10, 1.0 / 252) - 1.0) * 252;
        EvaluationContext context = WithFixings((cdi, 0.10), (generic, simple));
        double onGeneric = Averaged(generic, Seasoned, end).Amount(context);

        // QuantLib: 25793.65079364985 on Cdi, its annual fixings summed as simple rates, and 24588.624549726435 on the
        // generic index fed the simple-equivalent fixings
        Assert.Equal(24588.624549726435, onGeneric, 1e-8);
        Assert.Equal(onGeneric, Averaged(cdi, Seasoned, end).Amount(context), onGeneric * 1e-12);
    }

    [Fact]
    public void TheBlackCompoundingPricersPastPartGrowsTheCappedFixingsAtTheAnnualRate()
    {
        var cdi = new Cdi(Curve);
        EvaluationContext context = WithFixings((cdi, 0.10));
        var capped = CappedFlooredDaily(
            cdi, new BlackCompoundingOvernightIndexedCouponPricer(Vol), RateAveragingType.Compound, Seasoned, Today, 0.08, null);
        double expected = (Capped.CompoundFactor(Seasoned, Today) - 1.0) / B252.YearFraction(Seasoned, Today);

        // QuantLib: 0.08025447960820653, each capped fixing grown as a simple rate, ((1 + 0.08/252)^21 - 1)/t
        Assert.Equal(0.07720836132004116, expected, 1e-15);
        Assert.Equal(expected, capped.Rate(context), 1e-14);
    }

    [Fact]
    public void TheBlackAveragingPricersPastPartSumsTheCappedFixingsDailyGrowth()
    {
        var cdi = new Cdi(Curve);
        EvaluationContext context = WithFixings((cdi, 0.10));
        var capped = CappedFlooredDaily(
            cdi, new BlackAveragingOvernightIndexedCouponPricer(Vol), RateAveragingType.Simple, Seasoned, Today, 0.08, null);
        double sum = 0.0;
        for (Date d = Seasoned, next; d < Today; d = next)
        {
            next = Brazil.Advance(d, 1, TimeUnit.Days);
            sum += Capped.CompoundFactor(d, next) - 1.0;
        }

        double expected = sum / B252.YearFraction(Seasoned, Today);

        // QuantLib: 0.07999999999999996, the capped fixings summed as simple rates
        Assert.Equal(0.07697279432037174, expected, 1e-15);
        Assert.Equal(expected, capped.Rate(context), 1e-14);
    }

    [Fact]
    public void TheBlackCompoundingPricersForwardPartReadsTheStrikesSimpleEquivalent()
    {
        // QuantLib: 0.09523752198414674 capped and 0.10255527179898706 floored, the annual strike compared with a
        // simple rate estimated from the curve
        AssertForwardCapAndFloor(
            new BlackCompoundingOvernightIndexedCouponPricer(Vol),
            RateAveragingType.Compound,
            0.09349643764310976,
            0.0995070627506871);
    }

    [Fact]
    public void TheBlackAveragingPricersForwardPartReadsTheStrikesSimpleEquivalent()
    {
        // QuantLib: 0.09877605818888731 capped and 0.10591376200676852 floored, the strike as above, and the annual
        // forecasts summed as simple rates
        AssertForwardCapAndFloor(
            new BlackAveragingOvernightIndexedCouponPricer(Vol),
            RateAveragingType.Simple,
            0.09240410203811056,
            0.09827033599745143);
    }

    // A coupon from five business days out for three months, capped or floored daily at 10%: on Cdi, its rates equal the
    // generic index's on the same curve, calendar and day counter at the simple-equivalent strike, which equal
    // QuantLib's, given.
    private static void AssertForwardCapAndFloor(
        FloatingRateCouponPricer pricer, RateAveragingType averaging, double quantLibCapped, double quantLibFloored)
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Date start = Brazil.Advance(Today, 5, TimeUnit.Days);
        Date end = Brazil.Advance(start, new Period(3, TimeUnit.Months));
        var cdi = new Cdi(Curve);
        var generic = new OvernightIndex("CTRL", 0, new BRLCurrency(), Brazil, B252, Curve);

        // each business day spans 1/252
        double simple = (Math.Pow(1.10, 1.0 / 252) - 1.0) * 252;
        double capped = CappedFlooredDaily(generic, pricer, averaging, start, end, simple, null).Rate(context);
        double floored = CappedFlooredDaily(generic, pricer, averaging, start, end, null, simple).Rate(context);
        Assert.Equal(quantLibCapped, capped, 1e-15);
        Assert.Equal(quantLibFloored, floored, 1e-15);
        Assert.Equal(capped, CappedFlooredDaily(cdi, pricer, averaging, start, end, 0.10, null).Rate(context), 1e-14);
        Assert.Equal(floored, CappedFlooredDaily(cdi, pricer, averaging, start, end, null, 0.10).Rate(context), 1e-14);
    }

    [Fact]
    public void TheBlackCompoundingPricersForwardPartReadsTheSmileAtTheQuotedStrike() =>
        AssertForwardOnASmile(v => new BlackCompoundingOvernightIndexedCouponPricer(v), RateAveragingType.Compound);

    [Fact]
    public void TheBlackAveragingPricersForwardPartReadsTheSmileAtTheQuotedStrike() =>
        AssertForwardOnASmile(v => new BlackAveragingOvernightIndexedCouponPricer(v), RateAveragingType.Simple);

    // On a smile quoted at CDI's annual strikes, a coupon from five business days out for three months, capped or
    // floored daily at 9%, 10% and 11%: on Cdi, its rates equal the generic index's at the simple-equivalent strikes on
    // the same quotes relabelled to the generic index's strikes.
    private static void AssertForwardOnASmile(
        Func<OptionletVolatilityStructure, FloatingRateCouponPricer> pricerOn, RateAveragingType averaging)
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Date start = Brazil.Advance(Today, 5, TimeUnit.Days);
        Date end = Brazil.Advance(start, new Period(3, TimeUnit.Months));
        var cdi = new Cdi(Curve);
        var generic = new OvernightIndex("CTRL", 0, new BRLCurrency(), Brazil, B252, Curve);
        FloatingRateCouponPricer onAnnual = pricerOn(new LinearSmile(k => k));
        FloatingRateCouponPricer onSimple = pricerOn(new LinearSmile(k => Math.Pow(1.0 + (k / 252), 252) - 1.0));
        foreach (double annual in new[] { 0.09, 0.10, 0.11 })
        {
            double simple = (Math.Pow(1.0 + annual, 1.0 / 252) - 1.0) * 252;
            Assert.Equal(
                CappedFlooredDaily(generic, onSimple, averaging, start, end, simple, null).Rate(context),
                CappedFlooredDaily(cdi, onAnnual, averaging, start, end, annual, null).Rate(context),
                1e-13);
            Assert.Equal(
                CappedFlooredDaily(generic, onSimple, averaging, start, end, null, simple).Rate(context),
                CappedFlooredDaily(cdi, onAnnual, averaging, start, end, null, annual).Rate(context),
                1e-13);
        }
    }

    // A coupon on 1,000,000 from start to end on Business/252, capped or floored on each daily fixing, priced by the
    // Black pricer.
    private static CappedFlooredOvernightIndexedCoupon CappedFlooredDaily(
        OvernightIndex index,
        FloatingRateCouponPricer pricer,
        RateAveragingType averaging,
        Date start,
        Date end,
        double? cap,
        double? floor)
    {
        OvernightIndexedCoupon underlying = new OvernightIndexedCoupon(
            end, 1e6, start, end, index, dayCounter: B252, averagingMethod: averaging, lookbackDays: 0)
            .SetPricer(pricer);
        return new CappedFlooredOvernightIndexedCoupon(underlying, cap, floor, dailyCapFloor: true).SetPricer(pricer);
    }

    // A coupon on 1,000,000 from start to end on Business/252, priced by the compounding pricer.
    private static OvernightIndexedCoupon Compounded(OvernightIndex index, Date start, Date end, int lockout) =>
        new OvernightIndexedCoupon(end, 1e6, start, end, index, dayCounter: B252, lookbackDays: 0, lockoutDays: lockout)
            .SetPricer(new CompoundingOvernightIndexedCouponPricer());

    // An averaged coupon on 1,000,000 from start to end on Business/252, priced by the arithmetic pricer.
    private static OvernightIndexedCoupon Averaged(OvernightIndex index, Date start, Date end) =>
        new OvernightIndexedCoupon(
            end, 1e6, start, end, index, dayCounter: B252, averagingMethod: RateAveragingType.Simple, lookbackDays: 0)
            .SetPricer(new ArithmeticAveragedOvernightIndexedCouponPricer());

    // Each index's fixing on every business day from the seasoned coupons' start to the day before today.
    private static EvaluationContext WithFixings(params (OvernightIndex Index, double Fixing)[] indexes)
    {
        IndexFixings fixings = IndexFixings.Empty;
        for (Date d = Seasoned; d < Today; d = Brazil.Advance(d, 1, TimeUnit.Days))
        {
            foreach ((OvernightIndex index, double fixing) in indexes)
            {
                fixings = fixings.AddFixing(index, d, fixing);
            }
        }

        return new EvaluationContext { EvaluationDate = Today, Fixings = fixings };
    }

    // A caplet volatility linear in the annual strike, 0.20 + 2 (K - 0.10), on an axis the map takes to that strike.
    private sealed class LinearSmile(Func<double, double> annual)
        : OptionletVolatilityStructure(Today, Brazil, BusinessDayConvention.Following, B252)
    {
        public override Date MaxDate => Date.MaxDate;

        public override double MinStrike() => -double.MaxValue;

        public override double MaxStrike() => double.MaxValue;

        protected override SmileSection SmileSectionImpl(double optionTime) => throw new NotSupportedException();

        protected override double VolatilityImpl(double optionTime, double strike) =>
            0.20 + (2.0 * (annual(strike) - 0.10));
    }
}
