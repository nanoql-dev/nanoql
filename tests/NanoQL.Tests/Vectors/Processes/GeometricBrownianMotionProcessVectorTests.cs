// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The GeometricBrownianMotionProcess vectors, <c>tests/vectors/processes/geometricbrownianprocess.json</c>:
/// <see cref="GeometricBrownianMotionProcess"/>'s x0, and its drift, diffusion, Euler expectation, standard deviation,
/// variance, evolved state and apply at 20 points per process.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GeometricBrownianMotionProcessVectorTests
{
    [Fact]
    public void GeometricBrownianMotionProcessVector()
    {
        var file = VectorFile.Load("processes/geometricbrownianprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var p = new GeometricBrownianMotionProcess(c.Double("initialValue"), c.Double("mue"), c.Double("sigma"));
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Numbers)];
            check.Output(c, "x0", p.X0);
            check.Output(c, "drift", points.Select(q => (object?)p.Drift(q[0], q[1])).ToArray());
            check.Output(c, "diffusion", points.Select(q => (object?)p.Diffusion(q[0], q[1])).ToArray());
            check.Output(c, "expectation", points.Select(q => (object?)p.Expectation(q[0], q[1], q[2])).ToArray());
            check.Output(c, "stdDeviation", points.Select(q => (object?)p.StdDeviation(q[0], q[1], q[2])).ToArray());
            check.Output(c, "variance", points.Select(q => (object?)p.Variance(q[0], q[1], q[2])).ToArray());
            check.Output(c, "evolve", points.Select(q => (object?)p.Evolve(q[0], q[1], q[2], q[3])).ToArray());
            check.Output(c, "apply", points.Select(q => (object?)p.Apply(q[1], q[3])).ToArray());
        }

        check.Complete();
    }
}
