// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's L-BFGS-B vectors, <c>tests/vectors/cxx/math/optimization/lbfgsb.json</c>: <see cref="LBFGSB"/>'s
/// results on QuantLib's test problems, through the constraint's bounds and the constructor's, under QuantLib's test's
/// end criteria and an iteration limit. Where a case ends on the stationary function value its gradient norm is left out
/// of the file (the S-core plan's CR7 (xviii)).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LBFGSBCxxVectorTests
{
    [Fact]
    public void LBFGSBCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/lbfgsb");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            int memory = c.Int("memory");
            double pgTol = c.Double("pgTol"), fTol = c.Double("fTol");
            LBFGSB method = c.Input("lowerBound").ValueKind == JsonValueKind.Null
                ? new LBFGSB(memory, pgTol, fTol)
                : new LBFGSB(c.Doubles("lowerBound"), c.Doubles("upperBound"), memory, pgTol, fTol);
            var problem = new Problem(
                OptimizationSpecs.CostFunction(c.Input("costFunction")),
                OptimizationSpecs.Constraint(c.Input("constraint")),
                Numbers(c.Input("initialValue")));
            ProblemResults r = method.Minimize(problem, OptimizationSpecs.EndCriteria(c.Input("endCriteria")));
            check.Output(c, "currentValue", r.CurrentValue);
            check.Output(c, "functionValue", r.FunctionValue);
            if (c.Expected.TryGetProperty("gradientNormValue", out _))
            {
                check.Output(c, "gradientNormValue", r.GradientNormValue);
            }

            check.Output(c, "functionEvaluation", r.FunctionEvaluation);
            check.Output(c, "gradientEvaluation", r.GradientEvaluation);
            check.Output(c, "endCriteriaType", r.EndCriteriaType);
        }

        check.Complete();
    }
}
