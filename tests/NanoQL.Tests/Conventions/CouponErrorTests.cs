// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The floating and Ibor coupons', their legs', pricers' and pricer setters' refusals, by type, message and parameter,
/// which a vector's <c>{"throws": true}</c> can't assert: <see cref="ArgumentException"/> (with its parameter) for a
/// public member's argument, <see cref="ArgumentOutOfRangeException"/> for an enum value outside its type, and
/// <see cref="NanoQLException"/> for a value already stored, a builder's settings among them.
/// </summary>
public class CouponErrorTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.January, 2025) };

    private static readonly FlatForward Curve = new(Context.EvaluationDate, 0.03, new Actual365Fixed());

    private static readonly Date Start = new(17, Month.July, 2025);

    private static readonly Date End = new(19, Month.January, 2026);

    [Fact]
    public void TheFloatingAndIborCouponsRefusals()
    {
        var index = new Euribor6M(Curve);

        AssertArgument("gearing", "Null gearing not allowed", () => new FloatingRateCoupon(End, 1e6, Start, End, 2, index, 0.0));
        AssertArgument("gearing", "Null gearing not allowed", () => new IborCoupon(End, 1e6, Start, End, 2, index, 0.0));
        AssertNull("index", () => new FloatingRateCoupon(End, 1e6, Start, End, 2, null!));
        AssertNull("index", () => new IborCoupon(End, 1e6, Start, End, 2, null!));
        AssertRange("fixingConvention", () => new IborCoupon(
            End, 1e6, Start, End, 2, index, fixingConvention: (BusinessDayConvention)99));

        var bare = new IborCoupon(End, 1e6, Start, End, 2, index);
        AssertStored("pricer not set", () => bare.Rate(Context));
        const string NoIborPricer = "IborCoupon: pricer not set or not derived from IborCouponPricer";
        AssertStored(NoIborPricer, () => bare.IndexFixing(Context));
        AssertStored(NoIborPricer, () => bare.FixingValueDate());
        AssertStored(NoIborPricer, () => bare.FixingEndDate());
        AssertStored(NoIborPricer, () => bare.FixingMaturityDate());
        AssertStored(NoIborPricer, () => bare.SpanningTime());
        AssertStored(NoIborPricer, () => bare.SpanningTimeIndexMaturity());

        // A past fixing that isn't stored.
        var pastStart = new Date(17, Month.September, 2024);
        IborCoupon past = new IborCoupon(new Date(17, Month.March, 2025), 1e6, pastStart, new Date(17, Month.March, 2025), 2, index)
            .SetPricer(new BlackIborCouponPricer(Context));
        AssertStored($"Missing {index.Name} fixing for {past.FixingDate()}", () => past.Rate(Context));

        // A fixing period of no length.
        var zero = new IborIndex("Zero", new Period(0, TimeUnit.Days), 2, null, new TARGET(), BusinessDayConvention.Following, false, new Actual360(), Curve);
        IborCoupon degenerate = new IborCoupon(End, 1e6, Start, End, 2, zero)
            .SetPricer(new BlackIborCouponPricer(null, BlackIborCouponPricerTimingAdjustment.Black76, 1.0, true));
        Assert.StartsWith("\n cannot calculate forward rate between", Assert.Throws<NanoQLException>(() => degenerate.FixingEndDate()).Message, StringComparison.Ordinal);

        AssertNull("context", () => bare.HasFixed(null!));
        AssertNull("context", () => bare.IndexFixing(null!));
        AssertNull("discountingCurve", () => bare.Price(null!, Context));
        AssertNull("context", () => bare.Price(Curve, null!));
        AssertNull("context", () => bare.Price(null!, null!));
    }

    [Fact]
    public void TheCappedCouponsRefusals()
    {
        var index = new Euribor6M(Curve);
        var underlying = new IborCoupon(End, 1e6, Start, End, 2, index);

        AssertArgument("cap", "cap level (0.02) less than floor level (0.03)", () => new CappedFlooredCoupon(underlying, 0.02, 0.03));
        AssertArgument("cap", "cap level (0.02) less than floor level (0.03)", () => new CappedFlooredIborCoupon(
            End, 1e6, Start, End, 2, index, -1.0, 0.06, 0.02, 0.03));
        AssertNull("underlying", () => new CappedFlooredCoupon(null!));
        AssertStored("pricer not set", () => new CappedFlooredCoupon(underlying, 0.04).Rate(Context));
        AssertStored("pricer not set", () => new CappedFlooredIborCoupon(End, 1e6, Start, End, 2, index, cap: 0.04).Amount(Context));
        AssertNull("context", () => new CappedFlooredCoupon(underlying, 0.04).Rate(null!));
    }

    [Fact]
    public void TheIborLegsAndFloatingLegsRefusals()
    {
        var index = new Euribor6M(Curve);
        var empty = new Schedule([], new TARGET());
        Schedule schedule = new MakeSchedule().From(Start).To(new Date(17, Month.July, 2027)).WithTenor(new Period(6, TimeUnit.Months))
            .WithCalendar(new TARGET()).ToSchedule();
        Schedule stubbed = new MakeSchedule().From(new Date(17, Month.March, 2025)).To(new Date(17, Month.July, 2027))
            .WithTenor(new Period(6, TimeUnit.Months)).WithCalendar(new TARGET()).Backwards().ToSchedule();
        var selection = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [new Euribor3M(Curve), index]);

        // The builder: its arguments, then its stored settings at the conversion.
        AssertArgument("schedule", "empty schedule", () => new IborLeg(empty, index));
        AssertNull("schedule", () => new IborLeg(null!, index));
        AssertNull("index", () => new IborLeg(schedule, null!), "no index provided");
        var leg = new IborLeg(schedule, index);

        // QuantLib's empty day counter and calendars, null: the index's day counter and the schedule's calendar
        // (cashflowvectors.hpp:107, 128; the coupon's empty day counter is the index's).
        var exCoupon = new Period(2, TimeUnit.Days);
        Assert.Equal(
            Describe(leg.WithNotionals(1e6).WithPaymentLag(2).WithPaymentDayCounter(new Actual360())
                .WithPaymentCalendar(new TARGET()).WithExCouponPeriod(exCoupon, new TARGET(), BusinessDayConvention.Following)
                .ToLeg(Context)),
            Describe(leg.WithNotionals(1e6).WithPaymentLag(2).WithPaymentDayCounter(null).WithPaymentCalendar(null)
                .WithExCouponPeriod(exCoupon, null, BusinessDayConvention.Following).ToLeg(Context)));
        AssertRange("convention", () => leg.WithPaymentAdjustment((BusinessDayConvention)99));
        AssertRange("convention", () => leg.WithFixingConvention((BusinessDayConvention)99));
        AssertRange("fixingDays", () => leg.WithFixingDays(-2));
        AssertNull("context", () => leg.WithNotionals(1e6).ToLeg(null!));
        AssertStored("no notional given", () => leg.ToLeg(Context));
        AssertStored("too many nominals (5), only 4 required", () => leg.WithNotionals([1, 2, 3, 4, 5]).ToLeg(Context));
        AssertStored("too many gearings (5), only 4 required", () => leg.WithNotionals(1).WithGearings([1, 2, 3, 4, 5]).ToLeg(Context));
        AssertStored("too many spreads (5), only 4 required", () => leg.WithNotionals(1).WithSpreads([1, 2, 3, 4, 5]).ToLeg(Context));
        AssertStored("too many caps (5), only 4 required", () => leg.WithNotionals(1).WithCaps([1, 2, 3, 4, 5]).ToLeg(Context));
        AssertStored("too many floors (5), only 4 required", () => leg.WithNotionals(1).WithFloors([1, 2, 3, 4, 5]).ToLeg(Context));
        AssertStored("in-arrears and zero features are not compatible", () => leg.WithNotionals(1).InArrears().WithZeroPayments().ToLeg(Context));

        IborLeg stub = new IborLeg(stubbed, index).WithNotionals(1e6).WithStubIndexSelection(selection);
        AssertStored("stub index conventions are not supported for capped/floored Ibor legs", () => stub.WithIndexedCoupons().WithCaps(0.05).ToLeg(Context));
        AssertStored("stub index conventions are not supported for in-arrears Ibor legs", () => stub.WithIndexedCoupons().InArrears().ToLeg(Context));
        AssertStored("stub index conventions require indexed coupons", () => stub.ToLeg(Context));
        AssertStored("stub index conventions require indexed coupons", () => stub.WithAtParCoupons().ToLeg(Context with { UseAtParCoupons = false }));
        AssertStored(
            "schedule does not provide regularity information for stub index selection",
            () => new IborLeg(new Schedule(stubbed.Dates), index).WithNotionals(1e6).WithIndexedCoupons().WithStubIndexSelection(selection).ToLeg(Context));

        // The function: its arguments.
        AssertArgument("schedule", "empty schedule", () => FloatingLeg(empty, index, [1e6]));
        AssertNull("index", () => FloatingLeg(schedule, null!, [1e6]));
        AssertArgument("nominals", "no notional given", () => FloatingLeg(schedule, index, []));
        AssertArgument("nominals", "too many nominals (5), only 4 required", () => FloatingLeg(schedule, index, [1, 2, 3, 4, 5]));
        AssertArgument("gearings", "too many gearings (5), only 4 required", () => FloatingLeg(schedule, index, [1], gearings: [1, 2, 3, 4, 5]));
        AssertArgument("isZero", "in-arrears and zero features are not compatible", () => FloatingLeg(schedule, index, [1], inArrears: true, isZero: true));
        AssertArgument("paymentDayCounter", "no day counter implementation provided", () => FloatingLeg(schedule, index, [1], gearings: [0.0]));
        AssertRange("fixingDays", () => FloatingLeg(schedule, index, [1], fixingDays: [-1]));
        AssertRange("paymentAdj", () => FloatingLeg(schedule, index, [1], paymentAdj: (BusinessDayConvention)99));
    }

    [Fact]
    public void TheStubCouponsAndSelectionsRefusals()
    {
        var curve = Curve;
        var e3 = new Euribor3M(curve);
        var e6 = new Euribor6M(curve);
        var march = new Date(17, Month.March, 2025);
        var july = new Date(17, Month.July, 2025);

        AssertArgument("indices", "no candidate indices provided for stub index selection", () => new StubIndexSelection(StubIndexSelectionConvention.Interpolated, []));
        AssertArgument("indices", "null candidate index for stub index selection", () => new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [e3, null!]));
        AssertRange("convention", () => new StubIndexSelection((StubIndexSelectionConvention)7, [e3]), "unknown stub index convention");
        Assert.True(new StubIndexSelection().Empty);

        var interpolated = new StubIndexSelection(StubIndexSelectionConvention.Interpolated, [e3, e6]);
        AssertArgument("stubIndexSelection", "StubIborCoupon requires a non-empty stub index selection", () => new StubIborCoupon(july, 1e6, march, july, 2, new StubIndexSelection()));
        AssertArgument("isInArrears", "stub index selection is not supported for in-arrears coupons", () => new StubIborCoupon(july, 1e6, march, july, 2, interpolated, isInArrears: true));
        AssertArgument("startDate", $"{e3.Name} value date {e3.ValueDate(new Date(13, Month.March, 2025))} does not match accrual start date {new Date(15, Month.March, 2025)}", () => new StubIborCoupon(july, 1e6, new Date(15, Month.March, 2025), july, 2, interpolated));
        AssertArgument("stubIndexSelection", $"duplicate candidate index maturity {e6.MaturityDate(march)}", () => new StubIborCoupon(july, 1e6, march, july, 2, new StubIndexSelection(StubIndexSelectionConvention.ClosestIndex, [e6, e3, e6])));
        AssertArgument("endDate", $"index maturities do not bracket irregular coupon end date {new Date(17, Month.December, 2025)}", () => new StubIborCoupon(july, 1e6, march, new Date(17, Month.December, 2025), 2, interpolated));
        AssertArgument("endDate", $"index maturities do not bracket irregular coupon end date {new Date(24, Month.March, 2025)}", () => new StubIborCoupon(july, 1e6, march, new Date(24, Month.March, 2025), 2, interpolated));
        AssertRange("fixingDays", () => new StubIborCoupon(july, 1e6, march, july, -1, interpolated));
        AssertNull("stubIndexSelection", () => new StubIborCoupon(july, 1e6, march, july, 2, null!));

        AssertArgument("components", "no components given for weighted index", () => new StubIborCouponWeightedIndex([]));
        AssertArgument("components", "null component index", () => new StubIborCouponWeightedIndex([(null!, 1.0)]));
        var oneDay = new IborIndex("Euribor", new Period(3, TimeUnit.Months), 1, null, new TARGET(), BusinessDayConvention.ModifiedFollowing, true, new Actual360(), curve);
        AssertArgument("components", $"{oneDay.Name} fixing days (1) do not match {e3.Name} (2)", () => new StubIborCouponWeightedIndex([(e3, 0.5), (oneDay, 0.5)]));
        var nul = new IborIndex("Nul", new Period(3, TimeUnit.Months), 2, null, new NullCalendar(), BusinessDayConvention.ModifiedFollowing, true, new Actual360(), curve);
        AssertArgument("components", $"{nul.Name} fixing calendar does not match {e3.Name}", () => new StubIborCouponWeightedIndex([(e3, 0.5), (nul, 0.5)]));
        var a365 = new IborIndex("Euribor", new Period(3, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing, true, new Actual365Fixed(), curve);
        AssertArgument("components", $"{a365.Name} day counter does not match {e3.Name}", () => new StubIborCouponWeightedIndex([(e3, 0.5), (a365, 0.5)]));
        AssertArgument("components", $"{e3.Name} currency does not match {new IborIndex("Euribor", new Period(3, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing, true, new Actual360()).Name}", () => new StubIborCouponWeightedIndex([(new IborIndex("Euribor", new Period(3, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing, true, new Actual360()), 0.5), (e3, 0.5)]));
        AssertStored(
            "StubIborCoupon::WeightedIndex cannot be relinked to a single forwarding curve; relink its components instead",
            () => new StubIborCouponWeightedIndex([(e3, 1.0)]).Clone(curve));
    }

    [Fact]
    public void ThePricersAndSetCouponPricersRefusals()
    {
        var index = new Euribor6M(Curve);
        var plain = new FloatingRateCoupon(End, 1e6, Start, End, 2, index);
        var ibor = new IborCoupon(End, 1e6, Start, End, 2, index);
        var pricer = new BlackIborCouponPricer(Context);

        AssertArgument("coupon", "IborCouponPricer: expected IborCoupon", () => pricer.SwapletRate(plain, Context));
        AssertArgument("coupon", "IborCouponPricer: expected IborCoupon", () => pricer.CapletPrice(plain, 0.03, Context));
        AssertNull("coupon", () => pricer.SwapletRate(null!, Context));
        AssertNull("context", () => pricer.SwapletRate(ibor, null!));
        AssertNull("context", () => new BlackIborCouponPricer(null!));
        AssertRange("timingAdjustment", () => new BlackIborCouponPricer(Context, null, (BlackIborCouponPricerTimingAdjustment)7), "unknown timing adjustment (code 7)");
        AssertRange("timingAdjustment", () => new BlackIborCouponPricer(null, (BlackIborCouponPricerTimingAdjustment)7, 1.0, true), "unknown timing adjustment (code 7)");

        IborCoupon priced = ibor.SetPricer(pricer);
        AssertStored("missing optionlet volatility", () => pricer.CapletRate(priced, 0.03, Context));
        IborCoupon bare = new IborCoupon(End, 1e6, Start, End, 2, new Euribor6M()).SetPricer(pricer);
        AssertStored("no forecast curve provided", () => pricer.SwapletPrice(bare, Context));
        IborCoupon noAccrual = new IborCoupon(End, 1e6, Start, Start, 2, index).SetPricer(pricer);
        AssertStored("null accrual period", () => pricer.SwapletRate(noAccrual, Context));
        AssertStored("null accrual period", () => noAccrual.Rate(Context));

        // The setters: the visitor's texts, then the leg's and the list's checks.
        var other = new IndexFixingOnlyPricer();
        AssertArgument("pricer", "pricer not compatible with Ibor coupon", () => CouponPricerFunctions.SetCouponPricer([ibor], other));
        AssertArgument("pricer", "pricer not compatible with Ibor coupon", () => CouponPricerFunctions.SetCouponPricer([new CappedFlooredIborCoupon(End, 1e6, Start, End, 2, index, cap: 0.04)], null));
        AssertArgument("pricer", "pricer not compatible with Ibor Coupon", () => CouponPricerFunctions.SetCouponPricer([new CappedFlooredCoupon(ibor, 0.04)], other));
        AssertArgument("leg", "null cash flow in leg", () => CouponPricerFunctions.SetCouponPricer([null!], pricer));
        AssertArgument("leg", "no cashflows", () => CouponPricerFunctions.SetCouponPricers([], [pricer]));
        AssertArgument("pricers", "mismatch between leg size (1) and number of pricers (2)", () => CouponPricerFunctions.SetCouponPricers([ibor], [pricer, pricer]));
        AssertArgument("pricers", "no pricers given", () => CouponPricerFunctions.SetCouponPricers([ibor], []));
        AssertNull("pricers", () => CouponPricerFunctions.SetCouponPricers([ibor], null!));

        // The default case takes any pricer, and the first-matching forms keep a coupon's own when none suits it.
        Assert.Same(other, ((FloatingRateCoupon)CouponPricerFunctions.SetCouponPricer([plain], other)[0]).Pricer);
        Assert.Null(((FloatingRateCoupon)CouponPricerFunctions.SetCouponPricers([ibor], other, other)[0]).Pricer);
    }

    private static ImmutableArray<CashFlow> FloatingLeg(
        Schedule schedule,
        IborIndex index,
        double[] nominals,
        double[]? gearings = null,
        int?[]? fixingDays = null,
        bool inArrears = false,
        bool isZero = false,
        BusinessDayConvention paymentAdj = BusinessDayConvention.Following) =>
        CashFlowVectorsFunctions.FloatingLeg<IborIndex>(
            (pd, n, s, e, fd, i, g, sp, rs, re, dc, ia, ex, fc) => new IborCoupon(pd, n, s, e, fd, i, g, sp, rs, re, dc, ia, ex, fc),
            (pd, n, s, e, fd, i, g, sp, cap, floor, rs, re, dc, ia, ex, fc) =>
                new CappedFlooredIborCoupon(pd, n, s, e, fd, i, g, sp, cap, floor, rs, re, dc, ia, ex, fc),
            schedule,
            nominals,
            index,
            null,
            paymentAdj,
            fixingDays ?? [],
            gearings ?? [],
            [],
            [],
            [],
            inArrears,
            isZero);

    private static void AssertArgument(string paramName, string message, Func<object?> f)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(f);
        Assert.Equal(typeof(ArgumentException), e.GetType());
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void AssertNull(string paramName, Func<object?> f, string? message = null)
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(f);
        Assert.Equal(paramName, e.ParamName);
        if (message is not null)
        {
            Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
        }
    }

    private static (Date Date, Date? ExCouponDate, string DayCounter)[] Describe(ImmutableArray<CashFlow> leg) =>
        [.. leg.Cast<Coupon>().Select(c => (c.Date, c.ExCouponDate, c.DayCounter.Name))];

    private static void AssertRange(string paramName, Func<object?> f, string? message = null)
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(f);
        Assert.Equal(paramName, e.ParamName);
        if (message is not null)
        {
            Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
        }
    }

    private static void AssertStored(string message, Func<object?> f) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(f).Message);

    // A pricer that isn't an Ibor pricer.
    private sealed class IndexFixingOnlyPricer : FloatingRateCouponPricer
    {
        public override double SwapletPrice(FloatingRateCoupon coupon, EvaluationContext context) => 0.0;

        public override double SwapletRate(FloatingRateCoupon coupon, EvaluationContext context) => coupon.IndexFixing(context);

        public override double CapletPrice(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) => 0.0;

        public override double CapletRate(FloatingRateCoupon coupon, double effectiveCap, EvaluationContext context) => 0.0;

        public override double FloorletPrice(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) => 0.0;

        public override double FloorletRate(FloatingRateCoupon coupon, double effectiveFloor, EvaluationContext context) => 0.0;
    }
}
