// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Simplex vectors, <c>tests/vectors/math/optimization/simplex.json</c>: <see cref="Simplex"/>'s minimum through
/// <see cref="OptimizationMethod.Minimize(Problem, EndCriteria)"/>, as the wheel's <c>Optimizer.solve</c> returns it,
/// for five scalar cost functions (<see cref="SimplexCostFunctions"/>) from six starts with three lambdas, with the
/// calls of the cost function and <see cref="Simplex.Lambda"/>.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SimplexVectorTests
{
    [Fact]
    public void SimplexVector()
    {
        var file = VectorFile.Load("math/optimization/simplex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            SimplexCostFunctions.Counted f = SimplexCostFunctions.Create(c.String("costFunction"));
            var problem = new Problem(f, new NoConstraint(), Numbers(c.Input("initialValue")));
            var simplex = new Simplex(c.Double("lambda"));
            EndCriteria endCriteria = OptimizationSpecs.EndCriteria(c.Input("endCriteria"));
            check.Output(c, "x", () => simplex.Minimize(problem, endCriteria).CurrentValue);
            check.Output(c, "calls", f.Calls);
            check.Output(c, "lambda", simplex.Lambda);
        }

        check.Complete();
    }
}
