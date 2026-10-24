// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Hagan CMS pricers' vectors, <c>tests/vectors/cashflows/conundrumpricer.json</c>:
/// <see cref="NumericHaganPricer"/> on each yield-curve model and <see cref="AnalyticHaganPricer"/> on the standard and
/// exact-yield models, on a lognormal and a normal at-the-money matrix and a SABR cube, and a shifted matrix both
/// refuse; per coupon, the rate, the swaplet's price, the caplet's and floorlet's rates and prices at each strike, and
/// a collared coupon's rate, the SABR cube's outputs under the stability filter.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConundrumPricerVectorTests
{
    [Fact]
    public void ConundrumPricerVector()
    {
        var file = VectorFile.Load("cashflows/conundrumpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement volSpec = c.Input("volatility");
            SwaptionVolatilityStructure vol = volSpec.TryGetProperty("atm", out _)
                ? CubeInputs.CubeOf(volSpec, context)
                : CubeInputs.AtmOf(volSpec, context);
            CmsCouponPricer pricer = PricerOf(c.Input("pricer"), vol);
            double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
            double cap = c.Double("cap"), floor = c.Double("floor");

            var outputs = new Dictionary<string, List<Func<object?>>>
            {
                ["rate"] = [], ["swapletPrice"] = [], ["capletRate"] = [], ["capletPrice"] = [], ["floorletRate"] = [],
                ["floorletPrice"] = [], ["collaredRate"] = [],
            };
            foreach (JsonElement spec in c.Input("coupons").EnumerateArray())
            {
                CmsCoupon coupon = CouponOf(spec);
                FloatingRateCoupon priced = coupon.SetPricer(pricer);
                outputs["rate"].Add(() => priced.Rate(context));
                outputs["swapletPrice"].Add(() => pricer.SwapletPrice(coupon, context));
                foreach (double k in strikes)
                {
                    outputs["capletRate"].Add(() => pricer.CapletRate(coupon, k, context));
                    outputs["capletPrice"].Add(() => pricer.CapletPrice(coupon, k, context));
                    outputs["floorletRate"].Add(() => pricer.FloorletRate(coupon, k, context));
                    outputs["floorletPrice"].Add(() => pricer.FloorletPrice(coupon, k, context));
                }

                CappedFlooredCmsCoupon collar = CollarOf(spec, cap, floor).SetPricer(pricer);
                outputs["collaredRate"].Add(() => collar.Rate(context));
            }

            foreach ((string name, List<Func<object?>> all) in outputs)
            {
                check.Output(c, name, CubeInputs.Kept(c, name, all));
            }
        }

        check.Complete();
    }

    // The pricer a spec describes: {"type": "numeric" or "analytic", "model", "meanReversion"}, on the volatility.
    internal static CmsCouponPricer PricerOf(JsonElement spec, SwaptionVolatilityStructure vol)
    {
        GFunctionFactoryYieldCurveModel model = Enum.Parse<GFunctionFactoryYieldCurveModel>(spec.GetProperty("model").GetString()!);
        double meanReversion = spec.GetProperty("meanReversion").GetDouble();
        return spec.GetProperty("type").GetString() == "numeric"
            ? new NumericHaganPricer(vol, model, meanReversion)
            : new AnalyticHaganPricer(vol, model, meanReversion);
    }

    // The coupon a spec describes: {"payment", "start", "end", "fixingDays", "index", "gearing", "spread",
    // "dayCounter", "inArrears"}, on a nominal of 1, its reference period its accrual period.
    internal static CmsCoupon CouponOf(JsonElement spec)
    {
        (Date payment, Date start, Date end, SwapIndex index) = Terms(spec);
        return new CmsCoupon(
            payment,
            1.0,
            start,
            end,
            spec.GetProperty("fixingDays").GetInt32(),
            index,
            spec.GetProperty("gearing").GetDouble(),
            spec.GetProperty("spread").GetDouble(),
            start,
            end,
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            spec.GetProperty("inArrears").GetBoolean());
    }

    // The collared coupon on a spec's terms.
    internal static CappedFlooredCmsCoupon CollarOf(JsonElement spec, double? cap, double? floor)
    {
        (Date payment, Date start, Date end, SwapIndex index) = Terms(spec);
        return new CappedFlooredCmsCoupon(
            payment,
            1.0,
            start,
            end,
            spec.GetProperty("fixingDays").GetInt32(),
            index,
            spec.GetProperty("gearing").GetDouble(),
            spec.GetProperty("spread").GetDouble(),
            cap,
            floor,
            start,
            end,
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            spec.GetProperty("inArrears").GetBoolean());
    }

    private static (Date Payment, Date Start, Date End, SwapIndex Index) Terms(JsonElement spec) => (
        new Date(spec.GetProperty("payment").GetInt32()),
        new Date(spec.GetProperty("start").GetInt32()),
        new Date(spec.GetProperty("end").GetInt32()),
        SwaptionInputs.SwapIndexOf(spec.GetProperty("index")));
}
