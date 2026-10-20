// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The leg builders' refusal of an empty schedule, and a swap's when it builds its legs on one: a C++ library exception
/// in QuantLib, which nanoql refuses with its own text (the playbook's row for such exceptions). QuantLib's conversion
/// to a leg counts <c>schedule_.size()-1</c> periods, a <c>Size</c> that wraps to its largest value, so its size checks
/// pass. It then reserves that many coupons, and <c>reserve</c> throws <c>std::length_error</c>
/// (<c>fixedratecoupon.cpp:182</c>, <c>cashflowvectors.hpp:82-102</c>; on the wheel, "vector too long" from the
/// fixed-rate and Ibor legs and the overnight-indexed swap's fixed leg), or reads <c>schedule_.date(0)</c>, whose
/// <c>at()</c> throws <c>std::out_of_range</c> (<c>overnightindexedcoupon.cpp:592-603</c>,
/// <c>averagebmacoupon.cpp:225-228</c>; on the wheel, "invalid vector subscript" from the overnight leg and the BMA
/// swap). nanoql refuses the schedule at that point of the conversion: <see cref="NanoQLException"/>, "empty schedule",
/// from a builder's conversion, and <see cref="ArgumentException"/> naming <c>schedule</c> from
/// <see cref="CashFlowVectorsFunctions.FloatingLeg"/>, whose schedule is an argument. It refuses after the checks
/// QuantLib makes before it, so that with several bad inputs QuantLib's first refusal is nanoql's, but for the
/// overnight leg given payment dates, where QuantLib's own check refuses first with the wrapped count in its text,
/// which nanoql's replaces (<c>Divergences/OvernightIndexedCouponRefusalTests</c>, O3). A one-date schedule stays
/// QuantLib's.
/// </summary>
public class EmptyScheduleTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly FlatForward Curve = new(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void ALegBuilderRefusesAnEmptyScheduleAfterQuantLibsEarlierChecks()
    {
        var empty = new Schedule([], new TARGET());
        var context = new EvaluationContext { EvaluationDate = Today };
        var euribor = new Euribor6M(Curve);
        var estr = new Estr(Curve);

        // The schedule alone: refused by each builder's conversion, not when the builder is made.
        var fixedLeg = new FixedRateLeg(empty);
        AssertRefused("empty schedule", () => fixedLeg.WithNotionals(100.0).WithCouponRates(0.03, new Actual360()).ToLeg());
        var iborLeg = new IborLeg(empty, euribor);
        AssertRefused("empty schedule", () => iborLeg.WithNotionals(100.0).ToLeg(context));
        var overnightLeg = new OvernightLeg(empty, estr);
        AssertRefused("empty schedule", () => overnightLeg.WithNotionals(100.0).ToLeg());

        // With another bad input, QuantLib's refusal of it comes first: the fixed-rate leg's rates and notionals
        // (fixedratecoupon.cpp:178-179; wheel: "no notional given"), the floating legs' notionals and in-arrears check
        // (cashflowvectors.hpp:83, 99-100; wheel: "no notional given"), and the overnight leg's notionals and pricer
        // (overnightindexedcoupon.cpp:566-575; wheel: "no notional given").
        AssertRefused("no coupon rates given", () => fixedLeg.WithNotionals(100.0).ToLeg());
        AssertRefused("no notional given", () => fixedLeg.WithCouponRates(0.03, new Actual360()).ToLeg());
        AssertRefused("no notional given", () => iborLeg.ToLeg(context));
        AssertRefused(
            "in-arrears and zero features are not compatible",
            () => iborLeg.WithNotionals(100.0).InArrears().WithZeroPayments().ToLeg(context));
        AssertRefused("no notional given", () => overnightLeg.ToLeg());
        AssertRefused(
            "Wrong coupon pricer provided, provide a CompoundingOvernightIndexedCouponPricer",
            () => overnightLeg.WithNotionals(100.0)
                .WithCouponPricer(new ArithmeticAveragedOvernightIndexedCouponPricer()).ToLeg());

        // A swap reaches its legs' checks in QuantLib's order: an empty fixed schedule with no fixed nominals is
        // refused for the nominals (fixedvsfloatingswap.cpp:62-68; wheel: "no notional given").
        Schedule schedule = new MakeSchedule().From(new Date(17, Month.January, 2025)).To(new Date(17, Month.January, 2027))
            .WithFrequency(Frequency.Annual).WithCalendar(new TARGET()).ToSchedule();
        AssertRefused(
            "no notional given",
            () => new OvernightIndexedSwap(SwapType.Payer, [], empty, 0.03, null, [1e6], schedule, estr));
        AssertRefused(
            "empty schedule",
            () => new OvernightIndexedSwap(SwapType.Payer, [1e6], empty, 0.03, null, [1e6], schedule, estr));
    }

    [Fact]
    public void TheAverageBMALegRefusesAnEmptyScheduleAfterItsNotionalCheck()
    {
        var empty = new Schedule([], new TARGET());
        var context = new EvaluationContext { EvaluationDate = Today };
        var bma = new BMAIndex(Curve);
        var leg = new AverageBMALeg(empty, bma);

        // QuantLib's conversion checks the notionals first (averagebmacoupon.cpp:215; harness: "no notional given"),
        // then counts the wrapping periods.
        AssertRefused("no notional given", () => leg.ToLeg());
        AssertRefused("empty schedule", () => leg.WithNotionals(100.0).ToLeg());

        // A BMA swap builds its Libor leg, then its BMA leg on the empty schedule (wheel: "invalid vector subscript").
        Schedule libor = new MakeSchedule().From(new Date(17, Month.January, 2025)).To(new Date(17, Month.January, 2027))
            .WithTenor(new Period(3, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();
        AssertRefused(
            "empty schedule",
            () => new BMASwap(SwapType.Payer, 1e6, libor, 0.7, 0.0, new Euribor3M(Curve), null, empty, bma, null, context));
    }

    [Fact]
    public void TheFloatingLegBuildersRefuseAnEmptySchedule()
    {
        // QuantLib's FloatingLeg reserves the wrapped count after its checks (cashflowvectors.hpp:102; IborLeg reaches
        // it at iborcoupon.cpp:286): std::length_error.
        var empty = new Schedule([], new TARGET());
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };

        NanoQLException n = Assert.Throws<NanoQLException>(() =>
            new IborLeg(empty, new Euribor6M()).WithNotionals(1e6).WithCaps(0.05).ToLeg(context));
        Assert.Equal("empty schedule", n.Message);

        ArgumentException e = Assert.Throws<ArgumentException>(() => FloatingLeg(empty, new Euribor6M()));
        Assert.Equal("schedule", e.ParamName);
        Assert.StartsWith("empty schedule", e.Message, StringComparison.Ordinal);

        // A one-date schedule stays QuantLib's: no periods, so the nominal given is one too many.
        var oneDate = new Schedule([new Date(15, Month.January, 2025)], new TARGET());
        e = Assert.Throws<ArgumentException>(() => FloatingLeg(oneDate, new Euribor6M()));
        Assert.Equal("nominals", e.ParamName);
        Assert.StartsWith("too many nominals (1), only 0 required", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFixedRateLegsRefusal()
    {
        // The empty schedule is a value the builder stores, refused by its conversion.
        FixedRateLeg leg = new FixedRateLeg(new Schedule([], new TARGET()))
            .WithNotionals(100.0)
            .WithCouponRates(0.03, new Actual360());
        NanoQLException e = Assert.Throws<NanoQLException>(() => leg.ToLeg());

        Assert.Equal(typeof(NanoQLException), e.GetType());
        Assert.Equal("empty schedule", e.Message);
    }

    private static void AssertRefused(string message, Func<object> build)
    {
        NanoQLException e = Assert.Throws<NanoQLException>(build);
        Assert.Equal(message, e.Message);
    }

    private static ImmutableArray<CashFlow> FloatingLeg(Schedule schedule, IborIndex index) =>
        CashFlowVectorsFunctions.FloatingLeg<IborIndex>(
            (paymentDate, nominal, start, end, fixingDays, i, gearing, spread, refStart, refEnd, dc, inArrears, exCoupon, fixingConvention) =>
                new IborCoupon(paymentDate, nominal, start, end, fixingDays, i, gearing, spread, refStart, refEnd, dc, inArrears, exCoupon, fixingConvention),
            (paymentDate, nominal, start, end, fixingDays, i, gearing, spread, cap, floor, refStart, refEnd, dc, inArrears, exCoupon, fixingConvention) =>
                new CappedFlooredIborCoupon(paymentDate, nominal, start, end, fixingDays, i, gearing, spread, cap, floor, refStart, refEnd, dc, inArrears, exCoupon, fixingConvention),
            schedule,
            [1e6],
            index,
            null,
            BusinessDayConvention.Following,
            [],
            [],
            [],
            [],
            [],
            false,
            false);
}
