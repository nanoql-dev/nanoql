// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The HestonProcess vectors, <c>tests/vectors/processes/hestonprocess.json</c>: three processes on flat and zero
/// curves under each of the nine discretizations, their size, factors, initial values, drift and diffusion, the Euler
/// discretization's expectation, standard deviation and covariance, and evolve; and the Broadie-Kaya Gauss-Lobatto
/// scheme's price at an input where QuantLib's own is steady, which the file leaves out.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HestonProcessVectorTests
{
    [Fact]
    public void BroadieKayaGaussLobattoPrice()
    {
        // the vector file holds this scheme's variance alone: QuantLib's price jumps among its solve's branches under
        // last-bit moves, and the port's complex functions differ from the C runtime's in the last bits (SEQR6). At
        // this input, the high volatility of variance on flat curves from (100, 0.04) over 0.05 with no draw,
        // QuantLib's price (the wheel) moves 2.7e-10 under every one-ulp and 1e-12 relative move of the parameters, the
        // state, the step and the draws: the port within 1e-9 of it
        var today = new Date(15, Month.October, 2026);
        var curve = new FlatForward(today, 0.04, new Actual365Fixed());
        var dividends = new FlatForward(today, 0.02, new Actual365Fixed());
        var p = new HestonProcess(curve, dividends, 50.0, 0.09, 0.8, 0.06, 1.2, 0.4, HestonProcessDiscretization.BroadieKayaExactSchemeLobatto);
        double price = p.Evolve(0.0, new Vector([100.0, 0.04]), 0.05, new Vector([0.0, 0.0, 0.0]))[0];
        Assert.Equal(0.0, (price / 99.34328413665247) - 1.0, 1e-9);
    }

    [Fact]
    public void HestonProcessVector()
    {
        var file = VectorFile.Load("processes/hestonprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            HestonProcess p = HestonProcessOf(c.Input("process"));
            check.Output(c, "size", p.Size);
            check.Output(c, "factors", p.Factors);
            check.Output(c, "initialValues", p.InitialValues().ToArray());

            double[] times = Doubles(c.Input("times"));
            Vector[] states = [.. c.Input("states").EnumerateArray().Select(VectorOf)];
            (double T, Vector X)[] points = [.. times.SelectMany(t => states.Select(x => (t, x)))];
            check.Output(c, "drift", points.Select(q => Lazy(() => p.Drift(q.T, q.X).ToArray())).ToArray());
            check.Output(c, "diffusion", points.Select(q => Lazy(() => Flat(p.Diffusion(q.T, q.X)))).ToArray());

            (double T, Vector X, double Dt)[] euler =
            [
                .. c.Input("euler").EnumerateArray().Select(e => (e[0].GetDouble(), VectorOf(e[1]), e[2].GetDouble())),
            ];
            check.Output(c, "expectation", euler.Select(e => Lazy(() => p.Expectation(e.T, e.X, e.Dt).ToArray())).ToArray());
            check.Output(c, "stdDeviation", euler.Select(e => Lazy(() => Flat(p.StdDeviation(e.T, e.X, e.Dt)))).ToArray());
            check.Output(c, "covariance", euler.Select(e => Lazy(() => Flat(p.Covariance(e.T, e.X, e.Dt)))).ToArray());

            (Vector X, double Dt, Vector Dw)[] evolves =
            [
                .. c.Input("evolve").EnumerateArray().Select(e => (VectorOf(e[0]), e[1].GetDouble(), VectorOf(e[2]))),
            ];
            if (c.Expected.TryGetProperty("evolveVariance", out _))
            {
                // Broadie and Kaya's Gauss-Lobatto scheme: the variance alone, its price QuantLib's own solve's branch
                check.Output(c, "evolveVariance", evolves.Select(e => Lazy(() => p.Evolve(0.0, e.X, e.Dt, e.Dw)[1])).ToArray());
            }
            else
            {
                check.Output(c, "evolve", evolves.Select(e => Lazy(() => p.Evolve(0.0, e.X, e.Dt, e.Dw).ToArray())).ToArray());
            }
        }

        check.Complete();
    }

    // A process spec, its discretization QuantLib's integer.
    internal static HestonProcess HestonProcessOf(JsonElement spec) =>
        new(
            CurveOf(spec.GetProperty("riskFree")),
            CurveOf(spec.GetProperty("dividend")),
            spec.GetProperty("s0").GetDouble(),
            spec.GetProperty("v0").GetDouble(),
            spec.GetProperty("kappa").GetDouble(),
            spec.GetProperty("theta").GetDouble(),
            spec.GetProperty("sigma").GetDouble(),
            spec.GetProperty("rho").GetDouble(),
            spec.TryGetProperty("discretization", out JsonElement d)
                ? (HestonProcessDiscretization)d.GetInt32()
                : HestonProcessDiscretization.QuadraticExponentialMartingale);

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
