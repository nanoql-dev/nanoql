// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.CommodityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's unit conversion vectors, <c>tests/vectors/cxx/experimental/commodities/unitofmeasureconversion.json</c>:
/// <see cref="UnitOfMeasureConversion"/>'s convert of quantities in its units and others, its code, and
/// <see cref="UnitOfMeasureConversion.Chain"/> in its four orientations and its refusal.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class UnitOfMeasureConversionCxxVectorTests
{
    [Fact]
    public void UnitOfMeasureConversionCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/commodities/unitofmeasureconversion");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Quantity[] quantities = [.. c.Input("quantities").EnumerateArray().Select(QuantityOf)];
            if (c.Inputs.TryGetProperty("conversion", out JsonElement spec))
            {
                UnitOfMeasureConversion conversion = ConversionOf(spec);
                check.Output(c, "code", conversion.Code);
                check.Output(c, "converted", Converted(conversion, quantities));
                check.Output(
                    c, "texts", quantities.Select(q => (Func<object?>)(() => conversion.Convert(q).ToString())).ToArray());
                check.Output(c, "units", new object?[]
                {
                    conversion.Source.ToString(), conversion.Target.ToString(), conversion.CommodityType!.ToString(),
                    conversion.CommodityType != new NullCommodityType(),
                });
                continue;
            }

            UnitOfMeasureConversion? chained = null;
            Func<UnitOfMeasureConversion> chain = () => chained ??=
                UnitOfMeasureConversion.Chain(ConversionOf(c.Input("first")), ConversionOf(c.Input("second")));
            if (c.Expected.TryGetProperty("chain", out _))
            {
                check.Output(c, "chain", () => chain());
                continue;
            }

            check.Output(c, "factor", chain().ConversionFactor);
            check.Output(c, "source", chain().Source.Code);
            check.Output(c, "target", chain().Target.Code);
            check.Output(c, "type", chain().Type);
            check.Output(c, "converted", Converted(chain(), quantities));
        }

        check.Complete();
    }

    private static object?[] Converted(UnitOfMeasureConversion conversion, Quantity[] quantities) =>
        [.. quantities.Select(q => (Func<object?>)(() => Result(conversion.Convert(q))))];
}
