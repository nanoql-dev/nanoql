// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="YoYInflationLeg"/>'s refusals and the leg it builds. QuantLib's empty payment day counter, here
/// <see langword="null"/>, and a missing notional are each a <see cref="NanoQLException"/> with QuantLib's text, as are
/// more notionals, gearings or spreads than periods ("too many gearings (3), only 2 required"); an empty schedule is
/// one too, "empty schedule", where QuantLib's <c>leg.reserve(n)</c> throws <c>std::length_error</c>. Each coupon is
/// priced by QuantLib's default <see cref="YoYInflationCouponPricer"/>, which QuantLib attaches to a leg with no cap or
/// floor.
/// </summary>
public class YoYInflationLegTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.June, 2024) };

    [Fact]
    public void AMissingDayCounterOrNotionalIsRefused()
    {
        Assert.Equal(
            "no payment daycounter given",
            Assert.Throws<NanoQLException>(() => Leg().WithNotionals(1e6).ToLeg()).Message);
        Assert.Equal(
            "no notional given",
            Assert.Throws<NanoQLException>(() => Leg().WithPaymentDayCounter(new Actual365Fixed()).ToLeg()).Message);
    }

    [Fact]
    public void AnEmptyScheduleIsRefused()
    {
        var empty = new YoYInflationLeg(
            new Schedule([], new UnitedKingdom()),
            new UnitedKingdom(),
            new YoYInflationIndex(new UKRPI()),
            new Period(2, TimeUnit.Months),
            CPIInterpolationType.Flat);
        Assert.Equal(
            "empty schedule",
            Assert.Throws<NanoQLException>(() => empty.WithNotionals(1e6).WithPaymentDayCounter(new Actual365Fixed()).ToLeg()).Message);
    }

    [Fact]
    public void MoreValuesThanPeriodsAreRefused()
    {
        YoYInflationLeg leg = Leg().WithNotionals(1e6).WithPaymentDayCounter(new Actual365Fixed());
        Assert.Equal(
            "too many gearings (3), only 2 required",
            Assert.Throws<NanoQLException>(() => leg.WithGearings([1.0, 1.0, 1.0]).ToLeg()).Message);
        Assert.Equal(
            "too many spreads (3), only 2 required",
            Assert.Throws<NanoQLException>(() => leg.WithSpreads([0.0, 0.0, 0.0]).ToLeg()).Message);
        Assert.Equal(
            "too many nominals (3), only 2 required",
            Assert.Throws<NanoQLException>(() => leg.WithNotionals([1e6, 1e6, 1e6]).ToLeg()).Message);
    }

    [Fact]
    public void EachCouponCarriesTheDefaultPricer()
    {
        ImmutableArray<CashFlow> leg = Leg().WithNotionals(1e6).WithPaymentDayCounter(new Actual365Fixed()).ToLeg();

        Assert.Equal(2, leg.Length);
        Assert.All(leg, f => Assert.IsType<YoYInflationCouponPricer>(Assert.IsType<YoYInflationCoupon>(f).Pricer));
    }

    private static YoYInflationLeg Leg() =>
        new YoYInflationLeg(
            new Schedule(
                new Date(15, Month.June, 2024),
                new Date(15, Month.June, 2026),
                new Period(1, TimeUnit.Years),
                new UnitedKingdom(),
                BusinessDayConvention.Unadjusted,
                BusinessDayConvention.Unadjusted,
                DateGenerationRule.Forward,
                false,
                Context),
            new UnitedKingdom(),
            new YoYInflationIndex(new UKRPI()),
            new Period(2, TimeUnit.Months),
            CPIInterpolationType.Flat)
        .WithPaymentDayCounter(null);
}
