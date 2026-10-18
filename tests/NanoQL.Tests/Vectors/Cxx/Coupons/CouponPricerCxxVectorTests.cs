// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's coupon pricer vectors, <c>tests/vectors/cxx/cashflows/couponpricer.json</c>:
/// <see cref="IborCouponPricer.UseIndexedCoupon"/>; the six queries of <see cref="BlackIborCouponPricer"/>, which the
/// wheel binds without the coupon they need; <see cref="CouponPricerFunctions.SetCouponPricer"/>'s default case on
/// floating-rate coupons built directly; and <see cref="CouponPricerFunctions.SetCouponPricers(ImmutableArray{CashFlow},
/// IEnumerable{FloatingRateCouponPricer})"/> with its first-matching forms, which the wheel doesn't wrap.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CouponPricerCxxVectorTests
{
    [Fact]
    public void CouponPricerCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/couponpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("useIndexedCoupon", out _))
            {
                var pricer = (IborCouponPricer)CouponInputs.PricerOf(c.Input("pricer"), context)!;
                check.Output(c, "useIndexedCoupon", pricer.UseIndexedCoupon);
            }
            else if (c.Inputs.TryGetProperty("coupon", out JsonElement couponSpec))
            {
                FloatingRateCouponPricer pricer = CouponInputs.PricerOf(c.Input("pricer"), context)!;
                FloatingRateCoupon coupon = CouponInputs.CouponOf(couponSpec, context);
                double[] strikes = c.Doubles("strikes");
                check.Output(c, "swapletPrice", () => pricer.SwapletPrice(coupon, context));
                check.Output(c, "swapletRate", () => pricer.SwapletRate(coupon, context));
                check.Output(c, "capletPrice", CouponInputs.Each(strikes, x => pricer.CapletPrice(coupon, x, context)));
                check.Output(c, "capletRate", CouponInputs.Each(strikes, x => pricer.CapletRate(coupon, x, context)));
                check.Output(c, "floorletPrice", CouponInputs.Each(strikes, x => pricer.FloorletPrice(coupon, x, context)));
                check.Output(c, "floorletRate", CouponInputs.Each(strikes, x => pricer.FloorletRate(coupon, x, context)));
            }
            else
            {
                ImmutableArray<CashFlow> leg =
                    [.. c.Input("coupons").EnumerateArray().Select(s => CouponInputs.CouponOf(s, context))];
                FloatingRateCouponPricer?[] pricers =
                    [.. c.Input("pricers").EnumerateArray().Select(s => CouponInputs.PricerOf(s, context))];
                check.Output(c, "pricers", () => Positions(Set(c.String("setter"), leg, pricers), pricers));
            }
        }

        check.Complete();
    }

    private static ImmutableArray<CashFlow> Set(string setter, ImmutableArray<CashFlow> leg, FloatingRateCouponPricer?[] p) =>
        setter switch
        {
            "setCouponPricer(Leg,FloatingRateCouponPricer)" => CouponPricerFunctions.SetCouponPricer(leg, p[0]),
            "setCouponPricers(Leg,vector<FloatingRateCouponPricer>)" => CouponPricerFunctions.SetCouponPricers(leg, p),
            "setCouponPricers(Leg,p1,p2)" => CouponPricerFunctions.SetCouponPricers(leg, p[0], p[1]),
            "setCouponPricers(Leg,p1,p2,p3)" => CouponPricerFunctions.SetCouponPricers(leg, p[0], p[1], p[2]),
            "setCouponPricers(Leg,p1,p2,p3,p4)" => CouponPricerFunctions.SetCouponPricers(leg, p[0], p[1], p[2], p[3]),
            _ => throw new InvalidOperationException($"unknown setter '{setter}'"),
        };

    // Per cash flow, the first position of the pricer it holds among the call's, or null for none.
    private static int?[] Positions(ImmutableArray<CashFlow> leg, FloatingRateCouponPricer?[] pricers) =>
        [.. leg.Select(cf => cf is FloatingRateCoupon { Pricer: { } held }
            ? Array.FindIndex(pricers, p => ReferenceEquals(p, held)) is int k and >= 0 ? k : (int?)null
            : null)];
}
