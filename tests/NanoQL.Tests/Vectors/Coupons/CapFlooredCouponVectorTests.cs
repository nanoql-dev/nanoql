// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The capped and floored coupon vectors, <c>tests/vectors/cashflows/capflooredcoupon.json</c>: capped, floored and
/// collared Ibor coupons, each with its own Black pricer on a lognormal, shifted lognormal or normal volatility, with
/// their rate, amount, caps and floors, effective caps and floors, and the refusals of a cap below the floor and of a
/// coupon with no pricer.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CapFlooredCouponVectorTests
{
    [Fact]
    public void CapFlooredCouponVector()
    {
        var file = VectorFile.Load("cashflows/capflooredcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("coupon", out _))
            {
                check.Output(c, "coupon", () => CouponInputs.CouponOf(c.Input("coupon"), context));
                continue;
            }

            var coupon = (CappedFlooredCoupon)CouponInputs.CouponOf(c.Input("coupon"), context);
            FlatForward discount = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            Date mid = c.DateInput("accrualDate");

            check.Output(c, "nominal", coupon.Nominal(context));
            check.Output(c, "fixingDate", coupon.FixingDate());
            check.Output(c, "rate", () => coupon.Rate(context));
            check.Output(c, "amount", () => coupon.Amount(context));
            check.Output(c, "cap", coupon.Cap);
            check.Output(c, "floor", coupon.Floor);
            check.Output(c, "effectiveCap", coupon.EffectiveCap);
            check.Output(c, "effectiveFloor", coupon.EffectiveFloor);
            check.Output(c, "isCapped", coupon.IsCapped);
            check.Output(c, "isFloored", coupon.IsFloored);
            check.Output(c, "indexFixing", () => coupon.IndexFixing(context));
            check.Output(c, "adjustedFixing", () => coupon.AdjustedFixing(context));
            check.Output(c, "convexityAdjustment", () => coupon.ConvexityAdjustment(context));
            check.Output(c, "price", () => coupon.Price(discount, context));
            check.Output(c, "accruedAmount", () => coupon.AccruedAmount(mid, context));
        }

        check.Complete();
    }
}
