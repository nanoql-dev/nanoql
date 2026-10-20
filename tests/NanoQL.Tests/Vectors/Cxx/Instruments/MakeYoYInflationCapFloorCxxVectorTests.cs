// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's year-on-year cap and floor builder vectors,
/// <c>tests/vectors/cxx/instruments/makeyoyinflationcapfloor.json</c>: <see cref="MakeYoYInflationCapFloor"/> with
/// each setter, struck explicitly and at the money, and its refusals; and
/// <see cref="InflationCapFloorFunctions.ToString(YoYInflationCapFloorType)"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeYoYInflationCapFloorCxxVectorTests
{
    [Fact]
    public void MakeYoYInflationCapFloorCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/makeyoyinflationcapfloor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.String("kind") == "typeText")
            {
                check.Output(c, "typeText", () => InflationCapFloorFunctions.ToString((YoYInflationCapFloorType)c.Int("type")));
                continue;
            }

            var index = (YoYInflationIndex)InflationInputs.IndexOf(c.Input("index"));
            JsonElement spec = c.Input("builder");
            if (c.Expected.TryGetProperty("build", out _))
            {
                check.Output(c, "build", () => BuilderOf(spec, index).ToYoYInflationCapFloor(context) is not null);
                continue;
            }

            YoYInflationCapFloor capFloor = BuilderOf(spec, index).ToYoYInflationCapFloor(context);
            YoYInflationCoupon[] coupons = [.. capFloor.YoYLeg.Cast<YoYInflationCoupon>()];
            check.Output(c, "type", capFloor.Type);
            check.Output(c, "capRates", capFloor.CapRates);
            check.Output(c, "floorRates", capFloor.FloorRates);
            check.Output(c, "startDate", capFloor.StartDate());
            check.Output(c, "maturityDate", capFloor.MaturityDate());
            check.Output(c, "couponAccrualStartDate", coupons.Select(x => x.AccrualStartDate));
            check.Output(c, "couponAccrualEndDate", coupons.Select(x => x.AccrualEndDate));
            check.Output(c, "couponDate", coupons.Select(x => x.Date));
            check.Output(c, "couponNominal", coupons.Select(x => x.Nominal(context)));
            check.Output(c, "couponFixingDate", coupons.Select(x => x.FixingDate()));
            check.Output(c, "couponFixingDays", coupons.Select(x => x.FixingDays));
            check.Output(c, "couponDayCounter", coupons.Select(x => x.DayCounter.Name));
        }

        check.Complete();
    }

    // The builder a spec describes, its setters called in the harness's order, each where its argument isn't null.
    private static MakeYoYInflationCapFloor BuilderOf(JsonElement spec, YoYInflationIndex index)
    {
        var b = new MakeYoYInflationCapFloor(
            (YoYInflationCapFloorType)spec.GetProperty("type").GetInt32(),
            index,
            spec.GetProperty("length").GetInt32(),
            CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
            InflationCashFlowInputs.PeriodOf(spec.GetProperty("observationLag")),
            (CPIInterpolationType)spec.GetProperty("interpolation").GetInt32());
        if (Has(spec, "nominal", out JsonElement x))
        {
            b = b.WithNominal(x.GetDouble());
        }

        if (Has(spec, "effectiveDate", out x))
        {
            b = b.WithEffectiveDate(new Date(x.GetInt32()));
        }

        if (Has(spec, "paymentDayCounter", out x))
        {
            b = b.WithPaymentDayCounter(CashFlowInputs.DayCounterOf(x));
        }

        if (Has(spec, "paymentAdjustment", out x))
        {
            b = b.WithPaymentAdjustment((BusinessDayConvention)x.GetInt32());
        }

        if (Has(spec, "fixingDays", out x))
        {
            b = b.WithFixingDays(x.GetInt32());
        }

        if (Has(spec, "asOptionlet", out x))
        {
            b = b.AsOptionlet(x.GetBoolean());
        }

        if (Has(spec, "strike", out x))
        {
            b = b.WithStrike(x.GetDouble());
        }

        if (Has(spec, "atmStrike", out x))
        {
            b = b.WithAtmStrike(CashFlowInputs.CurveOf(x));
        }

        if (Has(spec, "forwardStart", out x))
        {
            b = b.WithForwardStart(InflationCashFlowInputs.PeriodOf(x));
        }

        return b;
    }

    private static bool Has(JsonElement spec, string name, out JsonElement value)
    {
        value = spec.GetProperty(name);
        return value.ValueKind != JsonValueKind.Null;
    }
}
