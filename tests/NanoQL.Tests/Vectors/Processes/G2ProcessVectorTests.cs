// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The G2 process vectors, <c>tests/vectors/processes/g2process.json</c>: <see cref="G2Process"/> and
/// <see cref="G2ForwardProcess"/> at two forward measure times, with no curve and on a flat one: the size, factors and
/// initial values, φ (refused with no curve), the short rate, the drift and diffusion, the step's expectation, its
/// standard deviation and covariance on a nonzero step, and evolve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class G2ProcessVectorTests
{
    [Fact]
    public void G2ProcessVector()
    {
        var file = VectorFile.Load("processes/g2process");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            double[] p = c.Doubles("params");
            JsonElement curveSpec = c.Input("curve");
            YieldTermStructure? curve = curveSpec.ValueKind == JsonValueKind.Null ? null : ModelInputs.CurveOf(curveSpec, context);
            JsonElement forwardTime = c.Input("forwardMeasureTime");
            StochasticProcess process;
            Func<double, double> phi;
            Func<double, double, double, double> shortRate;
            if (forwardTime.ValueKind == JsonValueKind.Null)
            {
                var g2 = new G2Process(p[0], p[1], p[2], p[3], p[4], curve);
                (process, phi, shortRate) = (g2, g2.Phi, g2.ShortRate);
            }
            else
            {
                G2ForwardProcess g2 = new G2ForwardProcess(p[0], p[1], p[2], p[3], p[4], curve).WithForwardMeasureTime(Number(forwardTime));
                (process, phi, shortRate) = (g2, g2.Phi, g2.ShortRate);
            }

            check.Output(c, "size", process.Size);
            check.Output(c, "factors", process.Factors);
            check.Output(c, "initialValues", ModelInputs.Elements(process.InitialValues()));
            check.Output(c, "phi", Numbers(c.Input("phiTimes")).Select(t => (Func<object?>)(() => phi(t))).ToArray());
            check.Output(c, "shortRate", ModelInputs.Points(c, "shortRatePoints").Select(s => (object?)shortRate(s[0], s[1], s[2])).ToArray());

            (double T, Vector X)[] points = [.. c.Input("processPoints").EnumerateArray().Select(e => (Number(e[0]), ModelInputs.VectorOf(e[1])))];
            check.Output(c, "drift", points.Select(x => (object?)ModelInputs.Elements(process.Drift(x.T, x.X))).ToArray());
            check.Output(c, "diffusion", points.Select(x => (object?)Rows(process.Diffusion(x.T, x.X))).ToArray());

            (double T0, Vector X0, double Dt)[] steps =
                [.. c.Input("stepPoints").EnumerateArray().Select(e => (Number(e[0]), ModelInputs.VectorOf(e[1]), Number(e[2])))];
            check.Output(c, "expectation", steps.Select(s => (object?)ModelInputs.Elements(process.Expectation(s.T0, s.X0, s.Dt))).ToArray());
            (double T0, Vector X0, double Dt)[] nonzero =
                [.. c.Input("nonzeroSteps").EnumerateArray().Select(e => (Number(e[0]), ModelInputs.VectorOf(e[1]), Number(e[2])))];
            check.Output(c, "stdDeviation", nonzero.Select(s => (object?)Rows(process.StdDeviation(s.T0, s.X0, s.Dt))).ToArray());
            check.Output(c, "covariance", nonzero.Select(s => (object?)Rows(process.Covariance(s.T0, s.X0, s.Dt))).ToArray());

            check.Output(c, "evolve", c.Input("evolvePoints").EnumerateArray().Select(e => (object?)ModelInputs.Elements(
                process.Evolve(Number(e[0]), ModelInputs.VectorOf(e[1]), Number(e[2]), ModelInputs.VectorOf(e[3])))).ToArray());
        }

        check.Complete();
    }

    private static object?[] Rows(Matrix m) =>
        [.. Enumerable.Range(0, m.Rows).Select(i => (object?)Enumerable.Range(0, m.Columns).Select(j => (object?)m[i, j]).ToArray())];
}
