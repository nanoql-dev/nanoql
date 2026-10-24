// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Merton76Process vectors, <c>tests/vectors/processes/merton76process.json</c>: <see cref="Merton76Process"/>'s
/// x0, and at five points per process the members that refuse with QuantLib's: the drift, diffusion and apply, and the
/// Euler discretization's expectation, standard deviation, variance and evolved state.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Merton76ProcessVectorTests
{
    [Fact]
    public void Merton76ProcessVector()
    {
        var file = VectorFile.Load("processes/merton76process");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("process");
            EvaluationContext context = c.Context();
            var p = new Merton76Process(
                spec.GetProperty("x0").GetDouble(),
                CurveOf(spec.GetProperty("dividend")),
                CurveOf(spec.GetProperty("riskFree")),
                BlackVolOf(spec.GetProperty("volatility"), context),
                spec.GetProperty("jumpIntensity").GetDouble(),
                spec.GetProperty("logMeanJump").GetDouble(),
                spec.GetProperty("logJumpVolatility").GetDouble());
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Doubles)];
            check.Output(c, "x0", p.X0);
            check.Output(c, "drift", points.Select(q => Lazy(() => p.Drift(q[0], q[1]))).ToArray());
            check.Output(c, "diffusion", points.Select(q => Lazy(() => p.Diffusion(q[0], q[1]))).ToArray());
            check.Output(c, "expectation", points.Select(q => Lazy(() => p.Expectation(q[0], q[1], q[2]))).ToArray());
            check.Output(c, "stdDeviation", points.Select(q => Lazy(() => p.StdDeviation(q[0], q[1], q[2]))).ToArray());
            check.Output(c, "variance", points.Select(q => Lazy(() => p.Variance(q[0], q[1], q[2]))).ToArray());
            check.Output(c, "evolve", points.Select(q => Lazy(() => p.Evolve(q[0], q[1], q[2], q[3]))).ToArray());
            check.Output(c, "apply", points.Select(q => Lazy(() => p.Apply(q[1], q[3]))).ToArray());
        }

        check.Complete();
    }
}
