// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's floating-rate coupon vectors, <c>tests/vectors/cxx/cashflows/floatingratecoupon.json</c>: a
/// <see cref="FloatingRateCoupon"/> built directly, which the wheel can't build, with its constructor's members, rate,
/// amount, fixings, convexity adjustment, price and accrued amounts, priced by the harness's pricer of any floating-rate
/// coupon; and the failures of an Ibor pricer, of no pricer, of a missing fixing and of a zero gearing.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FloatingRateCouponCxxVectorTests
{
    [Fact]
    public void FloatingRateCouponCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/floatingratecoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("coupon", out _))
            {
                check.Output(c, "coupon", () => CouponInputs.CouponOf(c.Input("coupon"), context));
                continue;
            }

            FloatingRateCoupon coupon = CouponInputs.CouponOf(c.Input("coupon"), context);
            FlatForward discount = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            Date[] dates = c.DateInputs("accrualDates");

            CheckInspectors(check, c, coupon, context);
            check.Output(c, "rate", () => coupon.Rate(context));
            check.Output(c, "indexFixing", () => coupon.IndexFixing(context));
            check.Output(c, "adjustedFixing", () => coupon.AdjustedFixing(context));
            check.Output(c, "convexityAdjustment", () => coupon.ConvexityAdjustment(context));
            check.Output(c, "amount", () => coupon.Amount(context));
            check.Output(c, "price", () => coupon.Price(discount, context));
            check.Output(c, "accruedAmount", CouponInputs.Each(dates, d => coupon.AccruedAmount(d, context)));
        }

        check.Complete();
    }

    /// <summary>The harness's inspectors of a floating-rate coupon, which never throw.</summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <param name="prefix">The outputs' prefix, empty for the coupon's own.</param>
    internal static void CheckInspectors(
        VectorAssert check, VectorCase c, FloatingRateCoupon coupon, EvaluationContext context, string prefix = "")
    {
        check.Output(c, prefix + "date", coupon.Date);
        check.Output(c, prefix + "nominal", coupon.Nominal(context));
        check.Output(c, prefix + "accrualStartDate", coupon.AccrualStartDate);
        check.Output(c, prefix + "accrualEndDate", coupon.AccrualEndDate);
        check.Output(c, prefix + "referencePeriodStart", coupon.ReferencePeriodStart);
        check.Output(c, prefix + "referencePeriodEnd", coupon.ReferencePeriodEnd);
        check.Output(c, prefix + "exCouponDate", coupon.ExCouponDate);
        check.Output(c, prefix + "accrualPeriod", coupon.AccrualPeriod());
        check.Output(c, prefix + "fixingDate", coupon.FixingDate());
        check.Output(c, prefix + "fixingDays", coupon.FixingDays);
        check.Output(c, prefix + "gearing", coupon.Gearing);
        check.Output(c, prefix + "spread", coupon.Spread);
        check.Output(c, prefix + "isInArrears", coupon.IsInArrears);
        check.Output(c, prefix + "fixingConvention", coupon.FixingConvention);
        check.Output(c, prefix + "dayCounter", coupon.DayCounter.Name);
        check.Output(c, prefix + "index", coupon.Index.Name);
        check.Output(c, prefix + "hasPricer", coupon.Pricer is not null);
    }
}
