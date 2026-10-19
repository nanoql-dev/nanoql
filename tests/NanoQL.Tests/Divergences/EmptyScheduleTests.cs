// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A leg builder given an empty schedule, or a swap building its legs on one. QuantLib's conversion to a leg counts
/// <c>schedule_.size()-1</c> periods, a <c>Size</c> wrapping below zero, and reserves them or reads
/// <c>schedule_.date(0)</c> past the end of the schedule's dates (<c>fixedratecoupon.cpp:182-185</c>,
/// <c>cashflowvectors.hpp:82-102</c>, <c>overnightindexedcoupon.cpp:592-603</c>, <c>averagebmacoupon.cpp:225-228</c>),
/// which is undefined. nanoql refuses the
/// schedule at that point of the conversion, with <see cref="NanoQLException"/>, "empty schedule", after the checks
/// QuantLib makes before it, so that with several bad inputs QuantLib's first refusal is nanoql's; the pilot's
/// <see cref="FixedRateLeg"/> threw <see cref="ArgumentOutOfRangeException"/> from the read.
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

    private static void AssertRefused(string message, Func<object> build)
    {
        NanoQLException e = Assert.Throws<NanoQLException>(build);
        Assert.Equal(message, e.Message);
    }
}
