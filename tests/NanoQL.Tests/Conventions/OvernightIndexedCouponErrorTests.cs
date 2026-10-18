// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The overnight coupons', leg's and pricers' refusals, by type, message and parameter (the playbook's §2 and §9.4):
/// a check on an argument throws <see cref="ArgumentException"/> (<see cref="ArgumentOutOfRangeException"/> for a value
/// outside a range or an enum, <see cref="ArgumentNullException"/> for a null), and a check on a stored value
/// <see cref="NanoQLException"/>, with QuantLib's text. A vector's <c>{"throws": true}</c> accepts either.
/// </summary>
public class OvernightIndexedCouponErrorTests
{
    private static readonly Date Today = new(15, January, 2025);
    private static readonly Date Start = new(17, February, 2025);
    private static readonly Date End = new(17, March, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheCouponsRefusals()
    {
        var sofr = new Sofr(new FlatForward(Today, 0.04, new Actual360()));

        AssertArgument<ArgumentOutOfRangeException>(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, lookbackDays: -2), "lookbackDays", null);
        AssertArgument<ArgumentOutOfRangeException>(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, lockoutDays: -2), "lockoutDays", null);
        AssertArgument<ArgumentException>(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, lockoutDays: 100),
            "lockoutDays",
            "Lockout period cannot be negative or exceed the number of fixing days.");
        AssertArgument<ArgumentException>(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, telescopicValueDates: true, lookbackDays: 2),
            "telescopicValueDates",
            "Telescopic formula cannot be applied for a coupon with lookback.");
        AssertArgument<ArgumentException>(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, averagingMethod: RateAveragingType.Simple, lockoutDays: 2),
            "averagingMethod",
            "Cannot price an overnight coupon with simple averaging with lookback or lockout.");
        AssertArgument<ArgumentOutOfRangeException>(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, averagingMethod: (RateAveragingType)5),
            "averagingMethod",
            "unknown compounding convention (5)");
        AssertArgument<ArgumentOutOfRangeException>(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, roundingPrecision: 17),
            "roundingPrecision",
            "rounding precision (17) must be between 0 and 16");
        AssertArgument<ArgumentException>(
            () => new OvernightIndexedCoupon(End, 1e6, End, Start, sofr), "startDate", "startDate must be less than endDate");
        AssertArgument<ArgumentException>(
            () => new OvernightIndexedCoupon(End - 1, 1e6, Start, End, sofr),
            "paymentDate",
            "Payment date cannot be earlier than accrual end date");
        AssertArgument<ArgumentNullException>(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, null!), "overnightIndex", null);
        AssertStored(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, rateComputationStartDate: End, rateComputationEndDate: Start),
            "degenerate schedule");

        // The members reading the pricer.
        var averaged = new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, averagingMethod: RateAveragingType.Simple);
        var spreadDaily = new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, spread: 0.001, compoundSpreadDaily: true);
        AssertStored(() => averaged.EffectiveIndexFixing(Context), "Average OIS Coupon does not have an effectiveIndexFixing");
        AssertStored(
            () => spreadDaily.SetPricer(new ArithmeticAveragedOvernightIndexedCouponPricer()).EffectiveSpread(Context),
            "OvernightIndexedCoupon::effectiveSpread(): expected OvernightIndexedCouponPricer");
        AssertStored(
            () => spreadDaily.SetPricer(null).EffectiveIndexFixing(Context),
            "OvernightIndexedCoupon::effectiveSpread(): expected OvernightIndexedCouponPricer");
        AssertStored(() => spreadDaily.SetPricer(null).AccruedAmount(Start + 3, Context), "pricer not set");
        AssertStored(() => spreadDaily.SetPricer(null).Rate(Context), "pricer not set");

        // What the pricer reads: a past fixing, the curve and its range.
        AssertStored(
            () => new OvernightIndexedCoupon(End, 1e6, Today - 10, Today + 10, sofr).Rate(Context),
            "Missing SOFRON Actual/360 fixing for ");
        AssertStored(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, new Sofr()).Rate(Context),
            "null term structure set to this instance of SOFRON Actual/360");
        var shortCurve = new Sofr(new ShortCurve(Today, Start + 10));
        AssertStored(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, shortCurve).Rate(Context),
            "coupon requires a range [February 14th, 2025, March 17th, 2025] wider than is supported by the term structure "
            + "for instance ofSOFRON Actual/360 [January 15th, 2025, February 27th, 2025]");
        AssertStored(
            () => new OvernightIndexedCoupon(End, 1e6, Start, End, new Sofr(), averagingMethod: RateAveragingType.Simple)
                .Rate(Context),
            "null term structure set to this instance of SOFRON Actual/360");
    }

    [Fact]
    public void TheCappedCouponsRefusals()
    {
        var sofr = new Sofr(new FlatForward(Today, 0.04, new Actual360()));
        var underlying = new OvernightIndexedCoupon(End, 1e6, Start, End, sofr);
        var capped = new CappedFlooredOvernightIndexedCoupon(underlying, 0.05, 0.01);

        AssertArgument<ArgumentNullException>(() => new CappedFlooredOvernightIndexedCoupon(null!), "underlying", null);
        AssertArgument<ArgumentException>(
            () => new CappedFlooredOvernightIndexedCoupon(underlying, 0.01, 0.05), "cap", "cap level (0.01) less than floor level (0.05)");
        AssertArgument<ArgumentException>(
            () => new CappedFlooredOvernightIndexedCoupon(
                new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, 1.5, compoundSpreadDaily: true), 0.05),
            "underlying",
            "CappedFlooredOvernightIndexedCoupon: if include spread = true, only a gearing 1.0 is allowed - scale the "
            + "notional in this case instead.");
        AssertArgument<ArgumentException>(
            () => capped.SetPricer(new TestPricer()),
            "pricer",
            "The pricer is required to be an instance of OvernightIndexedCouponPricer");
        AssertArgument<ArgumentNullException>(
            () => capped.SetPricer(null), "pricer", "The pricer is required to be an instance of OvernightIndexedCouponPricer");
        AssertStored(() => capped.Rate(Context), "coupon pricer not an instance of OvernightIndexedCouponPricer");
        AssertStored(
            () => new CappedFlooredOvernightIndexedCoupon(underlying.SetPricer(null), 0.05).Rate(Context),
            "underlying coupon pricer not set");
        AssertStored(
            () => capped.SetPricer(new CompoundingOvernightIndexedCouponPricer()).EffectiveCapletVolatility(Context),
            "CompoundingOvernightIndexedCouponPricer::floorletRate(Rate, bool) not implemented");
    }

    [Fact]
    public void TheOvernightLegsRefusals()
    {
        var sofr = new Sofr(new FlatForward(Today, 0.04, new Actual360()));
        var schedule = new Schedule([Start, End, End + 28], new TARGET());
        OvernightLeg leg = new OvernightLeg(schedule, sofr).WithNotionals(1e6);

        AssertArgument<ArgumentNullException>(() => new OvernightLeg(schedule, null!), "overnightIndex", "no index provided");
        AssertArgument<ArgumentNullException>(() => new OvernightLeg(null!, sofr), "schedule", null);

        // QuantLib's empty day counter and last-recent-period calendar, null: the index's day counter and the leg's
        // calendar (overnightindexedcoupon.cpp:649; the coupon's empty day counter is the index's).
        OvernightLeg recent = leg.WithLastRecentPeriod(new Period(10, TimeUnit.Days));
        Assert.Equal(
            Describe(recent.WithPaymentDayCounter(new Actual360()).WithLastRecentPeriodCalendar(new TARGET()).ToLeg()),
            Describe(recent.WithPaymentDayCounter(null).WithLastRecentPeriodCalendar(null).ToLeg()));

        // QuantLib's empty payment calendar, null: the schedule's (overnightindexedcoupon.cpp:587-588). An accrual
        // ending on Tuesday 29 April 2025, paid 2 business days later, passes TARGET's 1 May holiday.
        OvernightLeg april = new OvernightLeg(new Schedule([new Date(31, March, 2025), new Date(29, April, 2025)], new TARGET()), sofr)
            .WithNotionals(1e6).WithPaymentLag(2);
        Assert.Equal(new Date(2, May, 2025), april.WithPaymentCalendar(new TARGET()).ToLeg()[0].Date);
        Assert.Equal(new Date(2, May, 2025), april.WithPaymentCalendar(null).ToLeg()[0].Date);
        AssertArgument<ArgumentOutOfRangeException>(() => leg.WithLookbackDays(-1), "lookbackDays", null);
        AssertArgument<ArgumentOutOfRangeException>(() => leg.WithLockoutDays(-1), "lockoutDays", null);
        AssertArgument<ArgumentOutOfRangeException>(
            () => leg.WithRoundingPrecision(-1), "roundingPrecision", "rounding precision (-1) must be between 0 and 16");
        AssertArgument<ArgumentOutOfRangeException>(
            () => leg.WithAveragingMethod((RateAveragingType)(-1)), "averagingMethod", "unknown compounding convention (-1)");
        AssertArgument<ArgumentOutOfRangeException>(
            () => leg.WithPaymentAdjustment((BusinessDayConvention)99), "convention", null);
        AssertStored(() => new OvernightLeg(schedule, sofr).ToLeg(), "no notional given");
        AssertStored(
            () => leg.WithCouponPricer(new ArithmeticAveragedOvernightIndexedCouponPricer()).ToLeg(),
            "Wrong coupon pricer provided, provide a CompoundingOvernightIndexedCouponPricer");
        AssertStored(
            () => leg.WithAveragingMethod(RateAveragingType.Simple)
                .WithCouponPricer(new CompoundingOvernightIndexedCouponPricer())
                .ToLeg(),
            "Wrong coupon pricer provided, provide a ArithmeticAveragedOvernightIndexedCouponPricer");
        AssertStored(() => new OvernightLeg(new Schedule([], new TARGET()), sofr).WithNotionals(1e6).ToLeg(), "empty schedule");
        AssertStored(
            () => leg.WithPaymentDates([End]).ToLeg(),
            "Expected the number of explicit payment dates (1) to equal the number of calculation periods (2)");
        AssertStored(() => leg.WithGearings(0.0).ToLeg(), "no day counter implementation provided");
        AssertStored(
            () => new OvernightLeg(new Schedule([Start, End], new TARGET(), isRegular: [false]), sofr).WithNotionals(1e6).ToLeg(),
            "full interface (tenor) not available");
    }

    [Fact]
    public void AZeroGearingPeriodWithoutADayCounterIsRefusedAfterTheLegIsBuilt()
    {
        // QuantLib builds a zero-gearing period's fixed coupon on the empty day counter and fails only when it is
        // priced (overnightindexedcoupon.cpp:653-683), so every refusal it raises while building the leg comes first
        // (the wheel's refusals): a later period's payment date before its accrual end, and its cap below its floor.
        var estr = new Estr(new FlatForward(Today, 0.03, new Actual365Fixed()));
        Schedule schedule = new MakeSchedule().From(new Date(15, January, 2026)).To(new Date(15, January, 2027))
            .WithTenor(new Period(6, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();
        OvernightLeg leg = new OvernightLeg(schedule, estr).WithNotionals(100.0).WithGearings([0.0, 1.0]);

        AssertArgument<ArgumentException>(
            () => leg.WithPaymentLag(-3).ToLeg(), "paymentDate", "Payment date cannot be earlier than accrual end date");
        AssertArgument<ArgumentException>(
            () => leg.WithCaps(0.01).WithFloors(0.02).ToLeg(), "cap", "cap level (0.01) less than floor level (0.02)");

        // Its own refusal stays, once the leg is built (ZeroGearingWithoutDayCounterTests).
        AssertStored(() => leg.ToLeg(), "no day counter implementation provided");
    }

    [Fact]
    public void ThePricersRefusals()
    {
        var sofr = new Sofr(new FlatForward(Today, 0.04, new Actual360()));
        var compounded = new OvernightIndexedCoupon(End, 1e6, Start, End, sofr);
        var averaged = new OvernightIndexedCoupon(End, 1e6, Start, End, sofr, averagingMethod: RateAveragingType.Simple);
        var vol = new ConstantOptionletVolatility(Today, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed());
        var ibor = new FloatingRateCoupon(End, 1e6, Start, End, 2, new Euribor6M(new FlatForward(Today, 0.03, new Actual360())));

        // B3, with QuantLib's text; and a coupon no overnight pricer prices.
        AssertArgument<ArgumentException>(
            () => new BlackAveragingOvernightIndexedCouponPricer(vol).SwapletRate(compounded, Context),
            "coupon",
            "Averaging method required to be simple for BlackAveragingOvernightIndexedCouponPricer");
        AssertArgument<ArgumentException>(
            () => new CompoundingOvernightIndexedCouponPricer().SwapletRate(ibor, Context),
            "coupon",
            "OvernightIndexedCouponPricer: unsupported coupon type");

        // The members QuantLib doesn't provide.
        var compounding = new CompoundingOvernightIndexedCouponPricer();
        var arithmetic = new ArithmeticAveragedOvernightIndexedCouponPricer();
        foreach ((OvernightIndexedCouponPricer p, OvernightIndexedCoupon c) in new (OvernightIndexedCouponPricer, OvernightIndexedCoupon)[]
        {
            (compounding, compounded), (arithmetic, averaged),
        })
        {
            AssertStored(() => p.SwapletPrice(c, Context), "swapletPrice not available");
            AssertStored(() => p.CapletPrice(c, 0.03, Context), "capletPrice not available");
            AssertStored(() => p.CapletRate(c, 0.03, Context), "capletRate not available");
            AssertStored(() => p.FloorletPrice(c, 0.03, Context), "floorletPrice not available");
            AssertStored(() => p.FloorletRate(c, 0.03, Context), "floorletRate not available");
        }

        AssertStored(
            () => compounding.CapletRate(compounded, 0.03, true, Context),
            "CompoundingOvernightIndexedCouponPricer::capletRate(Rate, bool) not implemented");
        AssertStored(
            () => compounding.FloorletRate(compounded, 0.03, true, Context),
            "CompoundingOvernightIndexedCouponPricer::floorletRate(Rate, bool) not implemented");
        AssertStored(
            () => arithmetic.CapletRate(averaged, 0.03, false, Context),
            "ArithmeticAveragedOvernightIndexedCouponPricer::capletRate(Rate, bool) not implemented");
        AssertStored(
            () => arithmetic.FloorletRate(averaged, 0.03, false, Context),
            "ArithmeticAveragedOvernightIndexedCouponPricer::floorletRate(Rate, bool) not implemented");
        AssertStored(
            () => arithmetic.EffectiveCapletVolatility(averaged, 0.03, false, Context),
            "ArithmeticAveragedOvernightIndexedCouponPricer::capletRate(Rate, bool) not implemented");

        var blackCompounding = new BlackCompoundingOvernightIndexedCouponPricer(vol);
        var blackAveraging = new BlackAveragingOvernightIndexedCouponPricer(vol);
        foreach ((OvernightIndexedCouponPricer p, OvernightIndexedCoupon c, string name) in new (OvernightIndexedCouponPricer, OvernightIndexedCoupon, string)[]
        {
            (blackCompounding, compounded, "BlackCompoundingOvernightIndexedCouponPricer"),
            (blackAveraging, averaged, "BlackAveragingOvernightIndexedCouponPricer"),
        })
        {
            AssertStored(() => p.SwapletPrice(c, Context), $"{name}::swapletPrice() not provided");
            AssertStored(() => p.CapletPrice(c, 0.03, Context), $"{name}::capletPrice() not provided");
            AssertStored(() => p.FloorletPrice(c, 0.03, Context), $"{name}::floorletPrice() not provided");
            AssertStored(() => p.SetCapletVolatility().CapletRate(c, 0.03, Context), $"{name}: missing optionlet volatility");
            AssertStored(() => p.SetCapletVolatility().FloorletRate(c, 0.03, true, Context), "empty Handle cannot be dereferenced");
        }

        AssertStored(
            () => blackCompounding.SetEffectiveVolatilityInput(true).CapletRate(compounded, 0.03, true, Context),
            "BlackAverageONIndexedCouponPricer::optionletRateLocal() does not support effective volatility input.");
        AssertStored(
            () => blackAveraging.SetEffectiveVolatilityInput(true).FloorletRate(averaged, 0.03, true, Context),
            "BlackAveragingOvernightIndexedCouponPricer::optionletRateLocal() does not support effective volatility input.");

        // SetCouponPricer's cases, with QuantLib's texts.
        ImmutableArray<CashFlow> compoundedLeg = [compounded];
        ImmutableArray<CashFlow> averagedLeg = [averaged];
        ImmutableArray<CashFlow> cappedCompounded = [new CappedFlooredOvernightIndexedCoupon(compounded, 0.05)];
        ImmutableArray<CashFlow> cappedAveraged = [new CappedFlooredOvernightIndexedCoupon(averaged, 0.05)];
        AssertArgument<ArgumentException>(
            () => CouponPricerFunctions.SetCouponPricer(compoundedLeg, arithmetic),
            "pricer",
            "pricer not compatible with overnight indexed coupon");
        AssertArgument<ArgumentException>(
            () => CouponPricerFunctions.SetCouponPricer(averagedLeg, compounding),
            "pricer",
            "pricer not compatible with arithmetic averaged overnight indexed coupon");
        AssertArgument<ArgumentException>(
            () => CouponPricerFunctions.SetCouponPricer(cappedCompounded, new TestPricer()),
            "pricer",
            "pricer not compatible with capped-floored overnight indexed coupon");
        AssertArgument<ArgumentException>(
            () => CouponPricerFunctions.SetCouponPricer(cappedCompounded, arithmetic),
            "pricer",
            "pricer not compatible with capped-floored overnight indexed coupon");
        AssertArgument<ArgumentException>(
            () => CouponPricerFunctions.SetCouponPricer(cappedAveraged, compounding),
            "pricer",
            "pricer not compatible with arithmetic averaged capped-floored overnight indexed coupon");
    }

    private static void AssertArgument<T>(Func<object> f, string paramName, string? text)
        where T : ArgumentException
    {
        T e = Assert.Throws<T>(f);
        Assert.Equal(typeof(T), e.GetType());
        Assert.Equal(paramName, e.ParamName);
        if (text is not null)
        {
            Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
        }
    }

    private static (Date Date, Date? RateComputationStartDate, string DayCounter)[] Describe(
        ImmutableArray<CashFlow> leg) =>
        [.. leg.Cast<OvernightIndexedCoupon>().Select(c => (c.Date, c.RateComputationStartDate, c.DayCounter.Name))];

    private static void AssertStored(Func<object?> f, string text)
    {
        NanoQLException e = Assert.Throws<NanoQLException>(f);
        Assert.StartsWith(text, e.Message, StringComparison.Ordinal);
    }

    // A curve ending before the coupon does.
    private sealed class ShortCurve(Date referenceDate, Date maxDate)
        : YieldTermStructure(referenceDate, null, new Actual360())
    {
        public override Date MaxDate => maxDate;

        protected override double DiscountImpl(double t) => Math.Exp(-0.04 * t);
    }

    // A pricer that isn't an overnight one.
    private sealed class TestPricer : FloatingRateCouponPricer
    {
        public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) => 0.0;

        public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) => 0.0;

        public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) => 0.0;

        public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) => 0.0;

        public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) => 0.0;

        public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) => 0.0;
    }
}
