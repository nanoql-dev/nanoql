// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The constraint vectors, <c>tests/vectors/math/optimization/constraint.json</c>: <see cref="Simplex"/>'s minimum under
/// each of the five constraints (<see cref="NoConstraint"/>, <see cref="PositiveConstraint"/>,
/// <see cref="BoundaryConstraint"/>, <see cref="CompositeConstraint"/>, <see cref="NonhomogeneousBoundaryConstraint"/>),
/// whose <see cref="Constraint.Update"/> moves the first vertices and whose <see cref="Constraint.Test"/> refuses the
/// trial points outside, with the calls of the cost function; and the refusal of a start outside the constraint.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConstraintVectorTests
{
    [Fact]
    public void ConstraintVector()
    {
        var file = VectorFile.Load("math/optimization/constraint");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            SimplexCostFunctions.Counted f = SimplexCostFunctions.Create(c.String("costFunction"));
            Constraint constraint = OptimizationSpecs.Constraint(c.Input("constraint"));
            var problem = new Problem(f, constraint, Numbers(c.Input("initialValue")));
            var simplex = new Simplex(c.Double("lambda"));
            EndCriteria endCriteria = OptimizationSpecs.EndCriteria(c.Input("endCriteria"));
            check.Output(c, "x", () => simplex.Minimize(problem, endCriteria).CurrentValue);
            check.Output(c, "calls", f.Calls);
        }

        check.Complete();
    }
}
