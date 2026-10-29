// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.CommodityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's quantity vectors, <c>tests/vectors/cxx/experimental/commodities/quantity.json</c>:
/// <see cref="Quantity"/>'s conversion-mode methods under each of QuantLib's conversion types, through the context's
/// <see cref="QuantitySettings"/>, and its scalings. QuantLib's <c>==</c> throws on a unit mismatch without
/// conversion, where <see cref="Quantity.Equals(Quantity, EvaluationContext)"/> returns false (the S-xrt plan's
/// XRR1, <see cref="Money"/>'s form).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QuantityCxxVectorTests
{
    [Fact]
    public void QuantityCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/commodities/quantity");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement settings = c.Input("quantitySettings");
            JsonElement baseUnit = settings.GetProperty("baseUnit");
            EvaluationContext context = c.Context() with
            {
                QuantitySettings = new QuantitySettings
                {
                    ConversionType = (QuantityConversionType)settings.GetProperty("conversionType").GetInt32(),
                    BaseUnitOfMeasure = baseUnit.ValueKind == JsonValueKind.Null ? null : UnitOf(baseUnit),
                },
            };
            var pairs = new List<object?>();
            var equal = new List<Func<object?>>();
            foreach (JsonElement pair in c.Input("pairs").EnumerateArray())
            {
                Quantity q1 = QuantityOf(pair[0]);
                Quantity q2 = QuantityOf(pair[1]);
                pairs.Add(new object?[]
                {
                    (Func<object?>)(() => Result(q1.Add(q2, context))),
                    (Func<object?>)(() => Result(q1.Subtract(q2, context))),
                    (Func<object?>)(() => q1.Divide(q2, context)),
                    (Func<object?>)(() => q1.LessThan(q2, context)),
                    (Func<object?>)(() => q1.LessThanOrEqual(q2, context)),
                    (Func<object?>)(() => q1.GreaterThan(q2, context)),
                    (Func<object?>)(() => q1.GreaterThanOrEqual(q2, context)),
                    (Func<object?>)(() => QuantityFunctions.Close(q1, q2, context)),
                    (Func<object?>)(() => QuantityFunctions.CloseEnough(q1, q2, context)),
                    Result(+q1), Result(-q1), Result(q1 * 2.5), Result(2.5 * q1), Result(q1 / 4.0),
                });
                equal.Add(() => q1.Equals(q2, context));
            }

            check.Output(c, "pairs", pairs);
            if (context.QuantitySettings.ConversionType == QuantityConversionType.NoConversion)
            {
                check.Mapped(c, "equal", equal, false, "a unit mismatch without conversion is unequal (XRR1)");
            }
            else
            {
                check.Output(c, "equal", equal);
            }
        }

        check.Complete();
    }
}
