// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's cost function vectors, <c>tests/vectors/cxx/math/optimization/costfunction.json</c>:
/// <see cref="CostFunction"/>'s defaults (<see cref="CostFunction.Value"/>, <see cref="CostFunction.Gradient"/>,
/// <see cref="CostFunction.Jacobian"/>, <see cref="CostFunction.ValueAndGradient"/>,
/// <see cref="CostFunction.ValuesAndJacobian"/>, <see cref="CostFunction.FiniteDifferenceEpsilon"/>) on the harness's
/// named cost functions (<see cref="OptimizationSpecs"/>), and <see cref="SimpleCostFunction"/> over a named values
/// function.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CostFunctionCxxVectorTests
{
    [Fact]
    public void CostFunctionCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/costfunction");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CostFunction f = OptimizationSpecs.CostFunction(c.Input("costFunction"));
            var x = new Vector(Numbers(c.Input("x")));
            if (c.Expected.TryGetProperty("values", out _))
            {
                check.Output(c, "values", f.Values(x));
            }

            check.Output(c, "value", f.Value(x));
            var gradient = new Vector(x.Size);
            f.Gradient(gradient, x);
            check.Output(c, "gradient", gradient);
            Matrix jacobian = JacobianFor(f, x);
            f.Jacobian(jacobian, x);
            check.Output(c, "jacobian", OptimizationSpecs.Rows(jacobian));
            if (c.Expected.TryGetProperty("valueAndGradient", out _))
            {
                gradient = new Vector(x.Size);
                check.Output(c, "valueAndGradient", f.ValueAndGradient(gradient, x));
                check.Output(c, "valueAndGradientGradient", gradient);
                jacobian = JacobianFor(f, x);
                check.Output(c, "valuesAndJacobian", f.ValuesAndJacobian(jacobian, x));
                check.Output(c, "valuesAndJacobianJacobian", OptimizationSpecs.Rows(jacobian));
            }

            if (c.Expected.TryGetProperty("finiteDifferenceEpsilon", out _))
            {
                check.Output(c, "finiteDifferenceEpsilon", f.FiniteDifferenceEpsilon);
            }
        }

        check.Complete();
    }

    // The harness's Jacobian: a row per value at x, a column per variable.
    private static Matrix JacobianFor(CostFunction f, Vector x) => new(f.Values(x).Size, x.Size);
}
