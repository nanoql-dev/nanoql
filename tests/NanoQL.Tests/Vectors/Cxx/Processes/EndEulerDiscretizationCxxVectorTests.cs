// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's end-point Euler vectors, <c>tests/vectors/cxx/processes/endeulerdiscretization.json</c>: the
/// drift, diffusion and variance on the harness's Black-Scholes-Merton process, whose local volatility moves with time,
/// and the drift, diffusion and covariance on its Heston process, the matrices by rows, read through the
/// multi-dimensional discretization it converts to.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EndEulerDiscretizationCxxVectorTests
{
    [Fact]
    public void EndEulerDiscretizationCxxVector()
    {
        var file = VectorFile.Load("cxx/processes/endeulerdiscretization");
        var check = new VectorAssert(file);
        var e = new EndEulerDiscretization();
        StochasticProcessDiscretization multi = e;
        foreach (VectorCase c in file.Cases)
        {
            Date today = c.Context().EvaluationDate;
            DayCounter dc = new Actual365Fixed();
            double t0 = c.Double("t0"), dt = c.Double("dt");
            if (c.String("call") == "1d")
            {
                var p = new BlackScholesMertonProcess(
                    100.0,
                    new FlatForward(today, 0.01, dc),
                    new FlatForward(today, 0.03, dc),
                    new BlackVarianceCurve(today, [today + 90, today + 365, today + 730], [0.15, 0.25, 0.30], dc, false));
                double x0 = c.Double("x0");
                check.Output(c, "drift", e.Drift(p, t0, x0, dt));
                check.Output(c, "diffusion", e.Diffusion(p, t0, x0, dt));
                check.Output(c, "variance", e.Variance(p, t0, x0, dt));
            }
            else
            {
                var p = new HestonProcess(new FlatForward(today, 0.03, dc), new FlatForward(today, 0.01, dc), 100.0, 0.04, 1.5, 0.06, 0.4, -0.6);
                var x0 = new Vector(c.Doubles("x0"));
                check.Output(c, "drift", multi.Drift(p, t0, x0, dt).ToArray());
                check.Output(c, "diffusion", Rows(multi.Diffusion(p, t0, x0, dt)));
                check.Output(c, "covariance", Rows(multi.Covariance(p, t0, x0, dt)));
            }
        }

        check.Complete();
    }

    private static double[] Rows(Matrix m) => [.. Enumerable.Range(0, m.Rows).SelectMany(i => Enumerable.Range(0, m.Columns).Select(j => m[i, j]))];
}
