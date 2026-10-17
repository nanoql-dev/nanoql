// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The StochasticProcess vectors, <c>tests/vectors/stochasticprocess.json</c>: on Ornstein-Uhlenbeck processes,
/// <see cref="StochasticProcess1D"/>'s defaults the process doesn't override (<see cref="StochasticProcess1D.Evolve(double, double, double, double)"/>,
/// <see cref="StochasticProcess1D.Apply(double, double)"/>) and the multi-dimensional interface it implements on
/// vectors and matrices of size 1 (<see cref="StochasticProcess.Size"/>, the base's <see cref="StochasticProcess.Factors"/>,
/// <see cref="StochasticProcess.InitialValues"/>, and the vector forms of drift, diffusion, expectation, standard
/// deviation, covariance and evolve). A matrix compares as its rows.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StochasticProcessVectorTests
{
    [Fact]
    public void StochasticProcessVector()
    {
        var file = VectorFile.Load("stochasticprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            StochasticProcess1D p = Process(c.Input("process"));
            StochasticProcess multi = p;
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Numbers)];
            check.Output(c, "size", multi.Size);
            check.Output(c, "factors", multi.Factors);
            check.Output(c, "initialValues", multi.InitialValues());
            check.Output(c, "evolve", points.Select(q => (object?)p.Evolve(q[0], q[1], q[2], q[3])).ToArray());
            check.Output(c, "apply", points.Select(q => (object?)p.Apply(q[1], q[4])).ToArray());
            check.Output(c, "driftArray", points.Select(q => (object?)multi.Drift(q[0], Single(q[1]))).ToArray());
            check.Output(c, "diffusionArray", points.Select(q => (object?)Rows(multi.Diffusion(q[0], Single(q[1])))).ToArray());
            check.Output(
                c,
                "expectationArray",
                points.Select(q => (object?)multi.Expectation(q[0], Single(q[1]), q[2])).ToArray());
            check.Output(
                c,
                "stdDeviationArray",
                points.Select(q => (object?)Rows(multi.StdDeviation(q[0], Single(q[1]), q[2]))).ToArray());
            check.Output(
                c,
                "covarianceArray",
                points.Select(q => (object?)Rows(multi.Covariance(q[0], Single(q[1]), q[2]))).ToArray());
            check.Output(
                c,
                "evolveArray",
                points.Select(q => (object?)multi.Evolve(q[0], Single(q[1]), q[2], Single(q[3]))).ToArray());
        }

        check.Complete();
    }

    // The vectors' process spec: {"type": "OrnsteinUhlenbeckProcess", "speed", "volatility", "x0", "level"}.
    internal static OrnsteinUhlenbeckProcess Process(JsonElement spec)
    {
        string type = spec.GetProperty("type").GetString()!;
        if (type != "OrnsteinUhlenbeckProcess")
        {
            throw new InvalidOperationException($"unknown process {type}");
        }

        return new OrnsteinUhlenbeckProcess(
            Number(spec.GetProperty("speed")),
            Number(spec.GetProperty("volatility")),
            Number(spec.GetProperty("x0")),
            Number(spec.GetProperty("level")));
    }

    // A matrix as its rows.
    internal static object?[] Rows(Matrix m) =>
        [.. Enumerable.Range(0, m.Rows).Select(i => (object?)Enumerable.Range(0, m.Columns).Select(j => (object?)m[i, j]).ToArray())];

    private static Vector Single(double x) => [x];
}
