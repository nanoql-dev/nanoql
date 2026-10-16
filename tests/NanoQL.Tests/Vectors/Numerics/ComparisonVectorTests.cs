// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The comparison vector, <c>tests/vectors/math/comparison.json</c>: <c>close</c> and <c>close_enough</c>, with the
/// default 42 epsilons and with several others, on finite, tiny, huge, zero and infinite numbers.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ComparisonVectorTests
{
    [Fact]
    public void ComparisonVector()
    {
        var file = VectorFile.Load("math/comparison");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double x = Number(c.Input("x"));
            double[] others = [.. c.Input("others").EnumerateArray().Select(Number)];
            int[] ns = [.. c.Input("n").EnumerateArray().Select(n => n.GetInt32())];
            check.Output(c, "close", others.Select(y => (object?)ComparisonFunctions.Close(x, y)).ToArray());
            check.Output(
                c,
                "closeN",
                others.Select(y => (object?)ns.Select(n => ComparisonFunctions.Close(x, y, n)).ToArray()).ToArray());
            check.Output(c, "closeEnough", others.Select(y => (object?)ComparisonFunctions.CloseEnough(x, y)).ToArray());
            check.Output(
                c,
                "closeEnoughN",
                others.Select(y => (object?)ns.Select(n => ComparisonFunctions.CloseEnough(x, y, n)).ToArray()).ToArray());
        }

        check.Complete();
    }

    // A number as the vectors encode it: the non-finite ones as strings.
    private static double Number(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, CultureInfo.InvariantCulture) : e.GetDouble();
}
