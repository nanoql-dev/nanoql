// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.CommodityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's unit-conversion registry vectors,
/// <c>tests/vectors/cxx/experimental/commodities/unitofmeasureconversionmanager.json</c>:
/// <see cref="UnitOfMeasureConversions"/>' direct and derived lookups on QuantLib's known factors and on added ones,
/// each found conversion's factor, units and type and its convert of one unit, and the lookups refused.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class UnitOfMeasureConversionManagerCxxVectorTests
{
    [Fact]
    public void UnitOfMeasureConversionManagerCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/commodities/unitofmeasureconversionmanager");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            UnitOfMeasureConversions registry = c.Context().UnitOfMeasureConversions;
            foreach (System.Text.Json.JsonElement add in c.Input("adds").EnumerateArray())
            {
                registry = registry.Add(ConversionOf(add));
            }

            object?[] lookups =
            [
                .. c.Input("queries").EnumerateArray().Select(q => (Func<object?>)(() =>
                {
                    CommodityType type = CommodityTypeOf(q[0]);
                    UnitOfMeasure source = UnitOf(q[1]);
                    UnitOfMeasureConversion found = registry.Lookup(
                        type, source, UnitOf(q[2]), (UnitOfMeasureConversionType)q[3].GetInt32());
                    Quantity one = found.Convert(new Quantity(type, source, 1.0));
                    return new object?[]
                    {
                        found.ConversionFactor, found.Source.Code, found.Target.Code, found.Type,
                        one.UnitOfMeasure!.Code, one.Amount,
                    };
                })),
            ];
            check.Output(c, "lookups", lookups);
        }

        check.Complete();
    }
}
