// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The BMA swap vectors, <c>tests/vectors/instruments/bmaswap.json</c>: <see cref="BMASwap"/>'s inspectors, the
/// <see cref="DiscountingSwapEngine"/> results with the inspectors <see cref="BMASwapResults"/> derives, and both legs'
/// coupons, the average-BMA coupons' refusals of a single fixing among them; and the refusal of a type outside
/// <see cref="SwapType"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BMASwapVectorTests
{
    [Fact]
    public void BMASwapVector()
    {
        var file = VectorFile.Load("instruments/bmaswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("swap");
            BMASwap? swap = null;
            check.Output(c, "built", () =>
            {
                swap = SwapOf(spec, context);
                return true;
            });
            if (swap is null)
            {
                continue;
            }

            check.Output(c, "liborFraction", swap.LiborFraction);
            check.Output(c, "liborSpread", swap.LiborSpread);
            check.Output(c, "nominal", swap.Nominal);
            check.Output(c, "type", swap.Type);

            BMASwapResults r = DiscountingSwapEngine.Calculate(
                swap, context, IndexInputs.CurveOf(c.Input("discountCurve"))!);
            check.Output(c, "NPV", r.NPV);
            check.Output(c, "legNPV", r.LegNPV);
            check.Output(c, "legBPS", r.LegBPS);
            check.Output(c, "liborLegNPV", r.LiborLegNPV);
            check.Output(c, "liborLegBPS", r.LiborLegBPS);
            check.Output(c, "bmaLegNPV", r.BmaLegNPV);
            check.Output(c, "bmaLegBPS", r.BmaLegBPS);
            check.Output(c, "fairLiborFraction", r.FairLiborFraction);
            check.Output(c, "fairLiborSpread", r.FairLiborSpread);

            ImmutableArray<CashFlow> libor = swap.LiborLeg;
            check.Output(c, "liborDate", libor.Select(cf => (object)cf.Date));
            check.Output(c, "liborRate", CouponInputs.Each(libor, cf => ((Coupon)cf).Rate(context)));
            check.Output(c, "liborAmount", CouponInputs.Each(libor, cf => cf.Amount(context)));

            AverageBMACoupon[] bma = [.. swap.BmaLeg.Cast<AverageBMACoupon>()];
            AverageBMACoupon first = bma[0];
            check.Output(c, "bmaDate", bma.Select(x => (object)x.Date));
            check.Output(c, "bmaAccrualStartDate", bma.Select(x => (object)x.AccrualStartDate));
            check.Output(c, "bmaAccrualEndDate", bma.Select(x => (object)x.AccrualEndDate));
            check.Output(c, "bmaNominal", first.Nominal(context));
            check.Output(c, "bmaFixingDays", first.FixingDays);
            check.Output(c, "bmaGearing", first.Gearing);
            check.Output(c, "bmaSpread", first.Spread);
            check.Output(c, "bmaRate", CouponInputs.Each(bma, x => x.Rate(context)));
            check.Output(c, "bmaAmount", CouponInputs.Each(bma, x => x.Amount(context)));
            check.Output(c, "bmaFixingDate", () => first.FixingDate());
            check.Output(c, "bmaIndexFixing", () => first.IndexFixing(context));
            check.Output(c, "bmaConvexityAdjustment", () => first.ConvexityAdjustment(context));
        }

        check.Complete();
    }

    private static BMASwap SwapOf(JsonElement spec, EvaluationContext context) =>
        new(
            (SwapType)spec.GetProperty("type").GetInt32(),
            spec.GetProperty("nominal").GetDouble(),
            SwapInputs.ScheduleOf(spec.GetProperty("liborSchedule"), context),
            spec.GetProperty("liborFraction").GetDouble(),
            spec.GetProperty("liborSpread").GetDouble(),
            SwapInputs.IndexOf(spec.GetProperty("liborIndex")),
            CouponInputs.DayCounterOrNull(spec.GetProperty("liborDayCount")),
            SwapInputs.ScheduleOf(spec.GetProperty("bmaSchedule"), context),
            new BMAIndex(IndexInputs.CurveOf(spec.GetProperty("bmaCurve"))),
            CouponInputs.DayCounterOrNull(spec.GetProperty("bmaDayCount")),
            context);
}
