// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interpolated simple-zero curve vectors, <c>tests/vectors/termstructures/yield/interpolatedsimplezerocurve.json</c>:
/// <see cref="InterpolatedSimpleZeroCurve{TInterpolator}"/> on linear interpolation, built through
/// <see cref="SimpleZeroCurve"/> or the generic as each case names, with and without jumps, and its constructor's
/// refusals: whether the curve builds, its dates, times, data, zero rates and nodes, its maximum date and its discounts
/// at the case's dates, one element per case.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedSimpleZeroCurveVectorTests
{
    [Fact]
    public void InterpolatedSimpleZeroCurveVector()
    {
        var file = VectorFile.Load("termstructures/yield/interpolatedsimplezerocurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Date[] dates = c.DateInputs("dates");
            double[] rates = c.Doubles("rates");
            DayCounter dayCounter = CashFlowInputs.DayCounterOf(c.Input("dayCounter"));
            JsonElement calendarSpec = c.Input("calendar");
            Calendar? calendar =
                calendarSpec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CalendarOf(calendarSpec);
            JsonElement jumpsSpec = c.Input("jumps");
            double[]? jumps = jumpsSpec.ValueKind == JsonValueKind.Null ? null : CouponInputs.Reals(jumpsSpec);
            Date[]? jumpDates = c.Input("jumpDates").ValueKind == JsonValueKind.Null ? null : c.DateInputs("jumpDates");
            string? named = c.Input("classes")[0].GetString();
            Date[] when = c.DateInputs("discountDates");
            bool[] flags = InterpolationInputs.Bools(c.Input("extrapolate"));

            InterpolatedSimpleZeroCurve<Linear>? curve = null;
            check.Output(c, "built", new object?[]
            {
                (Func<object?>)(() =>
                {
                    curve = named switch
                    {
                        "SimpleZeroCurve" => new SimpleZeroCurve(dates, rates, dayCounter, calendar, jumps, jumpDates),
                        null => new InterpolatedSimpleZeroCurve<Linear>(dates, rates, dayCounter, calendar, jumps, jumpDates),
                        _ => throw new InvalidOperationException($"unknown class {named}"),
                    };
                    return true;
                }),
            });
            check.Output(c, "dates", new object?[] { curve?.Dates().Cast<object?>().ToArray() ?? [] });
            check.Output(c, "times", new object?[] { curve?.Times().Cast<object?>().ToArray() ?? [] });
            check.Output(c, "data", new object?[] { curve?.Data().Cast<object?>().ToArray() ?? [] });
            check.Output(c, "zeroRates", new object?[] { curve?.ZeroRates().Cast<object?>().ToArray() ?? [] });
            check.Output(c, "nodes", new object?[]
            {
                curve?.Nodes().Select(n => (object?)new object?[] { n.Key, n.Value }).ToArray() ?? [],
            });
            check.Output(c, "maxDate", new object?[] { curve?.MaxDate });
            check.Output(c, "discountAt", new object?[]
            {
                curve is null
                    ? []
                    : when.Zip(flags).Select(p => (Func<object?>)(() => curve.Discount(p.First, p.Second)))
                        .ToArray<object?>(),
            });
        }

        check.Complete();
    }
}
