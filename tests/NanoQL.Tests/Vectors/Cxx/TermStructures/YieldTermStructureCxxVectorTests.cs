// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's yield term structure vectors, <c>tests/vectors/cxx/termstructures/yieldtermstructure.json</c>: the
/// jumps of a <see cref="YieldTermStructure"/>, on an <see cref="InterpolatedDiscountCurve{TInterpolator}"/> of
/// <see cref="LogLinear"/>: whether the curve builds, <see cref="YieldTermStructure.JumpDates"/>,
/// <see cref="YieldTermStructure.JumpTimes"/>, and the discounts the jumps apply to.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class YieldTermStructureCxxVectorTests
{
    [Fact]
    public void YieldTermStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/yieldtermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("curve");
            JsonElement jumps = spec.GetProperty("jumps");
            JsonElement jumpDates = spec.GetProperty("jumpDates");
            YieldTermStructure? curve = null;
            check.Output(c, "built", () =>
            {
                curve = new InterpolatedDiscountCurve<LogLinear>(
                    spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32())),
                    CouponInputs.Reals(spec.GetProperty("discounts")),
                    CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                    null,
                    jumps.ValueKind == JsonValueKind.Null ? null : CouponInputs.Reals(jumps),
                    jumpDates.ValueKind == JsonValueKind.Null
                        ? null
                        : jumpDates.EnumerateArray().Select(d => new Date(d.GetInt32())));
                return true;
            });
            if (curve is null)
            {
                continue;
            }

            check.Output(c, "jumpDates", curve.JumpDates());
            check.Output(c, "jumpTimes", curve.JumpTimes());
            bool[] flags = InterpolationInputs.Bools(c.Input("extrapolate"));
            check.Output(c, "discounts", c.DateInputs("discountDates").Zip(flags)
                .Select(p => (Func<object?>)(() => curve.Discount(p.First, p.Second)))
                .ToArray());
        }

        check.Complete();
    }
}
