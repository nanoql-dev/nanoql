// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's rounding vectors, <c>tests/vectors/cxx/math/rounding.json</c>: what the wheel doesn't expose of
/// <see cref="Rounding"/>, the constructor <see cref="Rounding(int, RoundingType, int)"/> for the six types at
/// precisions 0 to 16 with digits 0, 5 and 10, its <see cref="Rounding.Precision"/>, <see cref="Rounding.Type"/> and
/// <see cref="Rounding.RoundingDigit"/>, and the rounding of two values; and the type of the default
/// <see cref="Rounding()"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RoundingCxxVectorTests
{
    [Fact]
    public void RoundingCxxVector()
    {
        var file = VectorFile.Load("cxx/math/rounding");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("rounding");
            if (spec.ValueKind == JsonValueKind.Null)
            {
                check.Output(c, "type", new Rounding().Type);
                continue;
            }

            var rounding = new Rounding(
                spec.GetProperty("precision").GetInt32(),
                (RoundingType)spec.GetProperty("type").GetInt32(),
                spec.GetProperty("digit").GetInt32());
            check.Output(c, "precision", () => rounding.Precision());
            check.Output(c, "type", rounding.Type);
            check.Output(c, "roundingDigit", () => rounding.RoundingDigit());
            check.Output(c, "rounded", c.Doubles("values").Select(x => (object?)rounding.Evaluate(x)));
        }

        check.Complete();
    }
}
