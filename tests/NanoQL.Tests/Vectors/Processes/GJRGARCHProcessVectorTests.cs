// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The GJRGARCHProcess vectors, <c>tests/vectors/processes/gjrgarchprocess.json</c>: two parameter sets on flat and zero
/// curves under the three discretizations: size, factors, initial values, drift, diffusion and evolve, the in-place
/// initial values and evolve read against the same outputs.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GJRGARCHProcessVectorTests
{
    [Fact]
    public void GJRGARCHProcessVector()
    {
        var file = VectorFile.Load("processes/gjrgarchprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            GJRGARCHProcess p = GJRGARCHProcessOf(c.Input("process"));
            check.Output(c, "size", p.Size);
            check.Output(c, "factors", p.Factors);
            double[] initial = new double[p.Size];
            p.InitialValues(initial);
            Assert.Equal(p.InitialValues().ToArray(), initial);
            check.Output(c, "initialValues", initial);

            double[] times = Doubles(c.Input("times"));
            Vector[] states = [.. c.Input("states").EnumerateArray().Select(VectorOf)];
            (double T, Vector X)[] points = [.. times.SelectMany(t => states.Select(x => (t, x)))];
            check.Output(c, "drift", points.Select(q => Lazy(() => p.Drift(q.T, q.X).ToArray())).ToArray());
            check.Output(c, "diffusion", points.Select(q => Lazy(() => Flat(p.Diffusion(q.T, q.X)))).ToArray());

            (Vector X, double Dt, Vector Dw)[] evolves =
            [
                .. c.Input("evolve").EnumerateArray().Select(e => (VectorOf(e[0]), e[1].GetDouble(), VectorOf(e[2]))),
            ];
            check.Output(c, "evolve", evolves.Select(e => Lazy(() => InPlaceEvolve(p, e.X, e.Dt, e.Dw))).ToArray());
        }

        check.Complete();
    }

    // A process spec.
    internal static GJRGARCHProcess GJRGARCHProcessOf(JsonElement spec) =>
        new(
            CurveOf(spec.GetProperty("riskFree")),
            CurveOf(spec.GetProperty("dividend")),
            spec.GetProperty("s0").GetDouble(),
            spec.GetProperty("v0").GetDouble(),
            spec.GetProperty("omega").GetDouble(),
            spec.GetProperty("alpha").GetDouble(),
            spec.GetProperty("beta").GetDouble(),
            spec.GetProperty("gamma").GetDouble(),
            spec.GetProperty("lambda").GetDouble(),
            spec.GetProperty("daysPerYear").GetDouble(),
            (GJRGARCHProcessDiscretization)spec.GetProperty("discretization").GetInt32());

    // the in-place step, asserted equal to the allocating one bit for bit, its values for the vector
    private static double[] InPlaceEvolve(GJRGARCHProcess p, Vector x, double dt, Vector dw)
    {
        double[] allocating = p.Evolve(0.0, x, dt, dw).ToArray();
        double[] inPlace = new double[p.Size];
        p.Evolve(0.0, x.AsSpan(), dt, dw.AsSpan(), inPlace);
        Assert.Equal(allocating, inPlace);
        return inPlace;
    }

    private static Vector VectorOf(JsonElement e) => new(Doubles(e));

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
