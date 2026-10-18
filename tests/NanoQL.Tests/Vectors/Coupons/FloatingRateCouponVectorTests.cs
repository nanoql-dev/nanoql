// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The floating-rate coupon vectors, <c>tests/vectors/cashflows/floatingratecoupon.json</c>: Ibor coupons in advance
/// and in arrears, past, current and future, each under both settings of the at-par switch, with their fixing date,
/// rate, amount, index fixing, adjusted fixing, convexity adjustment, price and accrued amounts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FloatingRateCouponVectorTests
{
    [Fact]
    public void FloatingRateCouponVector()
    {
        var file = VectorFile.Load("cashflows/floatingratecoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FloatingRateCoupon coupon = CouponInputs.CouponOf(c.Input("coupon"), context);
            FlatForward discount = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            Date[] dates = c.DateInputs("accrualDates");

            check.Output(c, "fixingDate", coupon.FixingDate());
            check.Output(c, "fixingDays", coupon.FixingDays);
            check.Output(c, "accrualPeriod", coupon.AccrualPeriod());
            check.Output(c, "index", coupon.Index.Name);
            check.Output(c, "dayCounter", coupon.DayCounter.Name);
            check.Output(c, "rate", () => coupon.Rate(context));
            check.Output(c, "amount", () => coupon.Amount(context));
            check.Output(c, "indexFixing", () => coupon.IndexFixing(context));
            check.Output(c, "adjustedFixing", () => coupon.AdjustedFixing(context));
            check.Output(c, "convexityAdjustment", () => coupon.ConvexityAdjustment(context));
            check.Output(c, "price", () => coupon.Price(discount, context));
            check.Output(c, "accruedAmount", CouponInputs.Each(dates, d => coupon.AccruedAmount(d, context)));
        }

        check.Complete();
    }
}
