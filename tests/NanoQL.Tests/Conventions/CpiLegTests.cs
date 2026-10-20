// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="CPILeg"/>'s refusals and the leg it builds. A missing notional, a schedule with periods and no fixed
/// rate, and a cap or floor on a coupon with a non-zero rate are each a <see cref="NanoQLException"/> with QuantLib's
/// text; an empty schedule is one too, "empty schedule", where QuantLib's <c>schedule_.date(0)</c> throws
/// <c>std::out_of_range</c>, after asking for the fixed rates as QuantLib does; QuantLib's empty day counter, here
/// <see langword="null"/>, builds coupons that fail on their first accrual with "no day counter implementation
/// provided", which <see cref="CPILeg.ToLeg"/> gives when it would build one, a schedule of one date still building the
/// notional flow alone. The leg is a CPI coupon per period and the notional flow last, each coupon priced by QuantLib's
/// default <see cref="CPICouponPricer"/>.
/// </summary>
public class CpiLegTests
{
    private static readonly EvaluationContext Context = new() { EvaluationDate = new Date(15, Month.June, 2024) };

    [Fact]
    public void AMissingNotionalIsRefused()
    {
        Assert.Equal(
            "no notional given",
            Assert.Throws<NanoQLException>(() => Leg().WithFixedRates(0.02).ToLeg()).Message);
    }

    [Fact]
    public void AMissingFixedRateIsRefused()
    {
        Assert.Equal(
            "no fixedRates given",
            Assert.Throws<NanoQLException>(() => Leg().WithNotionals(1e6).ToLeg()).Message);
    }

    [Fact]
    public void AnEmptyScheduleIsRefused()
    {
        var empty = new CPILeg(new Schedule([], new UnitedKingdom()), new UKRPI(), 305.0, new Period(3, TimeUnit.Months))
            .WithNotionals(1e6);
        Assert.Equal("no fixedRates given", Assert.Throws<NanoQLException>(() => empty.ToLeg()).Message);
        Assert.Equal("empty schedule", Assert.Throws<NanoQLException>(() => empty.WithFixedRates(0.02).ToLeg()).Message);
    }

    [Fact]
    public void ACappedCouponIsRefused()
    {
        Assert.Equal(
            "caps/floors on CPI coupons not implemented.",
            Assert.Throws<NanoQLException>(() => Leg().WithNotionals(1e6).WithFixedRates(0.02).WithCaps(0.05).ToLeg()).Message);
        Assert.Equal(
            "caps/floors on CPI coupons not implemented.",
            Assert.Throws<NanoQLException>(() => Leg().WithNotionals(1e6).WithFixedRates(0.02).WithFloors(0.0).ToLeg()).Message);
    }

    [Fact]
    public void AnEmptyDayCounterIsRefusedWhereACouponIsBuilt()
    {
        Assert.Equal(
            "no day counter implementation provided",
            Assert.Throws<NanoQLException>(
                () => Leg().WithNotionals(1e6).WithFixedRates(0.02).WithPaymentDayCounter(null).ToLeg()).Message);

        var oneDate = new Schedule([new Date(15, Month.June, 2029)], new UnitedKingdom());
        ImmutableArray<CashFlow> notionalOnly = new CPILeg(oneDate, new UKRPI(), 305.0, new Period(3, TimeUnit.Months))
            .WithNotionals(1e6)
            .WithPaymentDayCounter(null)
            .ToLeg();
        Assert.IsType<CPICashFlow>(Assert.Single(notionalOnly));
    }

    [Fact]
    public void TheLegEndsWithItsNotionalFlowAndCarriesTheDefaultPricer()
    {
        ImmutableArray<CashFlow> leg = Leg().WithNotionals(1e6).WithFixedRates(0.02).ToLeg();

        Assert.Equal(4, leg.Length);
        Assert.All(leg[..^1], f => Assert.IsType<CPICouponPricer>(Assert.IsType<CPICoupon>(f).Pricer));
        Assert.IsType<CPICashFlow>(leg[^1]);
    }

    private static CPILeg Leg() =>
        new(
            new Schedule(
                new Date(15, Month.June, 2024),
                new Date(15, Month.June, 2027),
                new Period(1, TimeUnit.Years),
                new UnitedKingdom(),
                BusinessDayConvention.Unadjusted,
                BusinessDayConvention.Unadjusted,
                DateGenerationRule.Forward,
                false,
                Context),
            new UKRPI(),
            305.0,
            new Period(3, TimeUnit.Months));
}
