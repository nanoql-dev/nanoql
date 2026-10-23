// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's CoxIngersollRossProcess vectors, <c>tests/vectors/cxx/processes/coxingersollrossprocess.json</c>:
/// <see cref="CoxIngersollRossProcess"/>, which the wheel doesn't expose: its x0, speed, volatility and level, and its
/// drift, expectation and quadratic-exponential evolve at 20 points per process; its diffusion, variance and standard
/// deviation left out (<c>Divergences/CirProcessMomentsTests.cs</c>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CoxIngersollRossProcessCxxVectorTests
{
    [Fact]
    public void CoxIngersollRossProcessCxxVector()
    {
        var file = VectorFile.Load("cxx/processes/coxingersollrossprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double speed = c.Double("speed"), vol = c.Double("volatility");
            bool hasX0 = c.Inputs.TryGetProperty("x0", out _), hasLevel = c.Inputs.TryGetProperty("level", out _);
            CoxIngersollRossProcess p = hasX0 && hasLevel ? new CoxIngersollRossProcess(speed, vol, c.Double("x0"), c.Double("level"))
                : hasX0 ? new CoxIngersollRossProcess(speed, vol, c.Double("x0"))
                : new CoxIngersollRossProcess(speed, vol);
            double[][] points = [.. c.Input("points").EnumerateArray().Select(Numbers)];
            check.Output(c, "x0", p.X0);
            check.Output(c, "speed", p.Speed);
            check.Output(c, "volatility", p.Volatility);
            check.Output(c, "level", p.Level);
            check.Output(c, "drift", points.Select(q => (object?)p.Drift(q[0], q[1])).ToArray());
            check.Output(c, "expectation", points.Select(q => (object?)p.Expectation(q[0], q[1], q[2])).ToArray());
            check.Output(c, "evolve", points.Select(q => (object?)p.Evolve(q[0], q[1], q[2], q[3])).ToArray());
        }

        check.Complete();
    }
}
