// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="ConstantCapFloorTermVolatility"/>'s harness vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/capfloor/constantcapfloortermvol.json</c>: structures on fixed and
/// moving reference dates, their inspectors and their volatilities by tenor, by date and by time.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConstantCapFloorTermVolatilityCxxVectorTests
{
    [Fact]
    public void ConstantCapFloorTermVolatilityCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/capfloor/constantcapfloortermvol");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            ConstantCapFloorTermVolatility s = StructureOf(c.Input("structure"), context);
            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "businessDayConvention", s.BusinessDayConvention);
            check.Output(c, "settlementDays", s.SettlementDays);
            check.Output(c, "volatilityByTenor", Points(c, "byTenor", (p, k, e) => s.Volatility(SwapInputs.PeriodOf(p), k, e)));
            check.Output(c, "volatilityByDate", Points(c, "byDate", (p, k, e) => s.Volatility(new Date(p.GetInt32()), k, e)));
            check.Output(c, "volatilityByTime", Points(c, "byTime", (p, k, e) => s.Volatility(p.GetDouble(), k, e)));
        }

        check.Complete();
    }

    private static ConstantCapFloorTermVolatility StructureOf(JsonElement spec, EvaluationContext context)
    {
        Calendar calendar = CashFlowInputs.CalendarOf(spec.GetProperty("calendar"));
        var bdc = (BusinessDayConvention)spec.GetProperty("convention").GetInt32();
        double vol = InterpolationInputs.Number(spec.GetProperty("volatility"));
        DayCounter dc = CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter"));
        return spec.TryGetProperty("settlementDays", out JsonElement days)
            ? ConstantCapFloorTermVolatility.Create(days.GetInt32(), calendar, bdc, vol, dc, context)
            : new ConstantCapFloorTermVolatility(new Date(spec.GetProperty("referenceDate").GetInt32()), calendar, bdc, vol, dc);
    }

    private static object?[] Points(VectorCase c, string name, Func<JsonElement, double, bool, double> volatility) =>
        [.. c.Input(name).EnumerateArray().Select(p => (Func<object?>)(() => volatility(p[0], InterpolationInputs.Number(p[1]), p[2].GetBoolean())))];
}
