// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's multi-cubic spline vectors, <c>tests/vectors/cxx/math/interpolations/multicubicspline.json</c>:
/// <see cref="MultiCubicSpline"/> in one to five dimensions at points inside the grid, at nodes, on and below the lower
/// boundary with and without extrapolation, beyond the upper boundary (refused), and grids QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultiCubicSplineCxxVectorTests
{
    [Fact]
    public void MultiCubicSplineCxxVector()
    {
        var file = VectorFile.Load("cxx/math/interpolations/multicubicspline");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            ImmutableArray<ImmutableArray<double>> grid =
                [.. c.Input("grid").EnumerateArray().Select(g => Doubles(g).ToImmutableArray())];
            double[] values = c.Doubles("values");
            bool[] flags = [.. c.Input("extrapolation").EnumerateArray().Select(f => f.GetBoolean())];
            object?[] results =
            [
                .. c.Input("points").EnumerateArray().Select(p =>
                {
                    double[] x = Doubles(p);
                    return (Func<object?>)(() => new MultiCubicSpline(grid, values, flags).Evaluate(x));
                }),
            ];
            check.Output(c, "value", results);
        }

        check.Complete();
    }
}
