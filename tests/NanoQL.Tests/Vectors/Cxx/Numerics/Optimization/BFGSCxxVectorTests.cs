// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's BFGS vectors, <c>tests/vectors/cxx/math/optimization/bfgs.json</c>: <see cref="BFGS"/>'s results on
/// its default Armijo line search, each on a new method (QuantLib's) or the same one (nanoql's, whose inverse Hessian
/// starts afresh at each minimization), on QuantLib's test-suite functions and the harness's named ones
/// (<see cref="OptimizationSpecs"/>), under end criteria met and an iteration limit reached.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BFGSCxxVectorTests
{
    [Fact]
    public void BFGSCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/bfgs");
        var check = new VectorAssert(file);
        var method = new BFGS();
        foreach (VectorCase c in file.Cases)
        {
            var problem = new Problem(
                OptimizationSpecs.CostFunction(c.Input("costFunction")),
                OptimizationSpecs.Constraint(c.Input("constraint")),
                Numbers(c.Input("initialValue")));
            OptimizationSpecs.LineSearchResults(check, c, method.Minimize(problem, OptimizationSpecs.EndCriteria(c.Input("endCriteria"))));
        }

        check.Complete();
    }
}
