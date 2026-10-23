// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's natural cubic fitting vectors, <c>tests/vectors/cxx/termstructures/yield/nonlinearfittingmethods.json</c>:
/// <see cref="NaturalCubicFitting"/>'s size and discount function, which the wheel doesn't expose, through a fitted
/// curve that evaluates it on given nodal discounts, on seven knot sets, one with cut-offs, and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NaturalCubicFittingCxxVectorTests
{
    [Fact]
    public void NaturalCubicFittingCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/yield/nonlinearfittingmethods");
        var check = new VectorAssert(file);
        var a365 = new Actual365Fixed();
        foreach (VectorCase c in file.Cases)
        {
            double[] knots = c.Doubles("knotTimes"), parameters = c.Doubles("parameters"), times = c.Doubles("times");
            JsonElement lo = c.Input("minCutoffTime"), hi = c.Input("maxCutoffTime");
            Date reference = c.DateInput("referenceDate"), maxDate = c.DateInput("maxDate");
            FittedBondDiscountCurve Build() => new(
                reference,
                new NaturalCubicFitting(
                    knots,
                    minCutoffTime: lo.ValueKind == JsonValueKind.Null ? 0.0 : lo.GetDouble(),
                    maxCutoffTime: hi.ValueKind == JsonValueKind.Null ? double.MaxValue : hi.GetDouble()),
                parameters,
                maxDate,
                a365);
            if (c.Name.StartsWith("refusal: ", StringComparison.Ordinal))
            {
                check.Output(c, "discount", () => Build().Discount(times[0]));
                continue;
            }

            FittedBondDiscountCurve curve = Build();
            check.Output(c, "size", curve.FitResults.Size);
            check.Output(c, "discount", times.Select(t => (Func<object?>)(() => curve.Discount(t))).ToArray());
        }

        check.Complete();
    }
}
