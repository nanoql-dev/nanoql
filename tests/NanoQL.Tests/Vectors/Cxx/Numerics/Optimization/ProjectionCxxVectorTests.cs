// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's projection vectors, <c>tests/vectors/cxx/math/optimization/projection.json</c>:
/// <see cref="Projection.Project"/> and <see cref="Projection.Include"/> for projections of one to six parameters with
/// every mix of fixed ones, QuantLib's default flags among them, and the refusals of the projection and of its calls.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ProjectionCxxVectorTests
{
    [Fact]
    public void ProjectionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/projection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("method", out JsonElement method))
            {
                JsonElement[] p = [.. c.Input("params").EnumerateArray()];
                double[] values = Numbers(p[0]);
                bool[] flags = Flags(p[1]);
                var x = new Vector(Numbers(p[2]));
                Func<object?> result = method.GetString() switch
                {
                    "Projection::project" => () => new Projection(values, flags).Project(x),
                    "Projection::include" => () => new Projection(values, flags).Include(x),
                    _ => throw new InvalidOperationException($"{c.Name}: unknown method {method}"),
                };
                check.Output(c, "result", result);
                continue;
            }

            var projection = new Projection(Numbers(c.Input("parameterValues")), Flags(c.Input("fixParameters")));
            check.Output(
                c,
                "project",
                c.Input("parameters").EnumerateArray().Select(x => (object?)projection.Project(new Vector(Numbers(x)))));
            check.Output(
                c,
                "include",
                c.Input("projectedParameters").EnumerateArray().Select(x => (object?)projection.Include(new Vector(Numbers(x)))));
        }

        check.Complete();
    }

    internal static bool[] Flags(JsonElement e) => [.. e.EnumerateArray().Select(f => f.GetBoolean())];
}
