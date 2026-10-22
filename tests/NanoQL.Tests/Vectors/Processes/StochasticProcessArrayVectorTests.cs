// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The StochasticProcessArray vectors, <c>tests/vectors/processes/stochasticprocessarray.json</c>:
/// <see cref="StochasticProcessArray"/>'s size, factors and initial values, and its drift, diffusion, expectation,
/// standard deviation, covariance and evolved state at 10 points, over arrays of Ornstein-Uhlenbeck, geometric
/// Brownian and Black-Scholes-Merton processes on positive definite, singular and indefinite correlations; and the
/// constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class StochasticProcessArrayVectorTests
{
    [Fact]
    public void StochasticProcessArrayVector()
    {
        var file = VectorFile.Load("processes/stochasticprocessarray");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            ImmutableArray<StochasticProcess1D> processes = Processes(c);
            Matrix correlation = MatrixVectorTests.FromRows(c.Input("correlation"));
            if (!c.Inputs.TryGetProperty("points", out JsonElement pointsInput))
            {
                check.Output(c, "size", () => new StochasticProcessArray(processes, correlation).Size);
                continue;
            }

            var p = new StochasticProcessArray(processes, correlation);
            var points = pointsInput.EnumerateArray().Select(q => (
                T: q[0].GetDouble(), X: new Vector(Numbers(q[1])), Dt: q[2].GetDouble(), Dw: new Vector(Numbers(q[3]))))
                .ToArray();
            check.Output(c, "size", p.Size);
            check.Output(c, "factors", p.Factors);
            check.Output(c, "initialValues", p.InitialValues().AsSpan().ToArray());
            check.Output(c, "drift", points.Select(q => (object?)p.Drift(q.T, q.X).AsSpan().ToArray()).ToArray());
            check.Output(c, "diffusion", points.Select(q => (object?)MatrixVectorTests.Rows(p.Diffusion(q.T, q.X))).ToArray());
            check.Output(
                c, "expectation", points.Select(q => (object?)p.Expectation(q.T, q.X, q.Dt).AsSpan().ToArray()).ToArray());
            check.Output(
                c, "stdDeviation", points.Select(q => (object?)MatrixVectorTests.Rows(p.StdDeviation(q.T, q.X, q.Dt))).ToArray());
            check.Output(
                c, "covariance", points.Select(q => (object?)MatrixVectorTests.Rows(p.Covariance(q.T, q.X, q.Dt))).ToArray());
            check.Output(
                c, "evolve", points.Select(q => (object?)p.Evolve(q.T, q.X, q.Dt, q.Dw).AsSpan().ToArray()).ToArray());
        }

        check.Complete();
    }

    // The processes as stochasticprocessarray.py builds them: "ou" (speed, vol, x0, level), "gbm" (s0, mue, sigma),
    // "bsm" (s0, r, q, vol) on flat curves and a constant volatility from the evaluation date, on Actual/365 Fixed.
    internal static ImmutableArray<StochasticProcess1D> Processes(VectorCase c)
    {
        Date today = c.Context().EvaluationDate;
        var dc = new Actual365Fixed();
        return [.. c.Input("processes").EnumerateArray().Select(spec =>
        {
            double[] a = Numbers(spec.GetProperty("parameters"));
            return spec.GetProperty("kind").GetString() switch
            {
                "ou" => (StochasticProcess1D)new OrnsteinUhlenbeckProcess(a[0], a[1], a[2], a[3]),
                "gbm" => new GeometricBrownianMotionProcess(a[0], a[1], a[2]),
                "bsm" => new BlackScholesMertonProcess(
                    a[0],
                    new FlatForward(today, a[2], dc),
                    new FlatForward(today, a[1], dc),
                    new BlackConstantVol(today, new NullCalendar(), a[3], dc)),
                var kind => throw new InvalidOperationException($"{c.Name}: unknown process {kind}"),
            };
        })];
    }
}
