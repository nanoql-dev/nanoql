// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A range accrual with no observation dates (the S-cms plan's CMR5 (xiii)): QuantLib's <c>RangeAccrualLeg</c> defaults
/// its observation tenor to <c>Period()</c>, so each coupon's observation schedule holds its start and end alone, and
/// the BGM pricer divides a sum over no observations by their number, 0/0: on the wheel, the vectors' semiannual leg
/// built without a tenor pays a rate of NaN, and a coupon whose observation schedule holds its start and end alone pays
/// NaN on each of the pricer's three routes. nanoql's leg refuses a floating coupon with no observation tenor, and its
/// pricer a coupon with no observation dates, each with a text naming what is missing.
/// </summary>
public class RangeAccrualObservationTenorTests
{
    [Fact]
    public void ALegWithoutAnObservationTenorIsRefused()
    {
        VectorCase c = Case();
        EvaluationContext context = c.Context();
        IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
        RangeAccrualLeg builder = RangeAccrualVectorTests.LegOf(c.Input("leg"), index, context)
            .WithObservationTenor(new Period());
        NanoQLException e = Assert.Throws<NanoQLException>(() => builder.ToLeg(context));
        Assert.Equal("no observation tenor given: a range-accrual coupon needs observation dates between its start and end", e.Message);
    }

    [Fact]
    public void ACouponWithNoObservationDatesIsRefused()
    {
        VectorCase c = Case();
        EvaluationContext context = c.Context();
        IborIndex index = IndexInputs.IborIndexOf(c.Input("index"));
        Date start = new(15, July, 2025), end = new(15, January, 2026);
        var observations = new Schedule(
            start,
            end,
            new Period(1, TimeUnit.Years),
            new TARGET(),
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            false,
            context);
        var coupon = new RangeAccrualFloatersCoupon(
            end, 1e6, index, start, end, 2, new Actual360(), 1.0, 0.0, start, end, observations, 0.02, 0.04);
        Assert.Equal(0, coupon.ObservationsNo);
        RangeAccrualPricerByBgm pricer = RangeAccrualVectorTests.PricerOf(c.Input("pricer"), context);
        NanoQLException e = Assert.Throws<NanoQLException>(() => coupon.SetPricer(pricer).Rate(context));
        Assert.Equal("no observation dates: the coupon's observation schedule has its start and end alone", e.Message);
    }

    private static VectorCase Case() => VectorFile.Load("cashflows/rangeaccrual").Cases
        .Single(x => x.Name.StartsWith("leg semiannual, weekly /", StringComparison.Ordinal));
}
