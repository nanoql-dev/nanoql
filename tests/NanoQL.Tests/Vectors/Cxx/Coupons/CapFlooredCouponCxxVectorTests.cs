// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's capped and floored coupon vectors, <c>tests/vectors/cxx/cashflows/capflooredcoupon.json</c>:
/// <see cref="CappedFlooredCoupon.Underlying"/>, which the wheel doesn't bind, with its members, its pricer (the capped
/// coupon's own after <see cref="FloatingRateCouponExtensions.SetPricer"/>), its rate and index fixing, and the capped
/// coupon's rate over an Ibor coupon or a floating-rate coupon built directly.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CapFlooredCouponCxxVectorTests
{
    [Fact]
    public void CapFlooredCouponCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/capflooredcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var coupon = (CappedFlooredCoupon)CouponInputs.CouponOf(c.Input("coupon"), context);
            FloatingRateCoupon underlying = coupon.Underlying;

            FloatingRateCouponCxxVectorTests.CheckInspectors(check, c, underlying, context, "underlying.");
            check.Output(
                c,
                "underlying.pricerIsCoupons",
                coupon.Pricer is not null && ReferenceEquals(underlying.Pricer, coupon.Pricer));
            check.Output(c, "underlying.rate", () => underlying.Rate(context));
            check.Output(c, "underlying.indexFixing", () => underlying.IndexFixing(context));
            check.Output(c, "rate", () => coupon.Rate(context));
        }

        check.Complete();
    }
}
