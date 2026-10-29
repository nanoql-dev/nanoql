// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The energy processes' wheel vectors, <c>tests/vectors/experimental/processes/extendedornsteinuhlenbeckprocess.json</c>:
/// <see cref="ExtendedOrnsteinUhlenbeckProcess"/> on its mid-point rule, <see cref="ExtOUWithJumpsProcess"/> and
/// <see cref="KlugeExtOUProcess"/>: their values, drifts, diffusions (the Kluge-OU process's left out, which QuantLib
/// writes past its matrix), expectations, deviations and variances, and their evolution on draws, with and without
/// jumps.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExtendedOrnsteinUhlenbeckProcessVectorTests
{
    private static readonly (double T, double X)[] Points = [(0.0, 0.5), (0.25, 3.0), (1.0, -1.0), (2.5, 2.2)];
    private static readonly (double T, double X, double Dt)[] Steps = [(0.0, 3.0, 0.01), (0.5, 2.0, 0.25), (1.0, -0.5, 1.0), (3.0, 4.0, 0.002)];
    private static readonly double[] Draws = [-1.3, 0.0, 0.7, 2.1];
    private static readonly double[][] JumpStates = [[3.0, 0.0], [2.5, 0.4], [-0.5, 1.2]];
    private static readonly double[] JumpTimes = [0.0, 0.5, 1.0];

    private static readonly (double T, double Dt, double[] Dw)[] JumpDraws =
    [
        (0.0, 0.01, [0.3, -1.0, 0.2]), (0.5, 0.1, [-0.7, 2.5, -0.4]), (1.0, 0.25, [1.1, 2.5, 1.5]), (2.0, 0.02, [0.0, 0.1, 0.0]),
    ];

    [Fact]
    public void ExtendedOrnsteinUhlenbeckProcessVector()
    {
        var file = VectorFile.Load("experimental/processes/extendedornsteinuhlenbeckprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            ExtendedOrnsteinUhlenbeckProcess ou = Extended(c.Input("process"));
            switch (c.Input("kind").GetString())
            {
                case "extended":
                    check.Output(c, "x0", ou.X0);
                    check.Output(c, "drift", Points.Select(p => ou.Drift(p.T, p.X)).ToArray());
                    check.Output(c, "diffusion", Points.Select(p => ou.Diffusion(p.T, p.X)).ToArray());
                    check.Output(c, "expectation", Steps.Select(s => ou.Expectation(s.T, s.X, s.Dt)).ToArray());
                    check.Output(c, "stdDeviation", Steps.Select(s => ou.StdDeviation(s.T, s.X, s.Dt)).ToArray());
                    check.Output(c, "variance", Steps.Select(s => ou.Variance(s.T, s.X, s.Dt)).ToArray());
                    check.Output(c, "evolve", (from s in Steps from w in Draws select ou.Evolve(s.T, s.X, s.Dt, w)).ToArray());
                    break;
                case "jumps":
                {
                    ExtOUWithJumpsProcess p = Jumps(ou, c.Input("jumps"));
                    check.Output(c, "size", p.Size);
                    check.Output(c, "factors", p.Factors);
                    check.Output(c, "initialValues", p.InitialValues().ToArray());
                    check.Output(c, "drift", JumpStates.Select((x, i) => p.Drift(JumpTimes[i], new Vector(x)).ToArray()).ToArray());
                    check.Output(c, "diffusion", JumpStates.Select((x, i) => Rows(p.Diffusion(JumpTimes[i], new Vector(x)))).ToArray());
                    check.Output(
                        c,
                        "evolve",
                        (from x in JumpStates from d in JumpDraws select p.Evolve(d.T, new Vector(x), d.Dt, new Vector(d.Dw)).ToArray()).ToArray());
                    break;
                }

                default:
                {
                    var p = new KlugeExtOUProcess(c.Input("rho").GetDouble(), Jumps(ou, c.Input("jumps")), Extended(c.Input("gas")));
                    double[][] states = [[.. JumpStates[0], 0.0], [.. JumpStates[1], 0.3], [.. JumpStates[2], -0.4]];
                    double[] extra = [0.5, -1.2, 0.0, 2.0];
                    (double T, double Dt, double[] Dw)[] draws = [.. JumpDraws.Select((d, i) => (d.T, d.Dt, (double[])[.. d.Dw, extra[i]]))];
                    check.Output(c, "size", p.Size);
                    check.Output(c, "factors", p.Factors);
                    check.Output(c, "initialValues", p.InitialValues().ToArray());
                    check.Output(c, "drift", states.Select((x, i) => p.Drift(JumpTimes[i], new Vector(x)).ToArray()).ToArray());
                    check.Output(
                        c,
                        "evolve",
                        (from x in states from d in draws select p.Evolve(d.T, new Vector(x), d.Dt, new Vector(d.Dw)).ToArray()).ToArray());
                    break;
                }
            }
        }

        check.Complete();
    }

    private static ExtendedOrnsteinUhlenbeckProcess Extended(JsonElement s) =>
        new(s.GetProperty("speed").GetDouble(), s.GetProperty("sigma").GetDouble(), s.GetProperty("x0").GetDouble(), Level(s.GetProperty("level")));

    private static ExtOUWithJumpsProcess Jumps(ExtendedOrnsteinUhlenbeckProcess ou, JsonElement j) =>
        new(ou, j.GetProperty("y0").GetDouble(), j.GetProperty("beta").GetDouble(), j.GetProperty("jumpIntensity").GetDouble(), j.GetProperty("eta").GetDouble());

    private static Func<double, double> Level(JsonElement s)
    {
        switch (s.GetProperty("type").GetString())
        {
            case "constant":
            {
                double b = s.GetProperty("b").GetDouble();
                return _ => b;
            }

            case "linear":
            {
                double c0 = s.GetProperty("c0").GetDouble(), c1 = s.GetProperty("c1").GetDouble();
                return t => c0 + (c1 * t);
            }

            default:
            {
                double a = s.GetProperty("amplitude").GetDouble(), shift = s.GetProperty("shift").GetDouble();
                return t => shift + (a * Math.Sin(t));
            }
        }
    }

    private static double[][] Rows(Matrix m) =>
        [.. Enumerable.Range(0, m.Rows).Select(i => Enumerable.Range(0, m.Columns).Select(j => m[i, j]).ToArray())];
}
