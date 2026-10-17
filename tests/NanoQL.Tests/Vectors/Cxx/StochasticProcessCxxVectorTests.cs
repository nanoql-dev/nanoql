// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.ArrayVectorTests;
using static NanoQL.Tests.Vectors.StochasticProcessVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's StochasticProcess vectors, <c>tests/vectors/cxx/stochasticprocess.json</c>: on
/// Ornstein-Uhlenbeck processes, the vector form of <see cref="StochasticProcess.Apply"/> that
/// <see cref="StochasticProcess1D"/> implements, and the base's <see cref="StochasticProcess.Time"/>, which refuses;
/// and the base's own defaults (<see cref="StochasticProcess.Evolve"/>, <see cref="StochasticProcess.Apply"/>,
/// <see cref="StochasticProcess.Factors"/>) on the harness's multi-dimensional process, which
/// <see cref="LinearProcess"/> repeats.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StochasticProcessCxxVectorTests
{
    [Fact]
    public void StochasticProcessCxxVector()
    {
        var file = VectorFile.Load("cxx/stochasticprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("process");
            StochasticProcess p = spec.GetProperty("type").GetString() == "LinearProcess" ? Linear(spec) : Process(spec);
            var date = new Date(c.Int("date"));
            check.Output(c, "apply", c.Input("applyPoints").EnumerateArray().Select(q =>
            {
                JsonElement[] xs = [.. q.EnumerateArray()];
                return (object?)p.Apply(new Vector(Numbers(xs[0])), new Vector(Numbers(xs[1])));
            }).ToArray());
            check.Output(c, "time", () => p.Time(date));
            if (p is StochasticProcess1D)
            {
                continue;
            }

            check.Output(c, "size", p.Size);
            check.Output(c, "factors", p.Factors);
            JsonElement[][] evolve = [.. c.Input("evolvePoints").EnumerateArray().Select(q => q.EnumerateArray().ToArray())];
            check.Output(c, "evolve", evolve.Select(q => (object?)Evolve(p, q, 0)).ToArray());
            JsonElement[] firstApply = [.. c.Input("applyPoints")[0].EnumerateArray()];
            check.Output(c, "applyWrongSize", () =>
                p.Apply(new Vector(Numbers(firstApply[0])), new Vector([.. Numbers(firstApply[1]), 0.1])));
            check.Output(c, "evolveWrongSize", () => Evolve(p, evolve[0], 0.3));
        }

        check.Complete();
    }

    // Evolve at a point (t0, x0, dt, dw), with an extra increment when extra isn't 0.
    private static Vector Evolve(StochasticProcess p, JsonElement[] q, double extra)
    {
        double[] dw = Numbers(q[3]);
        return p.Evolve(
            Number(q[0]),
            new Vector(Numbers(q[1])),
            Number(q[2]),
            extra != 0.0 ? new Vector([.. dw, extra]) : new Vector(dw));
    }

    private static LinearProcess Linear(JsonElement spec)
    {
        double[][] rows = [.. spec.GetProperty("sigma").EnumerateArray().Select(Numbers)];
        var sigma = new Matrix(rows.Length, rows.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            for (int j = 0; j < rows.Length; j++)
            {
                sigma[i, j] = rows[i][j];
            }
        }

        return new LinearProcess(new Vector(Numbers(spec.GetProperty("x0"))), new Vector(Numbers(spec.GetProperty("a"))), sigma);
    }

    // The harness's process: n independent geometric drifts x_i' = a_i x_i with a constant n x n diffusion sigma; its
    // expectation is x0_i exp(a_i dt), its standard deviation sigma sqrt(dt) and its covariance that times its
    // transpose, each hard-coded, so that the base's Evolve, Apply and Factors run as QuantLib's do.
    private sealed class LinearProcess : StochasticProcess
    {
        private readonly Vector x0;
        private readonly Vector a;
        private readonly Matrix sigma;

        public LinearProcess(Vector x0, Vector a, Matrix sigma)
        {
            this.x0 = x0.Clone();
            this.a = a.Clone();
            this.sigma = sigma.Clone();
        }

        public override int Size => x0.Size;

        public override Vector InitialValues() => x0.Clone();

        public override Vector Drift(double t, Vector x)
        {
            var r = new Vector(x.Size);
            for (int i = 0; i < x.Size; ++i)
            {
                r[i] = a[i] * x[i];
            }

            return r;
        }

        public override Matrix Diffusion(double t, Vector x) => sigma.Clone();

        public override Vector Expectation(double t0, Vector x0, double dt)
        {
            var r = new Vector(x0.Size);
            for (int i = 0; i < x0.Size; ++i)
            {
                r[i] = x0[i] * Math.Exp(a[i] * dt);
            }

            return r;
        }

        public override Matrix StdDeviation(double t0, Vector x0, double dt) => sigma * Math.Sqrt(dt);

        public override Matrix Covariance(double t0, Vector x0, double dt)
        {
            Matrix s = StdDeviation(t0, x0, dt);
            return s * MatrixFunctions.Transpose(s);
        }
    }
}
