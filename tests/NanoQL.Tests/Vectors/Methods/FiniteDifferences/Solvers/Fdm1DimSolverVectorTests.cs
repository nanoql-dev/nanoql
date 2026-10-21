// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Fdm1DimSolver vectors, <c>tests/vectors/methods/finitedifferences/solvers/fdm1dimsolver.json</c>:
/// <see cref="Fdm1DimSolver"/>'s interpolated value, theta and first and second derivatives at five log spots, for
/// European, American and Bermudan puts, dividends, the local volatility, damped calls under three schemes, and a
/// snapshot at time 0, where the theta is null.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Fdm1DimSolverVectorTests
{
    [Fact]
    public void Fdm1DimSolverVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/solvers/fdm1dimsolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("solver");
            (FdmBlackScholesOp op, FdmMesherComposite mesher) = OperatorOf(spec.GetProperty("op"), context);
            FdmInnerValueCalculator calculator = CalculatorOf(spec.GetProperty("calculator"), mesher);
            var desc = new FdmSolverDesc(
                mesher,
                [],
                CompositeOf(spec, mesher, calculator, context),
                calculator,
                spec.GetProperty("maturity").GetDouble(),
                spec.GetProperty("timeSteps").GetInt32(),
                spec.GetProperty("dampingSteps").GetInt32());
            var solver = new Fdm1DimSolver(desc, SchemeOf(spec.GetProperty("scheme")), op);
            double[] xs = Doubles(c.Input("x"));
            check.Output(c, "interpolateAt", xs.Select(x => (object?)solver.InterpolateAt(x)).ToArray());
            check.Output(c, "thetaAt", xs.Select(x => (object?)solver.ThetaAt(x)).ToArray());
            check.Output(c, "derivativeX", xs.Select(x => (object?)solver.DerivativeX(x)).ToArray());
            check.Output(c, "derivativeXX", xs.Select(x => (object?)solver.DerivativeXX(x)).ToArray());
        }

        check.Complete();
    }
}
