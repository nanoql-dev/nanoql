// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's steepest-descent vectors, <c>tests/vectors/cxx/math/optimization/steepestdescent.json</c>:
/// <see cref="SteepestDescent"/>'s results on its default Armijo line search, on QuantLib's test-suite functions and the
/// harness's named ones (<see cref="OptimizationSpecs"/>), under end criteria met and an iteration limit reached.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SteepestDescentCxxVectorTests
{
    [Fact]
    public void SteepestDescentCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/steepestdescent");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var problem = new Problem(
                OptimizationSpecs.CostFunction(c.Input("costFunction")),
                OptimizationSpecs.Constraint(c.Input("constraint")),
                Numbers(c.Input("initialValue")));
            OptimizationSpecs.LineSearchResults(
                check, c, new SteepestDescent().Minimize(problem, OptimizationSpecs.EndCriteria(c.Input("endCriteria"))));
        }

        check.Complete();
    }
}
