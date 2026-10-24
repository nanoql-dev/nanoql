// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The linear TSR pricer's vectors, <c>tests/vectors/cashflows/lineartsrpricer.json</c>: <see cref="LinearTsrPricer"/>
/// on the rate-bound and vega-ratio strategies, default and explicit bounds, three mean reversions and a coupon
/// discounting curve of its own, on a lognormal, a shifted and a normal at-the-money matrix and the interpolated cube;
/// per coupon, the outputs of <see cref="ConundrumPricerVectorTests"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LinearTsrPricerVectorTests
{
    [Fact]
    public void LinearTsrPricerVector()
    {
        var file = VectorFile.Load("cashflows/lineartsrpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement volSpec = c.Input("volatility");
            SwaptionVolatilityStructure vol = volSpec.TryGetProperty("atm", out _)
                ? CubeInputs.CubeOf(volSpec, context)
                : CubeInputs.AtmOf(volSpec, context);
            LinearTsrPricer pricer = PricerOf(c.Input("pricer"), vol);
            double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
            double cap = c.Double("cap"), floor = c.Double("floor");

            var outputs = new Dictionary<string, List<Func<object?>>>
            {
                ["rate"] = [], ["swapletPrice"] = [], ["capletRate"] = [], ["capletPrice"] = [], ["floorletRate"] = [],
                ["floorletPrice"] = [], ["collaredRate"] = [],
            };
            foreach (JsonElement spec in c.Input("coupons").EnumerateArray())
            {
                CmsCoupon coupon = ConundrumPricerVectorTests.CouponOf(spec);
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

                CappedFlooredCmsCoupon collar = ConundrumPricerVectorTests.CollarOf(spec, cap, floor).SetPricer(pricer);
                outputs["collaredRate"].Add(() => collar.Rate(context));
            }

            foreach ((string name, List<Func<object?>> all) in outputs)
            {
                check.Output(c, name, all.ToArray());
            }
        }

        check.Complete();
    }

    // The pricer a spec describes: {"meanReversion", "settings", "couponDiscountCurve"}, on the volatility.
    private static LinearTsrPricer PricerOf(JsonElement spec, SwaptionVolatilityStructure vol)
    {
        JsonElement s = spec.GetProperty("settings");
        double lower = s.GetProperty("lowerRateBound").GetDouble(), upper = s.GetProperty("upperRateBound").GetDouble();
        bool defaults = s.GetProperty("defaultBounds").GetBoolean();
        var settings = new LinearTsrPricerSettings();
        if (s.GetProperty("strategy").GetString() == "RateBound")
        {
            settings = defaults ? settings : settings.WithRateBound(lower, upper);
        }
        else
        {
            double vegaRatio = s.GetProperty("vegaRatio").GetDouble();
            settings = defaults ? settings.WithVegaRatio(vegaRatio) : settings.WithVegaRatio(vegaRatio, lower, upper);
        }

        JsonElement curve = spec.GetProperty("couponDiscountCurve");
        return new LinearTsrPricer(
            vol,
            spec.GetProperty("meanReversion").GetDouble(),
            curve.ValueKind == JsonValueKind.Null ? null : IndexInputs.CurveOf(curve),
            settings);
    }
}
