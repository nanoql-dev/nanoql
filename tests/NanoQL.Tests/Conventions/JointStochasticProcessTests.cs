// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="JointStochasticProcess"/> builds each step's correlation for that step (the S-hes plan's HR4 (iii)):
/// QuantLib's process caches the matrix by (t0, dt) when its derived class says the correlation doesn't depend on the
/// state, and serves it to a later step at the same (t0, dt) from another state; no class derives it, so nothing can show
/// the cache pays, and nanoql keeps none: a step is a fresh process's step bit for bit, after any other. And a derived
/// process's members: the size, factors and initial values its constituents give, each constituent's slice, the
/// correlated increments its pre-evolve hook receives, its post-evolve hook's result as the step's, and the first
/// constituent's time.
/// </summary>
public class JointStochasticProcessTests
{
    private static readonly Date Today = new(15, October, 2026);

    [Fact]
    public void TheCorrelationIsBuiltForEachStep()
    {
        // a derived process whose cross-model correlation moves with the state though it says it doesn't, where
        // QuantLib's cache would serve the first state's matrix to the second step
        var x0 = new Vector([100.0, 0.02]);
        var x1 = new Vector([140.0, 0.05]);
        var dw = new Vector([0.4, -1.1]);
        Recording process = Process(stateDependent: false);
        Vector first = process.Evolve(0.25, x0, 0.1, dw);
        Vector second = process.Evolve(0.25, x1, 0.1, dw);

        Assert.Equal(Process(stateDependent: false).Evolve(0.25, x1, 0.1, dw).ToArray(), second.ToArray());
        Assert.Equal(Process(stateDependent: true).Evolve(0.25, x1, 0.1, dw).ToArray(), second.ToArray());
        Assert.Equal(Process(stateDependent: false).Evolve(0.25, x0, 0.1, dw).ToArray(), first.ToArray());

        // the first state's matrix at the second state, QuantLib's cached step, is another step
        Vector stale = Process(stateDependent: false, frozenAt: x0).Evolve(0.25, x1, 0.1, dw);
        Assert.NotEqual(stale.ToArray(), second.ToArray());
    }

    [Fact]
    public void ADerivedProcesssMembers()
    {
        Recording process = Process(stateDependent: false);
        Assert.Equal(2, process.Size);
        Assert.Equal(2, process.Factors);
        Assert.Equal(2, process.Constituents.Length);
        Assert.Equal([100.0, 0.02], process.InitialValues().ToArray());

        var x0 = new Vector([90.0, 0.03]);
        Assert.Equal([90.0], process.SliceOf(x0, 0).ToArray());
        Assert.Equal([0.03], process.SliceOf(x0, 1).ToArray());

        var dw = new Vector([0.4, -1.1]);
        Vector y = process.Evolve(0.25, x0, 0.1, dw);
        Assert.NotNull(process.PreEvolved);
        Assert.Equal(process.PostEvolvedIncrements!.ToArray(), process.PreEvolved!.ToArray());
        Assert.Equal(process.PostEvolvedResult!.ToArray(), y.ToArray());
        // the first constituent's time: a geometric Brownian motion has no dates, and its refusal is the joint process's
        Assert.Equal(
            Assert.Throws<NanoQLException>(() => process.Constituents[0].Time(Today + 365)).Message,
            Assert.Throws<NanoQLException>(() => process.Time(Today + 365)).Message);
    }

    // a geometric Brownian motion and an Ornstein-Uhlenbeck process, their cross correlation 0.5·tanh(x/100 - 1) at the
    // first state x, or at frozenAt's for every state
    private static Recording Process(bool stateDependent, Vector? frozenAt = null) =>
        new(
            [new GeometricBrownianMotionProcess(100.0, 0.03, 0.2), new OrnsteinUhlenbeckProcess(0.5, 0.01, 0.02, 0.03)],
            stateDependent,
            frozenAt);

    private sealed class Recording(ImmutableArray<StochasticProcess> l, bool stateDependent, Vector? frozenAt)
        : JointStochasticProcess(l)
    {
        public Vector? PreEvolved { get; private set; }

        public Vector? PostEvolvedIncrements { get; private set; }

        public Vector? PostEvolvedResult { get; private set; }

        public Vector SliceOf(Vector x, int i) => Slice(x, i);

        public override void PreEvolve(double t0, Vector x0, double dt, Vector dw) => PreEvolved = dw.Clone();

        public override Vector PostEvolve(double t0, Vector x0, double dt, Vector dw, Vector y0)
        {
            PostEvolvedIncrements = dw.Clone();
            Vector y = y0.Clone();
            y[1] += 1e-3;
            PostEvolvedResult = y.Clone();
            return y;
        }

        public override double Numeraire(double t, Vector x) => Math.Exp(0.03 * t);

        public override bool CorrelationIsStateDependent() => stateDependent;

        public override Matrix CrossModelCorrelation(double t0, Vector x0)
        {
            double rho = 0.5 * Math.Tanh(((frozenAt ?? x0)[0] / 100.0) - 1.0);
            var c = new Matrix(2, 2, 0.0);
            c[0, 1] = rho;
            c[1, 0] = rho;
            return c;
        }
    }
}
