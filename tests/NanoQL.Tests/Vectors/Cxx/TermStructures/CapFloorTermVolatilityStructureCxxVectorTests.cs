// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's cap/floor term-volatility structure vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/capfloor/capfloortermvolatilitystructure.json</c>: on
/// <see cref="CapFloorTermVolSurface"/>s, <see cref="TermStructure.SettlementDays"/> (null for a fixed reference date,
/// where QuantLib refuses), the structure's inspectors, <see cref="VolatilityTermStructure.OptionDateFromTenor"/> and
/// <see cref="CapFloorTermVolatilityStructure"/>'s three volatility overloads inside and outside the range.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CapFloorTermVolatilityStructureCxxVectorTests
{
    [Fact]
    public void CapFloorTermVolatilityStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/capfloor/capfloortermvolatilitystructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CapFloorTermVolatilityStructure s = CapFloorTermVolSurfaceVectorTests.SurfaceOf(c.Input("surface"), context);

            check.Output(c, "settlementDays", s.SettlementDays);
            check.Output(c, "referenceDate", s.ReferenceDate);
            check.Output(c, "maxDate", s.MaxDate);
            check.Output(c, "maxTime", s.MaxTime);
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            check.Output(c, "businessDayConvention", s.BusinessDayConvention);
            check.Output(
                c, "optionDateFromTenor", CouponInputs.Each(c.Input("tenors").EnumerateArray(), t => s.OptionDateFromTenor(SwapInputs.PeriodOf(t))));
            check.Output(c, "volatilityByTenor", Points(c, "byTenor", (p, k, e) => s.Volatility(SwapInputs.PeriodOf(p), k, e)));
            check.Output(c, "volatilityByDate", Points(c, "byDate", (p, k, e) => s.Volatility(new Date(p.GetInt32()), k, e)));
            check.Output(c, "volatilityByTime", Points(c, "byTime", (p, k, e) => s.Volatility(p.GetDouble(), k, e)));
        }

        check.Complete();
    }

    private static object?[] Points(VectorCase c, string name, Func<JsonElement, double, bool, double> volatility) =>
        [.. c.Input(name).EnumerateArray().Select(p => (Func<object?>)(() => volatility(p[0], InterpolationInputs.Number(p[1]), p[2].GetBoolean())))];
}
