// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The EulerDiscretization vectors, <c>tests/vectors/processes/eulerdiscretization.json</c>: the moments of
/// Black-Scholes processes whose moments are Euler's (on a surface, or given a local volatility), and their
/// covariance through the multi-dimensional interface.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EulerDiscretizationVectorTests
{
    [Fact]
    public void EulerDiscretizationVector()
    {
        var file = VectorFile.Load("processes/eulerdiscretization");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            GeneralizedBlackScholesProcess p = c.Inputs.TryGetProperty("localVolatility", out var local)
                ? ProcessWithLocalVolOf(c.Input("process"), local, context)
                : ProcessOf(c.Input("process"), context);
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "stdDeviation", points.Select(x => (object?)p.StdDeviation(x[0], x[1], x[2])).ToArray());
            check.Output(c, "variance", points.Select(x => (object?)p.Variance(x[0], x[1], x[2])).ToArray());
            check.Output(c, "evolve", points.Select(x => (object?)p.Evolve(x[0], x[1], x[2], x[3])).ToArray());
            check.Output(
                c,
                "covariance",
                points.Select(x => (object?)((StochasticProcess)p).Covariance(x[0], new Vector(1, x[1]), x[2])[0, 0]).ToArray());
        }

        check.Complete();
    }
}
