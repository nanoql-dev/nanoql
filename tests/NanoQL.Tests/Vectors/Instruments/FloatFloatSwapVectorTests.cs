// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The float-float swap vectors, <c>tests/vectors/instruments/floatfloatswap.json</c>, through the per-coupon
/// constructor: Ibor and CMS legs, spreads, gearings, capital exchanges, caps, an expired swap and the constructor's
/// refusals; each swap's legs' payment dates, floating-rate coupons and amounts, and
/// <see cref="DiscountingSwapEngine"/>'s results on a flat curve with the fair spreads.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FloatFloatSwapVectorTests
{
    [Fact]
    public void FloatFloatSwapVector()
    {
        var file = VectorFile.Load("instruments/floatfloatswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("swap");
            FloatFloatSwap Build() => FloatFloatInputs.SwapOf(spec, context);

            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => Build());
                continue;
            }

            FloatFloatSwap swap = Build();
            CheckLeg(check, c, "leg1", swap.Leg1, context);
            CheckLeg(check, c, "leg2", swap.Leg2, context);

            var discount = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            FloatFloatSwapResults Price() => DiscountingSwapEngine.Calculate(swap, context, discount);
            if (c.Expected.TryGetProperty("calculate", out _))
            {
                check.Output(c, "calculate", () => Price());
                continue;
            }

            FloatFloatSwapResults results = Price();
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "legNPV", results.LegNPV);
            check.Output(c, "legBPS", results.LegBPS);
            check.Output(c, "fairSpread1", results.FairSpread1);
            check.Output(c, "fairSpread2", results.FairSpread2);
        }

        check.Complete();
    }

    // A leg's payment dates, its floating-rate coupons and its amounts, each amount refused where the cash flow refuses
    // it.
    private static void CheckLeg(
        VectorAssert check, VectorCase c, string prefix, ImmutableArray<CashFlow> leg, EvaluationContext context)
    {
        check.Output(c, prefix + "Dates", leg.Select(cf => cf.Date).ToArray());
        check.Output(c, prefix + "Floating", leg.Select(cf => cf is FloatingRateCoupon).ToArray());
        check.Output(c, prefix + "Amounts", leg.Select(cf => (Func<object?>)(() => cf.Amount(context))).ToArray());
    }
}
