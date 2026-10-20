// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's inflation coupon vectors, <c>tests/vectors/cxx/cashflows/inflationcoupon.json</c>:
/// <see cref="InflationCoupon.Price"/> on CPI and year-on-year coupons with pricers of their types, and
/// <see cref="InflationCouponExtensions.SetPricer"/> accepting a pricer of the coupon's type and refusing another or
/// none.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationCouponCxxVectorTests
{
    [Fact]
    public void InflationCouponCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/inflationcoupon");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            string kind = c.String("kind");
            InflationCoupon coupon = CouponOf(c, kind.StartsWith("cpi", StringComparison.Ordinal));
            JsonElement p = c.Input("pricer");
            InflationCouponPricer? pricer =
                p.ValueKind == JsonValueKind.Null ? null : InflationCashFlowInputs.PricerOf(p, context);
            if (kind.EndsWith("Price", StringComparison.Ordinal))
            {
                check.Output(
                    c,
                    "price",
                    () => coupon.SetPricer(pricer).Price(CashFlowInputs.CurveOf(c.Input("discountCurve")), context));
            }
            else
            {
                check.Output(c, "setPricer", () => coupon.SetPricer(pricer) is not null);
            }
        }

        check.Complete();
    }

    private static InflationCoupon CouponOf(VectorCase c, bool cpi)
    {
        InflationIndex index = InflationInputs.IndexOf(c.Input("index"));
        return cpi
            ? InflationCashFlowInputs.CPICouponOf(c.Input("coupon"), (ZeroInflationIndex)index)
            : InflationCashFlowInputs.YoYCouponOf(c.Input("coupon"), (YoYInflationIndex)index);
    }
}
