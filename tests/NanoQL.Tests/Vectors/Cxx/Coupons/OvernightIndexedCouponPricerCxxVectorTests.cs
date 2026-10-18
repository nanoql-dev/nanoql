// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's overnight pricer vectors, <c>tests/vectors/cxx/cashflows/overnightindexedcouponpricer.json</c>:
/// the pricer members the wheel doesn't reach, called directly on plain and capped coupons:
/// <see cref="OvernightIndexedCouponPricer.AverageRate"/>, the two-argument caplet and floorlet rates, the effective
/// caplet and floorlet volatilities, and <see cref="CompoundingOvernightIndexedCouponPricer.EffectiveSpread"/> and
/// <see cref="CompoundingOvernightIndexedCouponPricer.EffectiveIndexFixing"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OvernightIndexedCouponPricerCxxVectorTests
{
    [Fact]
    public void OvernightIndexedCouponPricerCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/overnightindexedcouponpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            OvernightIndexedCouponPricer pricer = OvernightInputs.PricerOf(c.Input("pricer"));
            JsonElement spec = c.Input("coupon");
            FloatingRateCoupon coupon = spec.TryGetProperty("underlying", out _)
                ? OvernightInputs.CappedOf(spec)
                : OvernightInputs.CouponOf(spec);
            double[] strikes = c.Doubles("strikes");

            Func<object?>[] Each(Func<double, bool, object?> f) =>
                [.. new[] { false, true }.SelectMany(daily => strikes.Select(k => (Func<object?>)(() => f(k, daily))))];

            check.Output(
                c,
                "averageRate",
                c.DateInputs("dates").Select(d => (Func<object?>)(() => pricer.AverageRate(coupon, d, context))).ToArray());
            check.Output(c, "capletRate", Each((k, daily) => pricer.CapletRate(coupon, k, daily, context)));
            check.Output(c, "floorletRate", Each((k, daily) => pricer.FloorletRate(coupon, k, daily, context)));
            check.Output(
                c, "effectiveCapletVolatility", Each((k, daily) => pricer.EffectiveCapletVolatility(coupon, k, daily, context)));
            check.Output(
                c,
                "effectiveFloorletVolatility",
                Each((k, daily) => pricer.EffectiveFloorletVolatility(coupon, k, daily, context)));
            if (pricer is CompoundingOvernightIndexedCouponPricer compounding)
            {
                check.Output(c, "effectiveSpread", () => compounding.EffectiveSpread(coupon, context));
                check.Output(c, "effectiveIndexFixing", () => compounding.EffectiveIndexFixing(coupon, context));
            }
        }

        check.Complete();
    }
}
