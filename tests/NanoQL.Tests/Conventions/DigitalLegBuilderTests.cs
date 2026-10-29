// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The digital and CMS spread legs' builders: each of QuantLib's one-value setters (<c>withGearings(Real)</c> and the
/// rest) sets a one-element list, as QuantLib's do (<c>std::vector&lt;Real&gt;(1, gearing)</c>), and the vectors read
/// the list setters; <see cref="CashFlowVectorsFunctions.FloatingDigitalLeg{TIndex, TCoupon}"/> is QuantLib's
/// <c>FloatingDigitalLeg</c> template, which the builders instantiate; a CMS spread leg's zero payments are the
/// floating legs' (QuantLib's <c>FloatingLeg</c>), as the CMS leg's, which the CMS coupon vectors check against
/// QuantLib's <c>CmsZeroLeg</c>; and a digital coupon's convexity adjustment is its underlying's
/// (<c>digitalcoupon.hpp</c>'s <c>convexityAdjustment</c>).
/// </summary>
public class DigitalLegBuilderTests
{
    [Fact]
    public void TheOneValueSettersSetOneElementLists()
    {
        (_, EvaluationContext context, JsonElement spec) = Case("cxx/cashflows/digitaliborcoupon");
        DigitalIborLeg iborLeg = DigitalIborCouponCxxVectorTests.BuilderOf(spec, context);
        ImmutableArray<CashFlow> one = DigitalIborCouponCxxVectorTests.Priced(
            iborLeg.WithNotionals(50.0).WithFixingDays(1).WithGearings(1.5).WithSpreads(0.001)
                .WithCallStrikes(0.04).WithCallPayoffs(0.01).WithPutStrikes(0.02).WithPutPayoffs(0.005),
            spec,
            context);
        ImmutableArray<CashFlow> list = DigitalIborCouponCxxVectorTests.Priced(
            iborLeg.WithNotionals([50.0]).WithFixingDays([1]).WithGearings([1.5]).WithSpreads([0.001])
                .WithCallStrikes([0.04]).WithCallPayoffs([0.01]).WithPutStrikes([0.02]).WithPutPayoffs([0.005]),
            spec,
            context);
        Assert.Equal(Shape(list, context, true), Shape(one, context, true));
        AssertSet(one);

        (_, context, spec) = Case("cxx/cashflows/digitalcmscoupon");
        DigitalCmsLeg cmsLeg = DigitalCmsCouponCxxVectorTests.BuilderOf(spec, context);
        one = DigitalCmsCouponCxxVectorTests.Priced(
            cmsLeg.WithNotionals(50.0).WithFixingDays(1).WithGearings(1.5).WithSpreads(0.001)
                .WithCallStrikes(0.04).WithCallPayoffs(0.01).WithPutStrikes(0.02).WithPutPayoffs(0.005),
            spec,
            context);
        list = DigitalCmsCouponCxxVectorTests.Priced(
            cmsLeg.WithNotionals([50.0]).WithFixingDays([1]).WithGearings([1.5]).WithSpreads([0.001])
                .WithCallStrikes([0.04]).WithCallPayoffs([0.01]).WithPutStrikes([0.02]).WithPutPayoffs([0.005]),
            spec,
            context);
        Assert.Equal(Shape(list, context, true), Shape(one, context, true));
        AssertSet(one);

        (_, context, spec) = Case("cxx/experimental/coupons/digitalcmsspreadcoupon");
        DigitalCmsSpreadLeg spreadLeg = DigitalCmsSpreadCouponCxxVectorTests.BuilderOf(spec, context);
        one = DigitalCmsSpreadCouponCxxVectorTests.Priced(
            spreadLeg.WithNotionals(50.0).WithFixingDays(1).WithGearings(1.5).WithSpreads(0.001)
                .WithCallStrikes(0.04).WithCallPayoffs(0.01).WithPutStrikes(0.02).WithPutPayoffs(0.005),
            spec,
            context);
        list = DigitalCmsSpreadCouponCxxVectorTests.Priced(
            spreadLeg.WithNotionals([50.0]).WithFixingDays([1]).WithGearings([1.5]).WithSpreads([0.001])
                .WithCallStrikes([0.04]).WithCallPayoffs([0.01]).WithPutStrikes([0.02]).WithPutPayoffs([0.005]),
            spec,
            context);
        Assert.Equal(Shape(list, context, true), Shape(one, context, true));
        AssertSet(one);
    }

    [Fact]
    public void ACmsSpreadLegsOneGearingAndZeroPayments()
    {
        VectorCase c = VectorFile.Load("experimental/coupons/cmsspreadcoupon").Cases.Single(x => x.Name == "plain");
        EvaluationContext context = c.Context();
        JsonElement spec = c.Input("leg");
        CmsSpreadLeg leg = CmsSpreadCouponVectorTests.LegOf(spec, context);
        Assert.Equal(Terms(leg.WithGearings([1.5]).ToLeg()), Terms(leg.WithGearings(1.5).ToLeg()));
        Assert.All(leg.WithGearings(1.5).ToLeg(), cf => Assert.Equal(1.5, ((FloatingRateCoupon)cf).Gearing));

        // every coupon paid at the CMS leg's zero-payment date, on the same schedule
        var cms = new CmsLeg(SwapInputs.ScheduleOf(spec.GetProperty("schedule"), context), SwapIndexInputs.SwapIndexOf(spec.GetProperty("index1")))
            .WithNotionals(100.0).WithPaymentDayCounter(CashFlowInputs.DayCounterOf(spec.GetProperty("paymentDayCounter")))
            .WithPaymentAdjustment((BusinessDayConvention)spec.GetProperty("paymentConvention").GetInt32());
        Date[] zero = [.. leg.WithZeroPayments().ToLeg().Select(cf => cf.Date)];
        Assert.Equal([.. cms.WithZeroPayments().ToLeg().Select(cf => cf.Date)], zero);
        Assert.Single(zero.Distinct());
        Assert.Equal([.. leg.ToLeg().Select(cf => cf.Date)], [.. leg.WithZeroPayments(false).ToLeg().Select(cf => cf.Date)]);
    }

    [Fact]
    public void FloatingDigitalLegIsTheBuildersTemplate()
    {
        (_, EvaluationContext context, JsonElement spec) = Case("cxx/cashflows/digitaliborcoupon");
        Schedule schedule = CouponInputs.ScheduleOf(spec.GetProperty("schedule"), context);
        IborIndex index = IndexInputs.IborIndexOf(spec.GetProperty("index"));
        DayCounter? dayCounter = CashFlowInputs.DayCounterOf(spec.GetProperty("paymentDayCounter"));
        var adjustment = (BusinessDayConvention)spec.GetProperty("paymentAdjustment").GetInt32();
        ImmutableArray<CashFlow> template = CashFlowVectorsFunctions.FloatingDigitalLeg<IborIndex, IborCoupon>(
            (paymentDate, nominal, start, end, fixingDays, i, gearing, spread, refStart, refEnd, dc, inArrears) =>
                new IborCoupon(paymentDate, nominal, start, end, fixingDays, i, gearing, spread, refStart, refEnd, dc, inArrears),
            (underlying, callStrike, callPosition, callAtm, callPayoff, putStrike, putPosition, putAtm, putPayoff, replication, naked) =>
                new DigitalIborCoupon(underlying, callStrike, callPosition, callAtm, callPayoff, putStrike, putPosition, putAtm, putPayoff, replication, naked),
            schedule,
            [100.0],
            index,
            dayCounter,
            adjustment,
            [],
            [],
            [],
            false,
            [0.05],
            PositionType.Long,
            false,
            [0.01],
            [],
            PositionType.Long,
            false,
            [],
            null);
        ImmutableArray<CashFlow> builder = new DigitalIborLeg(schedule, index).WithNotionals(100.0).WithPaymentDayCounter(dayCounter)
            .WithPaymentAdjustment(adjustment).WithCallStrikes(0.05).WithCallPayoffs(0.01).ToLeg();
        FloatingRateCouponPricer pricer = CouponInputs.PricerOf(spec.GetProperty("pricer"), context)!;
        Assert.Equal(
            Shape(CouponPricerFunctions.SetCouponPricer(builder, pricer), context, true),
            Shape(CouponPricerFunctions.SetCouponPricer(template, pricer), context, true));
    }

    [Fact]
    public void ADigitalCouponsConvexityAdjustmentIsItsUnderlyings()
    {
        (_, EvaluationContext context, JsonElement spec) = Case("cxx/cashflows/digitalcmscoupon");
        ImmutableArray<CashFlow> leg = DigitalCmsCouponCxxVectorTests.Priced(DigitalCmsCouponCxxVectorTests.BuilderOf(spec, context), spec, context);
        DigitalCoupon[] digitals = [.. leg.OfType<DigitalCoupon>()];
        Assert.All(digitals, d => Assert.Equal(d.Underlying.ConvexityAdjustment(context), d.ConvexityAdjustment(context)));

        // a coupon fixing after the evaluation date has the Hagan pricer's adjustment
        Assert.Contains(digitals, d => d.ConvexityAdjustment(context) != 0.0);
    }

    // A harness file's "cash calls at 5%" case, its context and leg spec.
    private static (VectorCase Case, EvaluationContext Context, JsonElement Spec) Case(string file)
    {
        VectorCase c = VectorFile.Load(file).Cases.Single(x => x.Name == "cash calls at 5%");
        return (c, c.Context(), c.Input("leg"));
    }

    // Each digital coupon's dates and terms, and with rates its rate.
    private static object?[] Shape(ImmutableArray<CashFlow> leg, EvaluationContext context, bool withRates) =>
        [.. leg.OfType<DigitalCoupon>().SelectMany(d => new object?[]
        {
            d.Date, d.FixingDate(), d.ContractNominal, d.FixingDays, d.Gearing, d.Spread, d.CallStrike, d.CallDigitalPayoff,
            d.PutStrike, d.PutDigitalPayoff, withRates ? d.Rate(context) : null,
        })];

    // Each coupon's payment date and floating terms.
    private static object?[] Terms(ImmutableArray<CashFlow> leg) =>
        [.. leg.OfType<FloatingRateCoupon>().SelectMany(f => new object?[] { f.Date, f.FixingDate(), f.Gearing, f.Spread })];

    private static void AssertSet(ImmutableArray<CashFlow> leg) => Assert.All(leg.OfType<DigitalCoupon>(), d =>
    {
        Assert.Equal((50.0, 1, 1.5, 0.001), (d.ContractNominal, d.FixingDays, d.Gearing, d.Spread));
        Assert.Equal((0.04, 0.01, 0.02, 0.005), (d.CallStrike, d.CallDigitalPayoff, d.PutStrike, d.PutDigitalPayoff));
    });
}
