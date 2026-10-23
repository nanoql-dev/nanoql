// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The OrnsteinUhlenbeckProcess vectors, <c>tests/vectors/processes/ornsteinuhlenbeckprocess.json</c>:
/// <see cref="OrnsteinUhlenbeckProcess"/>'s accessors, and its drift, diffusion, expectation, standard deviation and
/// variance at 20 points per process, with the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OrnsteinUhlenbeckProcessVectorTests
{
    [Fact]
    public void OrnsteinUhlenbeckProcessVector()
    {
        var file = VectorFile.Load("processes/ornsteinuhlenbeckprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double speed = c.Double("speed"), vol = Number(c.Input("volatility"));
            if (!c.Inputs.TryGetProperty("points", out _))
            {
                check.Output(c, "x0", () => new OrnsteinUhlenbeckProcess(speed, vol).X0);
                continue;
            }

            // The case leaves out x0 and level where it takes QuantLib's defaults.
            OrnsteinUhlenbeckProcess p = (c.Inputs.TryGetProperty("x0", out _), c.Inputs.TryGetProperty("level", out _)) switch
            {
                (false, false) => new OrnsteinUhlenbeckProcess(speed, vol),
                (true, false) => new OrnsteinUhlenbeckProcess(speed, vol, c.Double("x0")),
                (true, true) => new OrnsteinUhlenbeckProcess(speed, vol, c.Double("x0"), c.Double("level")),
                _ => throw new InvalidOperationException($"{c.Name}: a level without an x0"),
            };
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Numbers)];
            check.Output(c, "x0", p.X0);
            check.Output(c, "speed", p.Speed);
            check.Output(c, "volatility", p.Volatility);
            check.Output(c, "level", p.Level);
            check.Output(c, "drift", points.Select(q => (object?)p.Drift(q[0], q[1])).ToArray());
            check.Output(c, "diffusion", points.Select(q => (object?)p.Diffusion(q[0], q[1])).ToArray());
            check.Output(c, "expectation", points.Select(q => (object?)p.Expectation(q[0], q[1], q[2])).ToArray());

            // ornsteinuhlenbeckprocess.py leaves these out at the square root of machine epsilon and below it.
            if (c.Expected.TryGetProperty("variance", out _))
            {
                check.Output(c, "stdDeviation", points.Select(q => (object?)p.StdDeviation(q[0], q[1], q[2])).ToArray());
                check.Output(c, "variance", points.Select(q => (object?)p.Variance(q[0], q[1], q[2])).ToArray());
            }
        }

        check.Complete();
    }
}
