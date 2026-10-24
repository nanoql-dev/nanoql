// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CMS pricers' harness vectors, <c>tests/vectors/cxx/cashflows/conundrumpricer.json</c>: the pricers' mean
/// reversion (<see cref="IMeanRevertingPricer.MeanReversion"/>), <see cref="NumericHaganPricer.IntegrationLimits"/>,
/// <see cref="MarketQuotedOptionPricer"/>'s price and its refusal of a shifted volatility, and
/// <see cref="GFunctionFactory"/>'s g-functions with their derivatives.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConundrumPricerCxxVectorTests
{
    [Fact]
    public void ConundrumPricerCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/conundrumpricer");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("meanReversion", out _))
            {
                check.Output(c, "meanReversion", () => ((IMeanRevertingPricer)PricerOf(c.Input("pricer"), context)).MeanReversion);
            }
            else if (c.Expected.TryGetProperty("limits", out _))
            {
                var pricer = (NumericHaganPricer)PricerOf(c.Input("pricer"), context);
                double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
                var rows = new List<Func<object?>>();
                foreach (JsonElement spec in c.Input("coupons").EnumerateArray())
                {
                    CmsCoupon coupon = ConundrumPricerVectorTests.CouponOf(spec);
                    foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                    {
                        // the limits don't depend on the strike that prices the optionlet
                        rows.AddRange(strikes.Select(_ => (Func<object?>)(() =>
                        {
                            NumericHaganPricerIntegrationLimits l = pricer.IntegrationLimits(coupon, type, context);
                            return new object?[] { l.LowerLimit, l.UpperLimit, l.StdDeviations };
                        })));
                    }
                }

                check.Output(c, "limits", rows.ToArray());
            }
            else if (c.Expected.TryGetProperty("prices", out _))
            {
                SwaptionVolatilityStructure vol = CubeInputs.AtmOf(c.Input("volatility"), context);
                double forward = c.Double("forward"), deflator = c.Double("deflator");
                Period tenor = VolatilityInputs.TenorOf(c.Input("swapTenor"));
                double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
                var rows = new List<Func<object?>>();
                foreach (JsonElement e in c.Input("expiries").EnumerateArray())
                {
                    var expiry = new Date(e.GetInt32());
                    foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                    {
                        rows.AddRange(strikes.Select(k => (Func<object?>)(() =>
                            new MarketQuotedOptionPricer(forward, expiry, tenor, vol).Evaluate(k, type, deflator))));
                    }
                }

                check.Output(c, "prices", rows.ToArray());
            }
            else
            {
                double[] xs = VolatilityInputs.Reals(c.Input("xs"));
                IEnumerable<Func<GFunction>> functions = c.Name switch
                {
                    "newGFunctionStandard" => c.Input("specs").EnumerateArray().Select(s => (Func<GFunction>)(() =>
                        GFunctionFactory.NewGFunctionStandard(s[0].GetInt32(), s[1].GetDouble(), s[2].GetInt32()))),
                    "newGFunctionExactYield" => c.Input("coupons").EnumerateArray().Select(s => (Func<GFunction>)(() =>
                        GFunctionFactory.NewGFunctionExactYield(ConundrumPricerVectorTests.CouponOf(s), context))),
                    _ => c.Input("coupons").EnumerateArray().Select(s => (Func<GFunction>)(() =>
                        GFunctionFactory.NewGFunctionWithShifts(
                            ConundrumPricerVectorTests.CouponOf(s), c.Double("meanReversion"), context))),
                };
                Func<object?>[] values = [.. functions.Select(f => (Func<object?>)(() =>
                {
                    GFunction g = f();
                    return xs.Select(x => new object[] { g.Evaluate(x), g.FirstDerivative(x), g.SecondDerivative(x) })
                        .ToArray();
                }))];
                check.Output(c, "values", values);
            }
        }

        check.Complete();
    }

    // A harness pricer spec: {"type", "model", "meanReversion", "volatility", "lowerLimit", "upperLimit", "precision"}.
    internal static CmsCouponPricer PricerOf(JsonElement spec, EvaluationContext context)
    {
        SwaptionVolatilityStructure vol = CubeInputs.AtmOf(spec.GetProperty("volatility"), context);
        double m = spec.GetProperty("meanReversion").GetDouble();
        string type = spec.GetProperty("type").GetString()!;
        if (type == "LinearTsrPricer")
        {
            return new LinearTsrPricer(vol, m);
        }

        var model = (GFunctionFactoryYieldCurveModel)spec.GetProperty("model").GetInt32();
        return type == "NumericHaganPricer"
            ? new NumericHaganPricer(
                vol,
                model,
                m,
                spec.GetProperty("lowerLimit").GetDouble(),
                spec.GetProperty("upperLimit").GetDouble(),
                spec.GetProperty("precision").GetDouble())
            : new AnalyticHaganPricer(vol, model, m);
    }
}
