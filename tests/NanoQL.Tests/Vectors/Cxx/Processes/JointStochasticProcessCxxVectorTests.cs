// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The JointStochasticProcess harness vectors, <c>tests/vectors/cxx/processes/jointstochasticprocess.json</c>: the
/// harness's own derived process, mirrored here, over a geometric Brownian motion, a Heston process and an
/// Ornstein-Uhlenbeck process with a cross-model correlation, with its default factors and with fewer: size, factors,
/// initial values, drift, diffusion, numeraire, expectation, covariance, standard deviation, apply and evolve; and the
/// refusal of more factors than the size.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class JointStochasticProcessCxxVectorTests
{
    [Fact]
    public void JointStochasticProcessCxxVector()
    {
        var file = VectorFile.Load("cxx/processes/jointstochasticprocess");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (!c.Inputs.TryGetProperty("times", out _))
            {
                check.Output(c, "size", () => ProcessOf(c.Input("process")).Size);
                continue;
            }

            JsonElement spec = c.Input("process");
            HarnessJointProcess p = ProcessOf(spec);
            check.Output(c, "size", p.Size);
            check.Output(c, "factors", p.Factors);
            check.Output(c, "initialValues", p.InitialValues().ToArray());

            double[] times = Doubles(c.Input("times"));
            Vector[] states = [.. c.Input("states").EnumerateArray().Select(VectorOf)];
            double[] steps = Doubles(c.Input("steps"));
            Vector[] draws = [.. c.Input("draws").EnumerateArray().Select(VectorOf)];
            Vector[] increments = [.. c.Input("increments").EnumerateArray().Select(VectorOf)];
            (double T, Vector X)[] points = [.. times.SelectMany(t => states.Select(x => (t, x)))];
            (double T, Vector X, double Dt)[] stepped = [.. points.SelectMany(q => steps.Select(dt => (q.T, q.X, dt)))];
            check.Output(c, "drift", points.Select(q => Lazy(() => p.Drift(q.T, q.X).ToArray())).ToArray());
            check.Output(c, "diffusion", points.Select(q => Lazy(() => Flat(p.Diffusion(q.T, q.X)))).ToArray());
            check.Output(c, "numeraire", points.Select(q => Lazy(() => p.Numeraire(q.T, q.X))).ToArray());
            check.Output(c, "expectation", stepped.Select(q => Lazy(() => p.Expectation(q.T, q.X, q.Dt).ToArray())).ToArray());
            check.Output(c, "covariance", stepped.Select(q => Lazy(() => Flat(p.Covariance(q.T, q.X, q.Dt)))).ToArray());
            check.Output(c, "stdDeviation", stepped.Select(q => Lazy(() => Flat(p.StdDeviation(q.T, q.X, q.Dt)))).ToArray());
            check.Output(
                c, "apply", states.SelectMany(x => increments.Select(dx => Lazy(() => p.Apply(x, dx).ToArray()))).ToArray());

            // each evolve on a fresh process, as the harness's
            check.Output(
                c,
                "evolve",
                stepped.SelectMany(q => draws.Select(dw => Lazy(() => ProcessOf(spec).Evolve(q.T, q.X, q.Dt, dw).ToArray()))).ToArray());
        }

        check.Complete();
    }

    // The harness's process spec, its constituents in its order.
    private static HarnessJointProcess ProcessOf(JsonElement spec)
    {
        double[] gbm = Doubles(spec.GetProperty("gbm")), ou = Doubles(spec.GetProperty("ou"));
        double[][] rows = [.. spec.GetProperty("correlation").EnumerateArray().Select(Doubles)];
        var correlation = new Matrix(rows.Length, rows.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            for (int j = 0; j < rows.Length; j++)
            {
                correlation[i, j] = rows[i][j];
            }
        }

        JsonElement factors = spec.GetProperty("factors");
        return new HarnessJointProcess(
            [
                new GeometricBrownianMotionProcess(gbm[0], gbm[1], gbm[2]),
                HestonCxxInputs.ProcessOf(spec.GetProperty("heston")),
                new OrnsteinUhlenbeckProcess(ou[0], ou[1], ou[2], ou[3]),
            ],
            factors.ValueKind == JsonValueKind.Null ? null : factors.GetInt32(),
            correlation,
            spec.GetProperty("stateDependent").GetBoolean());
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

    // tools/cxx/src/processes/jointstochasticprocess.cpp's HarnessJointProcess: preEvolve does nothing; postEvolve adds
    // 1e-3 times the first correlated increment to the first state; the numeraire exp(0.03 t); the correlation given.
    private sealed class HarnessJointProcess(
        ImmutableArray<StochasticProcess> l, int? factors, Matrix correlation, bool stateDependent)
        : JointStochasticProcess(l, factors)
    {
        public override void PreEvolve(double t0, Vector x0, double dt, Vector dw)
        {
        }

        public override Vector PostEvolve(double t0, Vector x0, double dt, Vector dw, Vector y0)
        {
            Vector y = y0.Clone();
            y[0] += 1e-3 * dw[0];
            return y;
        }

        public override double Numeraire(double t, Vector x) => Math.Exp(0.03 * t);

        public override bool CorrelationIsStateDependent() => stateDependent;

        public override Matrix CrossModelCorrelation(double t0, Vector x0) => correlation.Clone();
    }
}
