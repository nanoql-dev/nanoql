// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Levenberg–Marquardt vectors, <c>tests/vectors/cxx/math/optimization/levenbergmarquardt.json</c>:
/// <see cref="LevenbergMarquardt"/>'s results through <see cref="OptimizationMethod.Minimize(Problem, EndCriteria)"/>
/// (and so MINPACK's lmdif) on the harness's named cost functions (<see cref="OptimizationSpecs"/>), with and without
/// the cost function's Jacobian, under end criteria met and an iteration limit reached, under constraints, and the
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LevenbergMarquardtCxxVectorTests
{
    [Fact]
    public void LevenbergMarquardtCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/levenbergmarquardt");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var problem = new Problem(
                OptimizationSpecs.CostFunction(c.Input("costFunction")),
                OptimizationSpecs.Constraint(c.Input("constraint")),
                Numbers(c.Input("initialValue")));
            EndCriteria endCriteria = OptimizationSpecs.EndCriteria(c.Input("endCriteria"));
            var method = new LevenbergMarquardt(
                Number(c.Input("epsfcn")),
                Number(c.Input("xtol")),
                Number(c.Input("gtol")),
                c.Bool("useCostFunctionsJacobian"));
            if (c.Expected.TryGetProperty("minimize", out _))
            {
                check.Output(c, "minimize", () => method.Minimize(problem, endCriteria));
                continue;
            }

            OptimizationSpecs.Results(check, c, method.Minimize(problem, endCriteria));
        }

        check.Complete();
    }
}
