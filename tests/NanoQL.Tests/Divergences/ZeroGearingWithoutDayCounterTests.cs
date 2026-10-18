// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A floating leg whose period has a zero gearing becomes a fixed-rate coupon on the leg's payment day counter
/// (<c>cashflowvectors.hpp:134-136</c>; for an overnight leg, <c>overnightindexedcoupon.cpp</c>'s
/// <c>OvernightLeg</c>). Without one, QuantLib builds that coupon on an empty day counter and fails only when it is
/// priced, "no day counter implementation provided" (<c>daycounter.hpp:118</c>); nanoql's
/// <see cref="FixedRateCoupon"/> needs a day counter, so the leg is refused when it is built, with QuantLib's text: a
/// builder's stored setting with <see cref="NanoQLException"/>, and the function form's argument with
/// <see cref="ArgumentException"/>.
/// </summary>
public class ZeroGearingWithoutDayCounterTests
{
    [Fact]
    public void AZeroGearingPeriodWithoutAPaymentDayCounterIsRefusedAtBuild()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2024) };
        Schedule schedule = new MakeSchedule()
            .From(new Date(17, Month.January, 2024)).To(new Date(17, Month.January, 2025))
            .WithFrequency(Frequency.Semiannual).WithCalendar(new TARGET()).ToSchedule();

        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new IborLeg(schedule, new Euribor6M()).WithNotionals(100.0).WithGearings(0.0).ToLeg(context));
        Assert.Equal("no day counter implementation provided", e.Message);

        // With a payment day counter, the zero-gearing period is QuantLib's fixed-rate coupon.
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, new Euribor6M()).WithNotionals(100.0).WithGearings(0.0)
            .WithSpreads(0.01).WithPaymentDayCounter(new Actual360()).ToLeg(context);
        Assert.All(leg, cf => Assert.IsType<FixedRateCoupon>(cf));
    }

    [Fact]
    public void AnOvernightLegsZeroGearingPeriodWithoutAPaymentDayCounterIsRefusedAtBuild()
    {
        Schedule schedule = new MakeSchedule()
            .From(new Date(17, Month.January, 2024)).To(new Date(17, Month.January, 2025))
            .WithFrequency(Frequency.Semiannual).WithCalendar(new TARGET()).ToSchedule();

        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new OvernightLeg(schedule, new Estr()).WithNotionals(100.0).WithGearings(0.0).ToLeg());
        Assert.Equal("no day counter implementation provided", e.Message);

        ImmutableArray<CashFlow> leg = new OvernightLeg(schedule, new Estr()).WithNotionals(100.0).WithGearings(0.0)
            .WithSpreads(0.01).WithPaymentDayCounter(new Actual360()).ToLeg();
        Assert.All(leg, cf => Assert.IsType<FixedRateCoupon>(cf));
    }
}
