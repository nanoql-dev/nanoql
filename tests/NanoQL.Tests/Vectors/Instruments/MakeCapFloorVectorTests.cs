// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="MakeCapFloor"/>'s vectors, <c>tests/vectors/instruments/makecapfloor.json</c>: caps and floors built
/// struck or at the money, with every member, both at-par settings and the builds QuantLib refuses. Each build is the
/// three-argument record followed by <see cref="MakeCapFloor.WithStrike"/> and
/// <see cref="MakeCapFloor.WithForwardStart"/>, QuantLib's definition of the wheel's deprecated five-argument
/// constructor, then the case's setters in order; an engine's curve is given to
/// <see cref="MakeCapFloor.WithDiscountingTermStructure"/>, and the engine itself to
/// <see cref="MakeCapFloor.WithPricingEngine"/>, which changes nothing.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeCapFloorVectorTests
{
    [Fact]
    public void MakeCapFloorVector()
    {
        var file = VectorFile.Load("instruments/makecapfloor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("build");
            CapFloor? cap = null;
            check.Output(c, "built", () =>
            {
                cap = BuilderOf(spec).ToCapFloor(context);
                return true;
            });
            if (cap is null)
            {
                continue;
            }

            YieldTermStructure discount = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            check.Output(c, "type", cap.Type);
            check.Output(c, "capRates", cap.CapRates);
            check.Output(c, "floorRates", cap.FloorRates);
            check.Output(c, "startDate", () => cap.StartDate());
            check.Output(c, "maturityDate", () => cap.MaturityDate());
            check.Output(c, "blackNPV", () => BlackCapFloorEngine.Calculate(cap, context, discount, 0.2).NPV);
            CouponInputs.CheckLeg(check, c, cap.FloatingLeg, context);
        }

        check.Complete();
    }

    private static MakeCapFloor BuilderOf(JsonElement spec)
    {
        JsonElement strike = spec.GetProperty("strike");
        MakeCapFloor builder = new MakeCapFloor(
                (CapFloorType)spec.GetProperty("type").GetInt32(),
                SwapInputs.PeriodOf(spec.GetProperty("tenor")),
                IndexInputs.IborIndexOf(spec.GetProperty("index")))
            .WithStrike(strike.ValueKind == JsonValueKind.Null ? null : strike.GetDouble())
            .WithForwardStart(SwapInputs.PeriodOf(spec.GetProperty("forwardStart")));
        foreach (JsonElement setter in spec.GetProperty("setters").EnumerateArray())
        {
            JsonElement value = setter[1];
            builder = setter[0].GetString() switch
            {
                "nominal" => builder.WithNominal(value.GetDouble()),
                "effectiveDate" => builder.WithEffectiveDate(new Date(value[0].GetInt32()), value[1].GetBoolean()),
                "tenor" => builder.WithTenor(SwapInputs.PeriodOf(value)),
                "calendar" => builder.WithCalendar(CashFlowInputs.CalendarOf(value)),
                "convention" => builder.WithConvention((BusinessDayConvention)value.GetInt32()),
                "terminationDateConvention" => builder.WithTerminationDateConvention((BusinessDayConvention)value.GetInt32()),
                "rule" => builder.WithRule((DateGenerationRule)value.GetInt32()),
                "endOfMonth" => builder.WithEndOfMonth(value.GetBoolean()),
                "firstDate" => builder.WithFirstDate(new Date(value.GetInt32())),
                "nextToLastDate" => builder.WithNextToLastDate(new Date(value.GetInt32())),
                "dayCount" => builder.WithDayCount(CashFlowInputs.DayCounterOf(value)),
                "asOptionlet" => builder.AsOptionlet(value.GetBoolean()),
                var name => throw new InvalidOperationException($"unknown setter {name}"),
            };
        }

        JsonElement engine = spec.GetProperty("engine");
        if (engine.ValueKind != JsonValueKind.Null)
        {
            YieldTermStructure curve = CashFlowInputs.CurveOf(engine.GetProperty("curve"));
            double vol = engine.GetProperty("vol").GetDouble();
            builder = builder.WithDiscountingTermStructure(curve).WithPricingEngine(
                engine.GetProperty("type").GetString() == "BlackCapFloorEngine"
                    ? (c, ctx) => BlackCapFloorEngine.Calculate(c, ctx, curve, vol)
                    : (c, ctx) => BachelierCapFloorEngine.Calculate(c, ctx, curve, vol));
        }

        return builder;
    }
}
