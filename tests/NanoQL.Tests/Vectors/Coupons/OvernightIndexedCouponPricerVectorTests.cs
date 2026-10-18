// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The overnight coupon pricer vectors, <c>tests/vectors/cashflows/overnightindexedcouponpricer.json</c>: coupons
/// priced by <see cref="CompoundingOvernightIndexedCouponPricer"/> and
/// <see cref="ArithmeticAveragedOvernightIndexedCouponPricer"/> (the approximation both ways, mean reversions of 0.03
/// and 0, convexity volatilities of 0 and 0.01), with the pricers' accessors, their throwing members and their
/// modifiers; and <see cref="CouponPricerFunctions.SetCouponPricer"/> on legs of overnight coupons, its refusals among
/// them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OvernightIndexedCouponPricerVectorTests
{
    [Fact]
    public void OvernightIndexedCouponPricerVector()
    {
        var file = VectorFile.Load("cashflows/overnightindexedcouponpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            OvernightIndexedCouponPricer pricer = OvernightInputs.PricerOf(c.Input("pricer"));
            if (c.Inputs.TryGetProperty("coupon", out JsonElement spec))
            {
                CheckCoupon(check, c, spec, pricer, context);
            }
            else
            {
                CheckLeg(check, c, pricer, context);
            }
        }

        check.Complete();
    }

    private static void CheckCoupon(
        VectorAssert check, VectorCase c, JsonElement spec, OvernightIndexedCouponPricer pricer, EvaluationContext context)
    {
        OvernightIndexedCoupon coupon = OvernightInputs.CouponOf(spec).SetPricer(pricer);
        check.Output(c, "rate", () => coupon.Rate(context));
        check.Output(c, "amount", () => coupon.Amount(context));
        check.Output(
            c,
            "accruedAmount",
            c.DateInputs("accrualDates").Select(d => (Func<object?>)(() => coupon.AccruedAmount(d, context))).ToArray());
        if (coupon.AveragingMethod == RateAveragingType.Compound)
        {
            check.Output(c, "effectiveSpread", () => coupon.EffectiveSpread(context));
            check.Output(c, "effectiveIndexFixing", () => coupon.EffectiveIndexFixing(context));
        }

        check.Output(c, "effectiveVolatilityInput", pricer.EffectiveVolatilityInput);
        check.Output(c, "hasCapletVolatility", pricer.CapletVolatility is not null);
        check.Output(c, "swapletPrice", () => pricer.SwapletPrice(coupon, context));
        check.Output(c, "capletPrice", () => pricer.CapletPrice(coupon, 0.03, context));
        check.Output(c, "capletRate", () => pricer.CapletRate(coupon, 0.03, context));
        check.Output(c, "floorletPrice", () => pricer.FloorletPrice(coupon, 0.03, context));
        check.Output(c, "floorletRate", () => pricer.FloorletRate(coupon, 0.03, context));

        if (c.Inputs.TryGetProperty("newVol", out JsonElement newVol))
        {
            OvernightIndexedCouponPricer modified = pricer
                .SetCapletVolatility(OvernightInputs.VolatilityOf(newVol))
                .SetEffectiveVolatilityInput(!pricer.EffectiveVolatilityInput);
            check.Output(c, "setEffectiveVolatilityInput", modified.EffectiveVolatilityInput);
            check.Output(c, "setHasCapletVolatility", modified.CapletVolatility is not null);
            check.Output(c, "setRate", () => coupon.SetPricer(modified).Rate(context));
            check.Output(c, "resetHasCapletVolatility", modified.SetCapletVolatility().CapletVolatility is not null);
        }
    }

    private static void CheckLeg(VectorAssert check, VectorCase c, OvernightIndexedCouponPricer pricer, EvaluationContext context)
    {
        JsonElement[] specs = [.. c.Input("coupons").EnumerateArray()];
        OvernightIndex index = OvernightInputs.IndexOf(specs[0].GetProperty("index"));
        ImmutableArray<CashFlow> leg = [.. specs.Select(s => OvernightInputs.CouponOf(s, index))];
        ImmutableArray<CashFlow> priced = default;
        check.Output(c, "setCouponPricer", () =>
        {
            priced = CouponPricerFunctions.SetCouponPricer(leg, pricer);
            return true;
        });
        if (priced.IsDefault)
        {
            return;
        }

        check.Output(c, "rates", priced.Select(x => (Func<object?>)(() => ((Coupon)x).Rate(context))).ToArray());
    }
}
