// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's projected-constraint vectors, <c>tests/vectors/cxx/math/optimization/projectedconstraint.json</c>:
/// <see cref="Constraint.Test"/>, <see cref="Constraint.UpperBound"/> and <see cref="Constraint.LowerBound"/> of
/// <see cref="ProjectedConstraint"/>, built by both constructors, over the five constraints and three projections, and
/// the projection's refusals, which reach its caller.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ProjectedConstraintCxxVectorTests
{
    [Fact]
    public void ProjectedConstraintCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/projectedconstraint");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("method", out JsonElement method))
            {
                JsonElement[] p = [.. c.Input("params").EnumerateArray()];
                var x = new Vector(Numbers(p[4]));
                ProjectedConstraint Make() => Of(p[0].GetString()!, p[1], p[2], p[3]);
                Func<object?> result = method.GetString() switch
                {
                    "ProjectedConstraint::test" => () => Make().Test(x),
                    "ProjectedConstraint::upperBound" => () => Make().UpperBound(x),
                    "ProjectedConstraint::lowerBound" => () => Make().LowerBound(x),
                    _ => throw new InvalidOperationException($"{c.Name}: unknown method {method}"),
                };
                check.Output(c, "result", result);
                continue;
            }

            ProjectedConstraint constraint = Of(
                c.String("form"), c.Input("constraint"), c.Input("parameterValues"), c.Input("fixParameters"));
            Vector[] parameters = [.. c.Input("params").EnumerateArray().Select(x => new Vector(Numbers(x)))];
            check.Output(c, "test", parameters.Select(x => (object?)constraint.Test(x)));
            check.Output(c, "upperBound", parameters.Select(x => (object?)constraint.UpperBound(x)));
            check.Output(c, "lowerBound", parameters.Select(x => (object?)constraint.LowerBound(x)));
        }

        check.Complete();
    }

    // The form "values" is the constructor on the values and the flags; "projection" the one on a projection of them.
    private static ProjectedConstraint Of(string form, JsonElement constraint, JsonElement values, JsonElement flags)
    {
        Constraint c = OptimizationSpecs.Constraint(constraint);
        double[] v = Numbers(values);
        bool[] f = ProjectionCxxVectorTests.Flags(flags);
        return form == "projection" ? new ProjectedConstraint(c, new Projection(v, f)) : new ProjectedConstraint(c, v, f);
    }
}
