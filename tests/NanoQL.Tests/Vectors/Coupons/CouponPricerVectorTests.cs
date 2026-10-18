// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The coupon pricer vectors, <c>tests/vectors/cashflows/couponpricer.json</c>: <see cref="BlackIborCouponPricer"/>
/// under both timing adjustments, three correlations and two volatility types, its volatility given at construction or
/// by <see cref="IborCouponPricer.SetCapletVolatility"/>, through the rates of 20 coupons; and
/// <see cref="CouponPricerFunctions.SetCouponPricer(ImmutableArray{CashFlow}, FloatingRateCouponPricer)"/> on Ibor legs
/// and its refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CouponPricerVectorTests
{
    [Fact]
    public void CouponPricerVector()
    {
        var file = VectorFile.Load("cashflows/couponpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement pricerSpec = c.Input("pricer");
            JsonElement vol = c.Input("capletVolatility");
            if (c.Name.StartsWith("pricer ", StringComparison.Ordinal))
            {
                FloatingRateCoupon[] coupons = c.Input("coupons").EnumerateArray()
                    .Select(s => CouponInputs.CouponOf(s, context).SetPricer(PricerOf(pricerSpec, vol, context)))
                    .ToArray();
                check.Output(c, "rate", CouponInputs.Each(coupons, x => x.Rate(context)));
                check.Output(c, "amount", CouponInputs.Each(coupons, x => x.Amount(context)));
                check.Output(c, "indexFixing", CouponInputs.Each(coupons, x => x.IndexFixing(context)));
                check.Output(c, "adjustedFixing", CouponInputs.Each(coupons, x => x.AdjustedFixing(context)));
                check.Output(c, "convexityAdjustment", CouponInputs.Each(coupons, x => x.ConvexityAdjustment(context)));
                continue;
            }

            ImmutableArray<CashFlow> leg = c.Inputs.TryGetProperty("leg", out JsonElement legSpec)
                ? CouponInputs.LegOf(legSpec, context)
                : [.. c.Input("coupons").EnumerateArray().Select(s => CouponInputs.CouponOf(s, context))];
            FloatingRateCouponPricer? pricer = PricerOf(pricerSpec, vol, context);
            if (c.Expected.TryGetProperty("setCouponPricer", out _))
            {
                check.Output(c, "setCouponPricer", () => CouponPricerFunctions.SetCouponPricer(leg, pricer));
                continue;
            }

            ImmutableArray<CashFlow> set = CouponPricerFunctions.SetCouponPricer(leg, pricer);
            check.Output(c, "rate", CouponInputs.Each(set, x => ((Coupon)x).Rate(context)));
            check.Output(c, "amount", CouponInputs.Each(set, x => x.Amount(context)));
        }

        check.Complete();
    }

    // The case's pricer, with the volatility the generator set afterwards through setCapletVolatility, if any.
    private static FloatingRateCouponPricer? PricerOf(JsonElement spec, JsonElement vol, EvaluationContext context)
    {
        FloatingRateCouponPricer? pricer = CouponInputs.PricerOf(spec, context);
        return vol.ValueKind == JsonValueKind.Null
            ? pricer
            : ((IborCouponPricer)pricer!).SetCapletVolatility(CouponInputs.VolatilityOf(vol));
    }
}
