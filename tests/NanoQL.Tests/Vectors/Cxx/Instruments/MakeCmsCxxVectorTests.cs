// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
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
/// <see cref="MakeCms"/>'s harness vectors, <c>tests/vectors/cxx/instruments/makecms.json</c>: the swaps its setters
/// build, each leg's cash flows, and with a pricer the legs' NPVs and the NPV on the engine QuantLib's builder
/// attaches; the at-the-money spread, on the index's curve and on a discounting curve, and its refusal without a
/// pricer.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeCmsCxxVectorTests
{
    [Fact]
    public void MakeCmsCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/makecms");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            SwapIndex swapIndex = SwaptionInputs.SwapIndexOf(c.Input("swapIndex"));
            JsonElement ibor = c.Input("iborIndex");
            var builder = new MakeCms(
                VolatilityInputs.TenorOf(c.Input("swapTenor")),
                swapIndex,
                ibor.ValueKind == JsonValueKind.Null ? null : IndexInputs.IborIndexOf(ibor));
            JsonElement pricerSpec = c.Input("pricer");
            CmsCouponPricer? pricer = pricerSpec.ValueKind == JsonValueKind.Null
                ? null
                : ConundrumPricerCxxVectorTests.PricerOf(pricerSpec, context);
            if (pricer is not null)
            {
                builder = builder.WithCmsCouponPricer(pricer);
            }

            YieldTermStructure engineCurve = swapIndex.ForwardingTermStructure!;
            foreach (JsonElement setter in c.Input("setters").EnumerateArray())
            {
                (builder, YieldTermStructure? curve) = Apply(builder, setter[0].GetString()!, setter[1]);
                engineCurve = curve ?? engineCurve;
            }

            var swap = new Lazy<Swap>(() => builder.ToSwap(context));
            check.Output(c, "legs", () => swap.Value.Legs.Select(Leg).ToArray());
            if (pricer is null)
            {
                // no NPV without a pricer; where QuantLib refuses the build, the NPV is the refusal too
                check.Output(c, "npv", c.Expected.GetProperty("npv").ValueKind == JsonValueKind.Null
                    ? null
                    : () => swap.Value);
            }
            else
            {
                check.Output(c, "npv", () =>
                {
                    SwapResults r = DiscountingSwapEngine.Calculate(swap.Value, context, engineCurve);
                    return new object?[] { r.LegNPV[0], r.LegNPV[1], r.NPV };
                });
            }
        }

        check.Complete();
    }

    private static object?[][] Leg(ImmutableArray<CashFlow> leg) =>
        [.. leg.Select(cf => new object?[]
        {
            cf.Date,
            (cf as Coupon)?.AccrualStartDate,
            (cf as Coupon)?.AccrualEndDate,
            (cf as Coupon)?.ContractNominal,
            cf is CmsCoupon,
            (cf as FloatingRateCoupon)?.Spread,
            (cf as FloatingRateCoupon)?.FixingDate(),
        })];

    // One setter, and the engine's curve where it sets one.
    private static (MakeCms Builder, YieldTermStructure? Curve) Apply(MakeCms b, string name, JsonElement x) => name switch
    {
        "receiveCms" => (b.ReceiveCms(x.GetBoolean()), null),
        "withAtmSpread" => (b.WithAtmSpread(x.GetBoolean()), null),
        "withCmsLegEndOfMonth" => (b.WithCmsLegEndOfMonth(x.GetBoolean()), null),
        "withFloatingLegEndOfMonth" => (b.WithFloatingLegEndOfMonth(x.GetBoolean()), null),
        "withNominal" => (b.WithNominal(x.GetDouble()), null),
        "withIborSpread" => (b.WithIborSpread(x.GetDouble()), null),
        "withForwardStart" => (b.WithForwardStart(VolatilityInputs.TenorOf(x)), null),
        "withCmsLegTenor" => (b.WithCmsLegTenor(VolatilityInputs.TenorOf(x)), null),
        "withFloatingLegTenor" => (b.WithFloatingLegTenor(VolatilityInputs.TenorOf(x)), null),
        "withEffectiveDate" => (b.WithEffectiveDate(new Date(x.GetInt32())), null),
        "withCmsLegFirstDate" => (b.WithCmsLegFirstDate(new Date(x.GetInt32())), null),
        "withCmsLegNextToLastDate" => (b.WithCmsLegNextToLastDate(new Date(x.GetInt32())), null),
        "withFloatingLegFirstDate" => (b.WithFloatingLegFirstDate(new Date(x.GetInt32())), null),
        "withFloatingLegNextToLastDate" => (b.WithFloatingLegNextToLastDate(new Date(x.GetInt32())), null),
        "withCmsLegCalendar" => (b.WithCmsLegCalendar(CashFlowInputs.CalendarOf(x)), null),
        "withFloatingLegCalendar" => (b.WithFloatingLegCalendar(CashFlowInputs.CalendarOf(x)), null),
        "withCmsLegDayCount" => (b.WithCmsLegDayCount(CashFlowInputs.DayCounterOf(x)), null),
        "withFloatingLegDayCount" => (b.WithFloatingLegDayCount(CashFlowInputs.DayCounterOf(x)), null),
        "withCmsLegConvention" => (b.WithCmsLegConvention((BusinessDayConvention)x.GetInt32()), null),
        "withCmsLegTerminationDateConvention" =>
            (b.WithCmsLegTerminationDateConvention((BusinessDayConvention)x.GetInt32()), null),
        "withCmsLegRule" => (b.WithCmsLegRule((DateGenerationRule)x.GetInt32()), null),
        "withFloatingLegConvention" => (b.WithFloatingLegConvention((BusinessDayConvention)x.GetInt32()), null),
        "withFloatingLegTerminationDateConvention" =>
            (b.WithFloatingLegTerminationDateConvention((BusinessDayConvention)x.GetInt32()), null),
        "withFloatingLegRule" => (b.WithFloatingLegRule((DateGenerationRule)x.GetInt32()), null),
        "withDiscountingTermStructure" => WithCurve(b, IndexInputs.CurveOf(x)!),
        _ => throw new InvalidOperationException($"unknown setter '{name}'"),
    };

    private static (MakeCms, YieldTermStructure?) WithCurve(MakeCms b, YieldTermStructure curve) =>
        (b.WithDiscountingTermStructure(curve), curve);
}
