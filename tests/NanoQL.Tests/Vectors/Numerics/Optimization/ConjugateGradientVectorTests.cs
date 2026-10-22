// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ConjugateGradient vectors, <c>tests/vectors/math/optimization/conjugategradient.json</c>:
/// <see cref="ConjugateGradient"/>'s minimum, on its default <see cref="ArmijoLineSearch"/>, through
/// <see cref="OptimizationMethod.Minimize(Problem, EndCriteria)"/> for simplex.py's cost functions
/// (<see cref="SimplexCostFunctions"/>), the point QuantLib's line search last accepted, read from the cost function's
/// trace, with the calls of the cost function.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ConjugateGradientVectorTests
{
    [Fact]
    public void ConjugateGradientVector()
    {
        var file = VectorFile.Load("math/optimization/conjugategradient");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            SimplexCostFunctions.Counted f = SimplexCostFunctions.Create(c.String("costFunction"));
            var problem = new Problem(f, new NoConstraint(), Numbers(c.Input("initialValue")));
            EndCriteria endCriteria = OptimizationSpecs.EndCriteria(c.Input("endCriteria"));
            check.Output(c, "x", () => new ConjugateGradient().Minimize(problem, endCriteria).CurrentValue);
            check.Output(c, "calls", f.Calls);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"math/optimization/conjugategradient: bit-identical {check.BitIdentical} of {check.Reals} real outputs");
        check.Complete();
    }
}
