// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The multiple-resets swap builder vectors, <c>tests/vectors/instruments/makemultipleresetsswap.json</c>:
/// <see cref="MakeMultipleResetsSwap"/>'s swaps with their setters applied in order, at the fair rate or a given one,
/// their NPV and fair rate on the engine QuantLib's builder sets on the swap, and QuantLib's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeMultipleResetsSwapVectorTests
{
    [Fact]
    public void MakeMultipleResetsSwapVector()
    {
        var file = VectorFile.Load("instruments/makemultipleresetsswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("build");
            IborIndex index = IndexInputs.IborIndexOf(spec.GetProperty("index"));
            (MakeMultipleResetsSwap builder, YieldTermStructure? discount, YieldTermStructure? engine) = BuilderOf(spec, index);
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => builder.ToSwap(context).ResetsPerCoupon);
                continue;
            }

            MultipleResetsSwap swap = builder.ToSwap(context);
            SwapInputs.CheckInspectors(check, c, swap);
            check.Output(c, "fullResetSchedule", swap.FullResetSchedule.Dates);
            check.Output(c, "resetsPerCoupon", swap.ResetsPerCoupon);
            SwapInputs.CheckLeg(check, c, "fixedLeg", swap.FixedLeg, context);
            SwapInputs.CheckLeg(check, c, "floatingLeg", swap.FloatingLeg, context);

            // the engine QuantLib's builder sets: the given one, else the given discounting curve's or the forwarding
            // curve's, without the settlement date's flows
            FixedVsFloatingSwapResults results = engine is not null
                ? DiscountingSwapEngine.Calculate((FixedVsFloatingSwap)swap, context, engine)
                : DiscountingSwapEngine.Calculate(
                    (FixedVsFloatingSwap)swap, context, discount ?? index.ForwardingTermStructure!, includeSettlementDateFlows: false);
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "fairRate", results.FairRate);
        }

        check.Complete();
    }

    // The builder a spec describes, its setters applied in order, and the discounting curve and the engine's curve
    // given to it, if any.
    private static (MakeMultipleResetsSwap Builder, YieldTermStructure? Discount, YieldTermStructure? Engine) BuilderOf(
        JsonElement spec, IborIndex index)
    {
        JsonElement tenor = spec.GetProperty("tenor");
        var b = new MakeMultipleResetsSwap(
            new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32()), index, spec.GetProperty("resetsPerCoupon").GetInt32());
        YieldTermStructure? discount = null, engine = null;
        foreach (JsonElement setter in spec.GetProperty("setters").EnumerateArray())
        {
            JsonElement value = setter[1];
            b = setter[0].GetString()! switch
            {
                "receiveFixed" => b.ReceiveFixed(value.GetBoolean()),
                "swapType" => b.WithType((SwapType)value.GetInt32()),
                "nominal" => b.WithNominal(value.GetDouble()),
                "fixedRate" => b.WithFixedRate(value.GetDouble()),
                "settlementDays" => b.WithSettlementDays(value.GetInt32()),
                "effectiveDate" => b.WithEffectiveDate(new Date(value.GetInt32())),
                "terminationDate" => b.WithTerminationDate(new Date(value.GetInt32())),
                "forwardStart" => b.WithForwardStart(SwapInputs.PeriodOf(value)),
                "fixedLegFrequency" => b.WithFixedLegFrequency((Frequency)value.GetInt32()),
                "fixedLegDayCount" => b.WithFixedLegDayCount(CashFlowInputs.DayCounterOf(value)),
                "fixedLegConvention" => b.WithFixedLegConvention((BusinessDayConvention)value.GetInt32()),
                "floatingLegSpread" => b.WithFloatingLegSpread(value.GetDouble()),
                "averagingMethod" => b.WithAveragingMethod((RateAveragingType)value.GetInt32()),
                "discountingTermStructure" => b.WithDiscountingTermStructure(discount = IndexInputs.CurveOf(value)),
                "pricingEngine" => b.WithPricingEngine(EngineOn(engine = IndexInputs.CurveOf(value)!)),
                string name => throw new InvalidOperationException($"unknown setter '{name}'"),
            };
        }

        return (b, discount, engine);
    }

    private static Func<FixedVsFloatingSwap, EvaluationContext, FixedVsFloatingSwapResults> EngineOn(YieldTermStructure curve) =>
        (swap, context) => DiscountingSwapEngine.Calculate(swap, context, curve);
}
