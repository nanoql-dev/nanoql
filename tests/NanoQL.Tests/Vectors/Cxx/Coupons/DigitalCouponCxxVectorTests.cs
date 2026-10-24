// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The digital coupon's harness vectors, <c>tests/vectors/cxx/cashflows/digitalcoupon.json</c>: calls, puts and
/// collars, cash- and asset-or-nothing, on the three replications and a naked option, over Black-priced Ibor coupons;
/// the inspectors and the three rates; the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DigitalCouponCxxVectorTests
{
    [Fact]
    public void DigitalCouponCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/digitalcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FloatingRateCoupon underlying = CouponInputs.CouponOf(c.Input("underlying"), context);
            if (c.Expected.TryGetProperty("inspectors", out _))
            {
                check.Output(c, "inspectors", () => DigitalInputs.DigitalOf(underlying, c.Input("terms")));
                continue;
            }

            DigitalCoupon coupon = DigitalInputs.DigitalOf(underlying, c.Input("terms"));
            check.Output(c, "digital", true);
            foreach (string name in DigitalInputs.Inspectors)
            {
                check.Output(c, name, DigitalInputs.Inspector(coupon, name));
            }

            foreach (string name in new[] { "rate", "callOptionRate", "putOptionRate" })
            {
                check.Output(c, name, () => DigitalInputs.RateOf(coupon, name, context));
            }
        }

        check.Complete();
    }
}
