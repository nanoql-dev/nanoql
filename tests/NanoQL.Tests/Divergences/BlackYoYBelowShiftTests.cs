// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Black year-on-year pricer's option struck at or below 0 (its year-on-year part): QuantLib's
/// <c>BlackYoYInflationCouponPricer</c> prices an optionlet by <c>blackFormula</c> at the coupon's effective strike,
/// unshifted (<c>inflationcouponpricer.cpp:178-188</c>), which refuses a strike below 0, so a coupon capped or floored
/// below 0 refuses: on the wheel, the vectors' at-the-money coupon (a forward of 3.05%) capped at −1% refuses ("strike
/// + displacement (-0.01 + 0) must be non-negative"), at −1e-12 likewise, and at 0 pays 0.0. nanoql's caplet there is
/// the forward less the strike and its floorlet 0, the formula's limits, so a coupon capped below 0 pays its cap and
/// one floored there its swaplet. QuantLib's check: its Black formula at 0 and at 1e-12, a coupon capped at 1e-12
/// paying 9.999986949615902e-13.
/// </summary>
public class BlackYoYBelowShiftTests
{
    [Fact]
    public void ACouponCappedBelowZeroPaysItsCap()
    {
        VectorCase c = VectorFile.Load("cashflows/capflooredinflationcoupon").Cases
            .Single(x => x.Name == "coupon Black forecast, at the money, capped / 0");
        (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
        YoYInflationCouponPricer pricer = CappedFlooredYoYInflationCouponVectorTests.PricerOf(c, context);
        CappedFlooredYoYInflationCoupon Coupon(double? cap, double? floor) => new CappedFlooredYoYInflationCoupon(
            c.DateInput("paymentDate"),
            c.Double("nominal"),
            c.DateInput("startDate"),
            c.DateInput("endDate"),
            c.Int("fixingDays"),
            (YoYInflationIndex)index,
            InflationCashFlowInputs.PeriodOf(c.Input("observationLag")),
            (CPIInterpolationType)c.Int("interpolation"),
            CashFlowInputs.DayCounterOf(c.Input("dayCounter")),
            c.Double("gearing"),
            c.Double("spread"),
            cap,
            floor).SetPricer(pricer);

        Assert.Equal(0.0305, Coupon(null, null).Rate(context));
        Assert.Equal(-0.01, Coupon(-0.01, null).Rate(context), 1e-15);
        Assert.Equal(-1e-12, Coupon(-1e-12, null).Rate(context), 1e-15);
        Assert.Equal(0.0, Coupon(0.0, null).Rate(context));
        Assert.Equal(9.999986949615902e-13, Coupon(1e-12, null).Rate(context));
        Assert.Equal(0.0305, Coupon(null, -0.01).Rate(context), 1e-15);
    }
}
