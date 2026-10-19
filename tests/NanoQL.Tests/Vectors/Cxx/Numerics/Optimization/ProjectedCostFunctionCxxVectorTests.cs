// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's projected cost function vectors, <c>tests/vectors/cxx/math/optimization/projectedcostfunction.json</c>:
/// <see cref="ProjectedCostFunction.Value"/>, <see cref="ProjectedCostFunction.Values"/>,
/// <see cref="ProjectedCostFunction.Project"/>, <see cref="ProjectedCostFunction.Include"/> and
/// <see cref="CostFunction.FiniteDifferenceEpsilon"/> over the harness's named cost functions, by both constructors,
/// and the refusals of the projection and of each member.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ProjectedCostFunctionCxxVectorTests
{
    [Fact]
    public void ProjectedCostFunctionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/projectedcostfunction");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.String("kind"))
            {
                case "projected":
                    ProjectedCostFunction p = Build(c);
                    check.Output(c, "value", Each(c, "freeParameters", x => p.Value(x)));
                    check.Output(c, "values", Each(c, "freeParameters", p.Values));
                    check.Output(c, "project", Each(c, "parameters", p.Project));
                    check.Output(c, "include", Each(c, "projectedParameters", p.Include));
                    check.Output(c, "finiteDifferenceEpsilon", p.FiniteDifferenceEpsilon);
                    break;
                case "refusal":
                    var x = new Vector(Numbers(c.Input("x")));
                    check.Output(c, "result", () => c.String("member") switch
                    {
                        "value" => Build(c).Value(x),
                        "values" => Build(c).Values(x),
                        "project" => Build(c).Project(x),
                        "include" => Build(c).Include(x),
                        string member => throw new InvalidOperationException($"{c.Name}: unknown member {member}"),
                    });
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    // The case's projected cost function, by the constructor it names.
    private static ProjectedCostFunction Build(VectorCase c)
    {
        CostFunction f = OptimizationSpecs.CostFunction(c.Input("costFunction"));
        double[] values = Numbers(c.Input("parameterValues"));
        bool[] flags = ProjectionCxxVectorTests.Flags(c.Input("fixParameters"));
        return c.String("constructor") switch
        {
            "values" => new ProjectedCostFunction(f, values, flags),
            "projection" => new ProjectedCostFunction(f, new Projection(values, flags)),
            string constructor => throw new InvalidOperationException($"{c.Name}: unknown constructor {constructor}"),
        };
    }

    // Each vector of an input, its result computed when compared, so that a throw is checked.
    private static Func<object?>[] Each(VectorCase c, string input, Func<Vector, object> f) =>
        [.. c.Input(input).EnumerateArray().Select(e => (Func<object?>)(() => f(new Vector(Numbers(e)))))];
}
