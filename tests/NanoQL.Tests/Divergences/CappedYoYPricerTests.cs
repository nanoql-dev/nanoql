// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Setting an inflation pricer on a capped year-on-year coupon: QuantLib's <c>setCouponPricer</c> calls the non-virtual
/// <c>InflationCoupon::setPricer</c> (<c>inflationcouponpricer.cpp</c>), so a <c>CappedFlooredYoYInflationCoupon</c>
/// built on an underlying coupon gets the pricer on itself only, while its rate reads the underlying's rate and the
/// underlying's pricer (<c>capflooredinflationcoupon.cpp:88-105</c>): the coupon keeps the underlying's old pricer, and
/// the new one is unused. nanoql's copy carries the pricer into its underlying (<see
/// cref="CappedFlooredYoYInflationCoupon"/>'s <c>WithPricer</c>). QuantLib's check: its coupon with the pricer set on
/// both, which the coupon on its own terms is, priced by the pricer (the vectors): on the at-the-money coupon capped at
/// 3.1%, the unit-displaced Black pricer's 0.026633082577577766, where QuantLib's coupon on an underlying holding the
/// Black pricer keeps that pricer's 0.028900831678540094.
/// </summary>
public class CappedYoYPricerTests
{
    [Fact]
    public void ThePricerReachesTheUnderlying()
    {
        VectorFile file = VectorFile.Load("cashflows/capflooredinflationcoupon");
        VectorCase black = file.Cases.Single(x => x.Name == "coupon Black forecast, at the money, capped / 0");
        VectorCase displaced = file.Cases.Single(x => x.Name == "coupon UnitDisplacedBlack forecast, at the money, capped / 28");
        (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(displaced);
        YoYInflationCoupon underlying = new YoYInflationCoupon(
            displaced.DateInput("paymentDate"),
            displaced.Double("nominal"),
            displaced.DateInput("startDate"),
            displaced.DateInput("endDate"),
            displaced.Int("fixingDays"),
            (YoYInflationIndex)index,
            InflationCashFlowInputs.PeriodOf(displaced.Input("observationLag")),
            (CPIInterpolationType)displaced.Int("interpolation"),
            CashFlowInputs.DayCounterOf(displaced.Input("dayCounter")),
            displaced.Double("gearing"),
            displaced.Double("spread")).SetPricer(CappedFlooredYoYInflationCouponVectorTests.PricerOf(black, context));
        var capped = new CappedFlooredYoYInflationCoupon(underlying, displaced.Double("cap"));
        Assert.Equal(0.028900831678540094, capped.Rate(context), 1e-15);

        ImmutableArray<CashFlow> leg = InflationCouponPricerFunctions.SetCouponPricer(
            [capped], CappedFlooredYoYInflationCouponVectorTests.PricerOf(displaced, context));
        var repriced = (CappedFlooredYoYInflationCoupon)leg[0];
        Assert.Equal(0.026633082577577766, repriced.Rate(context), 1e-15);
        Assert.Same(repriced.Pricer, repriced.Underlying!.Pricer);
    }
}
