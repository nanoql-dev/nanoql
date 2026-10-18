// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Black overnight pricer vectors, <c>tests/vectors/cashflows/blackovernightindexedcouponpricer.json</c>: capped,
/// floored and collared overnight coupons priced by <see cref="BlackCompoundingOvernightIndexedCouponPricer"/> and
/// <see cref="BlackAveragingOvernightIndexedCouponPricer"/>, on the period rate and on the daily rates, with their
/// effective caps, floors and volatilities; <see cref="CappedFlooredOvernightIndexedCoupon"/>'s cap-and-floor check at
/// a negative gearing; and <see cref="CouponPricerFunctions.SetCouponPricer"/> on legs of capped coupons, which sets the
/// coupons and their underlyings.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackOvernightIndexedCouponPricerVectorTests
{
    [Fact]
    public void BlackOvernightIndexedCouponPricerVector()
    {
        var file = VectorFile.Load("cashflows/blackovernightindexedcouponpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            OvernightIndexedCouponPricer pricer = OvernightInputs.PricerOf(c.Input("pricer"));
            if (c.Inputs.TryGetProperty("capped", out JsonElement spec))
            {
                CheckCapped(check, c, spec, pricer, context);
            }
            else
            {
                CheckLeg(check, c, pricer, context);
            }
        }

        check.Complete();
    }

    private static void CheckCapped(
        VectorAssert check, VectorCase c, JsonElement spec, OvernightIndexedCouponPricer pricer, EvaluationContext context)
    {
        CappedFlooredOvernightIndexedCoupon? built = null;
        check.Output(c, "built", () =>
        {
            built = OvernightInputs.CappedOf(spec);
            return true;
        });
        if (built is null)
        {
            return;
        }

        CappedFlooredOvernightIndexedCoupon coupon = built.SetPricer(pricer);
        check.Output(c, "rate", () => coupon.Rate(context));
        check.Output(c, "amount", () => coupon.Amount(context));
        check.Output(
            c,
            "accruedAmount",
            c.DateInputs("accrualDates").Select(d => (Func<object?>)(() => coupon.AccruedAmount(d, context))).ToArray());
        YieldTermStructure discount = CashFlowInputs.CurveOf(c.Input("discountCurve"));
        check.Output(c, "price", () => coupon.Price(discount, context));
        check.Output(c, "cap", coupon.Cap);
        check.Output(c, "floor", coupon.Floor);
        check.Output(c, "effectiveCap", () => coupon.EffectiveCap(context));
        check.Output(c, "effectiveFloor", () => coupon.EffectiveFloor(context));
        check.Output(c, "effectiveCapletVolatility", () => coupon.EffectiveCapletVolatility(context));
        check.Output(c, "effectiveFloorletVolatility", () => coupon.EffectiveFloorletVolatility(context));
        check.Output(c, "underlyingRate", () => coupon.Underlying.Rate(context));
        check.Output(c, "isCapped", coupon.IsCapped);
        check.Output(c, "isFloored", coupon.IsFloored);
        check.Output(c, "nakedOption", coupon.NakedOption);
        check.Output(c, "dailyCapFloor", coupon.DailyCapFloor);
        check.Output(c, "compoundSpreadDaily", coupon.CompoundSpreadDaily);
        check.Output(c, "averagingMethod", coupon.AveragingMethod);
        check.Output(c, "fixingDate", coupon.FixingDate());
        check.Output(c, "nominal", coupon.Nominal(context));
        check.Output(c, "convexityAdjustment", () => coupon.ConvexityAdjustment(context));

        // The pricer's own effective volatilities, which QuantLib keeps after the rate: the generator writes them
        // where the rate didn't throw.
        if (c.Expected.TryGetProperty("pricerEffectiveCapletVolatility", out _))
        {
            check.Output(
                c,
                "pricerEffectiveCapletVolatility",
                coupon.EffectiveCap(context) is double cap
                    ? pricer.EffectiveCapletVolatility(coupon, cap, coupon.DailyCapFloor, context)
                    : null);
            check.Output(
                c,
                "pricerEffectiveFloorletVolatility",
                coupon.EffectiveFloor(context) is double floor
                    ? pricer.EffectiveFloorletVolatility(coupon, floor, coupon.DailyCapFloor, context)
                    : null);
            check.Output(c, "pricerCapletRate", () => pricer.CapletRate(coupon, 0.04, context));
            check.Output(c, "pricerFloorletRate", () => pricer.FloorletRate(coupon, 0.04, context));
        }
    }

    private static void CheckLeg(VectorAssert check, VectorCase c, OvernightIndexedCouponPricer pricer, EvaluationContext context)
    {
        ImmutableArray<CashFlow> leg = [.. c.Input("coupons").EnumerateArray().Select(OvernightInputs.CappedOf)];
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

        CappedFlooredOvernightIndexedCoupon[] coupons = priced.Cast<CappedFlooredOvernightIndexedCoupon>().ToArray();
        check.Output(c, "caps", coupons.Select(x => x.Cap).ToArray());
        check.Output(c, "floors", coupons.Select(x => x.Floor).ToArray());
        check.Output(c, "rates", coupons.Select(x => (Func<object?>)(() => x.Rate(context))).ToArray());
        check.Output(c, "underlyingRates", coupons.Select(x => (Func<object?>)(() => x.Underlying.Rate(context))).ToArray());
    }
}
