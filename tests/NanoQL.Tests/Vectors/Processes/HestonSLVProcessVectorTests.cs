// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The HestonSLVProcess vectors, <c>tests/vectors/processes/hestonslvprocess.json</c>: the Heston stochastic-local
/// volatility process on three Heston processes on flat curves and one on zero curves, with constant and surface
/// leverage functions and mixing factors 1 and 0.5, its size, factors, initial values, drift, diffusion and evolve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HestonSLVProcessVectorTests
{
    [Fact]
    public void HestonSLVProcessVector()
    {
        var file = VectorFile.Load("processes/hestonslvprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("process");
            var p = new HestonSLVProcess(
                HestonProcessVectorTests.HestonProcessOf(spec.GetProperty("heston")),
                FdHestonVanillaEngineVectorTests.LeverageOf(spec.GetProperty("leverage"))!,
                spec.GetProperty("mixingFactor").GetDouble());
            check.Output(c, "size", p.Size);
            check.Output(c, "factors", p.Factors);
            check.Output(c, "initialValues", p.InitialValues().ToArray());

            double[] times = Doubles(c.Input("times"));
            Vector[] states = [.. c.Input("states").EnumerateArray().Select(x => new Vector(Doubles(x)))];
            (double T, Vector X)[] points = [.. times.SelectMany(t => states.Select(x => (t, x)))];
            check.Output(c, "drift", points.Select(q => Lazy(() => p.Drift(q.T, q.X).ToArray())).ToArray());
            check.Output(c, "diffusion", points.Select(q => Lazy(() => Flat(p.Diffusion(q.T, q.X)))).ToArray());

            (double T, Vector X, double Dt, Vector Dw)[] evolves =
            [
                .. c.Input("evolve").EnumerateArray()
                    .Select(e => (e[0].GetDouble(), new Vector(Doubles(e[1])), e[2].GetDouble(), new Vector(Doubles(e[3])))),
            ];
            check.Output(c, "evolve", evolves.Select(e => Lazy(() => p.Evolve(e.T, e.X, e.Dt, e.Dw).ToArray())).ToArray());
        }

        check.Complete();
    }

    private static double[] Flat(Matrix m)
    {
        double[] flat = new double[m.Rows * m.Columns];
        for (int i = 0; i < m.Rows; i++)
        {
            for (int j = 0; j < m.Columns; j++)
            {
                flat[(i * m.Columns) + j] = m[i, j];
            }
        }

        return flat;
    }
}
