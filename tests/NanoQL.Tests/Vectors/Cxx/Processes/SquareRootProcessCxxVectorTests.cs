// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's SquareRootProcess vectors, <c>tests/vectors/cxx/processes/squarerootprocess.json</c>:
/// <see cref="SquareRootProcess"/>, which the wheel doesn't expose: its x0, a, b and sigma, and its drift, diffusion,
/// Euler expectation, standard deviation, variance and evolved state at up to 20 points per process, those at a
/// negative state left out (<c>Divergences/SquareRootProcessTruncationTests.cs</c>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SquareRootProcessCxxVectorTests
{
    [Fact]
    public void SquareRootProcessCxxVector()
    {
        var file = VectorFile.Load("cxx/processes/squarerootprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double b = c.Double("b"), a = c.Double("a"), sigma = c.Double("sigma");
            SquareRootProcess p = c.Inputs.TryGetProperty("x0", out _)
                ? new SquareRootProcess(b, a, sigma, c.Double("x0"))
                : new SquareRootProcess(b, a, sigma);
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Numbers)];
            check.Output(c, "x0", p.X0);
            check.Output(c, "a", p.A);
            check.Output(c, "b", p.B);
            check.Output(c, "sigma", p.Sigma);
            check.Output(c, "drift", points.Select(q => (object?)p.Drift(q[0], q[1])).ToArray());
            check.Output(c, "diffusion", points.Select(q => (object?)p.Diffusion(q[0], q[1])).ToArray());
            check.Output(c, "expectation", points.Select(q => (object?)p.Expectation(q[0], q[1], q[2])).ToArray());
            check.Output(c, "stdDeviation", points.Select(q => (object?)p.StdDeviation(q[0], q[1], q[2])).ToArray());
            check.Output(c, "variance", points.Select(q => (object?)p.Variance(q[0], q[1], q[2])).ToArray());
            check.Output(c, "evolve", points.Select(q => (object?)p.Evolve(q[0], q[1], q[2], q[3])).ToArray());
        }

        check.Complete();
    }
}
