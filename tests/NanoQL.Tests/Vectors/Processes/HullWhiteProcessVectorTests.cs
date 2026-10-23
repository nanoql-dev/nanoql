// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Hull-White process vectors, <c>tests/vectors/processes/hullwhiteprocess.json</c>: <see cref="HullWhiteProcess"/>
/// and <see cref="HullWhiteForwardProcess"/> at three forward measure times, on flat and bootstrapped curves
/// (<see cref="ModelInputs"/>): x0, the drift and diffusion, the step's expectation, standard deviation and variance,
/// evolve and apply; and the forward process's α, B and M_T.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HullWhiteProcessVectorTests
{
    [Fact]
    public void HullWhiteProcessVector()
    {
        var file = VectorFile.Load("processes/hullwhiteprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            YieldTermStructure curve = ModelInputs.CurveOf(c.Input("curve"), context);
            double a = c.Double("a"), sigma = c.Double("sigma");
            JsonElement forwardTime = c.Input("forwardMeasureTime");
            StochasticProcess1D process = forwardTime.ValueKind == JsonValueKind.Null
                ? new HullWhiteProcess(curve, a, sigma)
                : new HullWhiteForwardProcess(curve, a, sigma).WithForwardMeasureTime(Number(forwardTime));
            CheckProcess(check, c, process);
            if (process is HullWhiteForwardProcess forward && c.Inputs.TryGetProperty("alphaTimes", out _))
            {
                check.Output(c, "alpha", Numbers(c.Input("alphaTimes")).Select(t => (object?)forward.Alpha(t)).ToArray());
                check.Output(c, "B", ModelInputs.Points(c, "bPoints").Select(p => (object?)forward.B(p[0], p[1])).ToArray());
                check.Output(c, "M_T", ModelInputs.Points(c, "mTPoints").Select(p => (object?)forward.M_T(p[0], p[1], p[2])).ToArray());
            }
        }

        check.Complete();
    }

    /// <summary>
    /// Compares a one-dimensional process's x0, drift, diffusion, expectation, standard deviation, variance, evolve and
    /// apply at the case's points.
    /// </summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="p">The process.</param>
    internal static void CheckProcess(VectorAssert check, VectorCase c, StochasticProcess1D p)
    {
        double[][] processPoints = ModelInputs.Points(c, "processPoints");
        double[][] stepPoints = ModelInputs.Points(c, "stepPoints");
        check.Output(c, "x0", p.X0);
        check.Output(c, "drift", processPoints.Select(x => (object?)p.Drift(x[0], x[1])).ToArray());
        check.Output(c, "diffusion", processPoints.Select(x => (object?)p.Diffusion(x[0], x[1])).ToArray());
        check.Output(c, "expectation", stepPoints.Select(s => (object?)p.Expectation(s[0], s[1], s[2])).ToArray());
        check.Output(c, "stdDeviation", stepPoints.Select(s => (object?)p.StdDeviation(s[0], s[1], s[2])).ToArray());
        check.Output(c, "variance", stepPoints.Select(s => (object?)p.Variance(s[0], s[1], s[2])).ToArray());
        check.Output(c, "evolve", ModelInputs.Points(c, "evolvePoints").Select(e => (object?)p.Evolve(e[0], e[1], e[2], e[3])).ToArray());
        check.Output(c, "apply", ModelInputs.Points(c, "applyPoints").Select(x => (object?)p.Apply(x[0], x[1])).ToArray());
    }
}
