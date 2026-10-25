// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The HybridHestonHullWhiteProcess harness vectors, <c>tests/vectors/cxx/processes/hybridhestonhullwhiteprocess.json</c>:
/// two Heston processes with two forward Hull-White processes at two correlations under both discretizations: size,
/// factors, initial values and eta, drift, diffusion, evolve, the in-place evolve read against the same outputs (the
/// S-hes plan's HR2), and the numeraire; and the refusal of correlations whose squares sum past 1.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HybridHestonHullWhiteProcessCxxVectorTests
{
    [Fact]
    public void HybridHestonHullWhiteProcessCxxVector()
    {
        var file = VectorFile.Load("cxx/processes/hybridhestonhullwhiteprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (!c.Inputs.TryGetProperty("times", out _))
            {
                check.Output(c, "size", () => HybridHestonHullWhiteCxxInputs.ProcessOf(c.Input("process")).Size);
                continue;
            }

            HybridHestonHullWhiteProcess p = HybridHestonHullWhiteCxxInputs.ProcessOf(c.Input("process"));
            check.Output(c, "size", p.Size);
            check.Output(c, "factors", p.Factors);
            double[] initial = new double[p.Size];
            p.InitialValues(initial);
            Assert.Equal(p.InitialValues().ToArray(), initial);
            check.Output(c, "initialValues", initial);
            check.Output(c, "eta", p.Eta);

            double[] times = Doubles(c.Input("times"));
            Vector[] states = [.. c.Input("states").EnumerateArray().Select(VectorOf)];
            (double T, Vector X)[] points = [.. times.SelectMany(t => states.Select(x => (t, x)))];
            check.Output(c, "drift", points.Select(q => Lazy(() => p.Drift(q.T, q.X).ToArray())).ToArray());
            check.Output(c, "diffusion", points.Select(q => Lazy(() => Flat(p.Diffusion(q.T, q.X)))).ToArray());
            check.Output(c, "numeraire", points.Select(q => Lazy(() => p.Numeraire(q.T, q.X))).ToArray());

            (double T0, Vector X, double Dt, Vector Dw)[] evolves =
            [
                .. c.Input("evolve").EnumerateArray()
                    .Select(e => (e[0].GetDouble(), VectorOf(e[1]), e[2].GetDouble(), VectorOf(e[3]))),
            ];
            check.Output(c, "evolve", evolves.Select(e => Lazy(() => InPlaceEvolve(p, e.T0, e.X, e.Dt, e.Dw))).ToArray());
        }

        check.Complete();
    }

    // the in-place step, asserted equal to the allocating one bit for bit, its values for the vector
    private static double[] InPlaceEvolve(HybridHestonHullWhiteProcess p, double t0, Vector x, double dt, Vector dw)
    {
        double[] allocating = p.Evolve(t0, x, dt, dw).ToArray();
        double[] inPlace = new double[p.Size];
        p.Evolve(t0, x.AsSpan(), dt, dw.AsSpan(), inPlace);
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
