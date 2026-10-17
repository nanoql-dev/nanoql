// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Array vectors, <c>tests/vectors/math/array.json</c>: <see cref="Vector"/>'s element-by-element and
/// scalar-operand operators, negation, the dot product, equality, size, text and the size-and-value constructor, with
/// the refusals of operands of another size, on 24 pairs of 1 to 8 elements.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ArrayVectorTests
{
    [Fact]
    public void ArrayVector()
    {
        var file = VectorFile.Load("math/array");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] x = Numbers(c.Input("x")), y = Numbers(c.Input("y")), z = Numbers(c.Input("z"));
            double s = Number(c.Input("s"));
            Vector a = new(x), b = new(y), other = new(z);
            check.Output(c, "sum", a + b);
            check.Output(c, "difference", a - b);
            check.Output(c, "product", a * b);
            check.Output(c, "quotient", a / b);
            check.Output(c, "sumScalar", a + s);
            check.Output(c, "differenceScalar", a - s);
            check.Output(c, "productScalar", a * s);
            check.Output(c, "quotientScalar", a / s);
            check.Output(c, "negative", -a);
            check.Output(c, "dotProduct", ArrayFunctions.DotProduct(a, b));
            check.Output(c, "equal", a == b);
            check.Output(c, "notEqual", a != b);
            check.Output(c, "equalCopy", a == new Vector(x));
            check.Output(c, "equalOtherSize", a == other);
            check.Output(c, "size", a.Size);
            check.Output(c, "text", a.ToString());
            check.Output(c, "filled", new Vector(x.Length, s));
            check.Output(c, "sumOtherSize", () => a + other);
            check.Output(c, "differenceOtherSize", () => a - other);
            check.Output(c, "productOtherSize", () => a * other);
            check.Output(c, "quotientOtherSize", () => a / other);
            check.Output(c, "dotProductOtherSize", () => ArrayFunctions.DotProduct(a, other));
        }

        check.Complete();
    }

    // A number as the vectors encode it: the non-finite ones as strings.
    internal static double Number(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();

    internal static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(Number)];
}
