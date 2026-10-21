// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's constraint vectors, <c>tests/vectors/cxx/math/optimization/constraint.json</c>:
/// <see cref="Constraint.Test"/>, <see cref="Constraint.UpperBound"/>, <see cref="Constraint.LowerBound"/> and
/// <see cref="Constraint.Update"/> (the parameters it moves in place, and the step it returns) for the five
/// constraints at twenty parameter vectors each, and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConstraintCxxVectorTests
{
    [Fact]
    public void ConstraintCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/constraint");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Check(check, c);
        }

        check.Complete();
    }

    /// <summary>
    /// Checks a case of a constraint vector file: a refusal's method and parameters, or a constraint's test, bounds and
    /// updates at its parameter vectors.
    /// </summary>
    /// <param name="check">The comparison.</param>
    /// <param name="c">The case.</param>
    internal static void Check(VectorAssert check, VectorCase c)
    {
        if (c.Inputs.TryGetProperty("method", out JsonElement method))
        {
            JsonElement[] p = [.. c.Input("params").EnumerateArray()];
            Func<object?> result = method.GetString() switch
            {
                "Constraint::test" => () => OptimizationSpecs.Constraint(p[0]).Test(new Vector(Numbers(p[1]))),
                "Constraint::upperBound" => () => OptimizationSpecs.Constraint(p[0]).UpperBound(new Vector(Numbers(p[1]))),
                "Constraint::lowerBound" => () => OptimizationSpecs.Constraint(p[0]).LowerBound(new Vector(Numbers(p[1]))),
                "Constraint::update" => () => OptimizationSpecs.Constraint(p[0]).Update(
                    new Vector(Numbers(p[1])), new Vector(Numbers(p[2])), Number(p[3])),
                _ => throw new InvalidOperationException($"{c.Name}: unknown method {method}"),
            };
            check.Output(c, "result", result);
            return;
        }

        Constraint constraint = OptimizationSpecs.Constraint(c.Input("constraint"));
        double[][] parameters = [.. c.Input("params").EnumerateArray().Select(Numbers)];
        double[][] directions = [.. c.Input("directions").EnumerateArray().Select(Numbers)];
        double[] betas = Numbers(c.Input("betas"));
        check.Output(c, "test", parameters.Select(x => (object?)constraint.Test(new Vector(x))).ToArray());
        check.Output(c, "upperBound", parameters.Select(x => (object?)constraint.UpperBound(new Vector(x))).ToArray());
        check.Output(c, "lowerBound", parameters.Select(x => (object?)constraint.LowerBound(new Vector(x))).ToArray());
        check.Output(
            c,
            "updatedParams",
            parameters.Select((x, i) => (object?)(Func<object?>)(() =>
            {
                var moved = new Vector(x);
                constraint.Update(moved, new Vector(directions[i]), betas[i]);
                return moved;
            })).ToArray());
        check.Output(
            c,
            "diff",
            parameters.Select((x, i) => (object?)(Func<object?>)(() =>
                constraint.Update(new Vector(x), new Vector(directions[i]), betas[i]))).ToArray());
    }
}
