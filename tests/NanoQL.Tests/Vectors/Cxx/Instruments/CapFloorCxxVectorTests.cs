// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's cap and floor vectors, <c>tests/vectors/cxx/instruments/capfloor.json</c>: <see cref="CapFloor"/>'s
/// four-argument and three-argument constructors and their refusals, <see cref="CapFloor.Optionlet"/> at every index
/// and past the leg, and <see cref="CapFloor.LastFloatingRateCoupon"/>, which the wheel doesn't bind.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CapFloorCxxVectorTests
{
    [Fact]
    public void CapFloorCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/capfloor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("capFloor");
            if (c.Expected.TryGetProperty("built", out _))
            {
                check.Output(c, "built", () => CapFloorOf(spec, context));
                continue;
            }

            CapFloor capFloor = CapFloorOf(spec, context);
            check.Output(c, "built.type", capFloor.Type);
            check.Output(c, "built.capRates", capFloor.CapRates);
            check.Output(c, "built.floorRates", capFloor.FloorRates);
            check.Output(c, "built.legSize", capFloor.FloatingLeg.Length);

            int[] indexes = [.. Enumerable.Range(0, capFloor.FloatingLeg.Length + 1)];
            check.Output(c, "optionlet.type", CouponInputs.Each(indexes, i => capFloor.Optionlet(i).Type));
            check.Output(c, "optionlet.capRates", CouponInputs.Each(indexes, i => capFloor.Optionlet(i).CapRates));
            check.Output(c, "optionlet.floorRates", CouponInputs.Each(indexes, i => capFloor.Optionlet(i).FloorRates));
            check.Output(c, "optionlet.legSize", CouponInputs.Each(indexes, i => capFloor.Optionlet(i).FloatingLeg.Length));
            check.Output(c, "optionlet.date", CouponInputs.Each(indexes, i => capFloor.Optionlet(i).FloatingLeg[0].Date));
            check.Output(c, "optionlet.startDate", CouponInputs.Each(indexes, i => capFloor.Optionlet(i).StartDate()));
            check.Output(c, "optionlet.maturityDate", CouponInputs.Each(indexes, i => capFloor.Optionlet(i).MaturityDate()));

            if (capFloor.FloatingLeg.Length > 0)
            {
                FloatingRateCoupon? last = capFloor.LastFloatingRateCoupon();
                check.Output(c, "lastIsFloating", last is not null);
                if (last is not null)
                {
                    FloatingRateCouponCxxVectorTests.CheckInspectors(check, c, last, context, "last.");
                }
            }
        }

        check.Complete();
    }

    // The four-argument constructor on cap and floor rates, or the three-argument one on strikes.
    private static CapFloor CapFloorOf(JsonElement spec, EvaluationContext context)
    {
        var type = (CapFloorType)spec.GetProperty("type").GetInt32();
        ImmutableArray<CashFlow> leg = CapFloorInputs.LegOf(spec, context);
        return spec.TryGetProperty("strikes", out JsonElement strikes)
            ? new CapFloor(type, leg, CouponInputs.Reals(strikes))
            : new CapFloor(type, leg, CouponInputs.Reals(spec.GetProperty("capRates")), CouponInputs.Reals(spec.GetProperty("floorRates")));
    }
}
