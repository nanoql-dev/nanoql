// SPDX-FileCopyrightText: Copyright (C) 2026 Junjie Guo
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/g2process.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testG2ProcessObservesTermStructure (Conventions/ModelRebuildTests.cs)

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using RsgType = NanoQL.Numerics.InverseCumulativeRsg<
    NanoQL.Numerics.RandomSequenceGenerator<NanoQL.Numerics.MersenneTwisterUniformRng>,
    NanoQL.Numerics.InverseCumulativeNormal>;

namespace NanoQL.Tests.TestSuite;

// Adapted, for every case: the flat curve's zero settlement days (line 57) count from the test runner's date, the
// context's, as FlatForward.Create does.
[Trait("Oracle", "QuantLib")]
public class G2ProcessTests
{
    [Fact]
    public void TestG2ProcessDriftIncludesTermStructure()
    {
        // Testing that G2Process drift includes the term-structure shift...

        const Real a = 0.1, sigma = 0.01, b = 0.2, eta = 0.013, rho = -0.5;

        var paramOnly = new G2Process(a, sigma, b, eta, rho);
        var withCurve = new G2Process(a, sigma, b, eta, rho, MakeFlatCurve(0.04));

        const double t = 1.5;
        var z = new Vector(2);
        z[0] = 0.002;
        z[1] = -0.003;

        Vector d1 = paramOnly.Drift(t, z);
        Vector d2 = withCurve.Drift(t, z);

        // The y-component must not depend on the term structure.
        const Real tol = 1e-12;
        Assert.False(Math.Abs(d1[1] - d2[1]) > tol, $"y-drift should not depend on the term structure: {d1[1]} vs {d2[1]}");

        // The x-component must differ by exactly a*phi(t) + phi'(t)
        // (computed with the same numerical derivative as the implementation).
        const Real h = 1.0e-4;
        Real phi_t = withCurve.Phi(t);
        Real phi_th = withCurve.Phi(t + h);
        Real expectedDelta = (a * phi_t) + ((phi_th - phi_t) / h);
        Real actualDelta = d2[0] - d1[0];
        Assert.False(
            Math.Abs(actualDelta - expectedDelta) > 1e-10,
            $"x-drift shift mismatch: expected {expectedDelta}, got {actualDelta}");

        // Diffusion is purely deterministic and curve-independent.
        Matrix diff1 = paramOnly.Diffusion(t, z);
        Matrix diff2 = withCurve.Diffusion(t, z);
        for (int i = 0; i < 2; ++i)
        {
            for (int j = 0; j < 2; ++j)
            {
                Assert.False(
                    Math.Abs(diff1[i, j] - diff2[i, j]) > 1e-14,
                    $"G2Process diffusion changed when term structure was provided at ({i},{j}): {diff1[i, j]} vs {diff2[i, j]}");
            }
        }
    }

    [Fact]
    public void TestG2ProcessPhiAndShortRate()
    {
        // Testing G2Process phi(t), shortRate(t, z1, z2), and initialValues...

        const Real a = 0.1, sigma = 0.01, b = 0.2, eta = 0.013, rho = -0.5;
        const Rate flatRate = 0.03;
        YieldTermStructure curve = MakeFlatCurve(flatRate);

        var process = new G2Process(a, sigma, b, eta, rho, curve);

        double[] times = [0.25, 1.0, 5.0, 10.0];

        const Real tol = 1e-12;
        foreach (double t in times)
        {
            Real expectedPhi = ReferencePhi(curve, t, a, sigma, b, eta, rho);
            Real actualPhi = process.Phi(t);
            Assert.False(
                Math.Abs(actualPhi - expectedPhi) > tol,
                $"G2Process::phi mismatch at t={t}: expected {expectedPhi}, got {actualPhi}");
        }

        // shortRate is just the sum of the simulated components: the term-structure
        // offset phi(t) is already baked into the first component.
        Real[] zs1 = [-0.01, 0.0, 0.005];
        Real[] zs2 = [-0.002, 0.0, 0.004];
        foreach (Real z1 in zs1)
        {
            foreach (Real z2 in zs2)
            {
                Rate expected = z1 + z2;
                Rate actual = process.ShortRate(1.0, z1, z2);
                Assert.False(
                    Math.Abs(actual - expected) > tol,
                    $"G2Process::shortRate(t, z1, z2) should equal z1+z2: got {actual}, expected {expected}");
            }
        }

        // Initial state must satisfy z1(0) + z2(0) = phi(0) = f(0,0).
        Vector iv = process.InitialValues();
        Real expectedR0 = ReferencePhi(curve, 0.0, a, sigma, b, eta, rho);
        Assert.False(
            Math.Abs((iv[0] + iv[1]) - expectedR0) > 1e-12,
            $"initialValues do not sum to phi(0): {iv[0] + iv[1]} vs {expectedR0}");
        Assert.False(
            Math.Abs(process.X0 - expectedR0) > 1e-12,
            $"x0() should equal phi(0) when a curve is set: {process.X0} vs {expectedR0}");
        Assert.False(Math.Abs(process.Y0) > 1e-14, $"y0() should be zero, got {process.Y0}");
    }

    [Fact]
    public void TestG2ProcessPhiMatchesG2Model()
    {
        // Testing that G2Process::phi matches the G2 model fitting parameter...

        const Real a = 0.12, sigma = 0.011, b = 0.17, eta = 0.009, rho = -0.3;
        YieldTermStructure curve = MakeFlatCurve(0.025);

        var process = new G2Process(a, sigma, b, eta, rho, curve);
        var model = new G2(curve, a, sigma, b, eta, rho);
        TwoFactorModelShortRateDynamics dyn = model.Dynamics();
        Assert.NotNull(dyn);

        const Real tol = 1e-12;
        foreach (double t in (ReadOnlySpan<double>)[0.1, 0.5, 2.0, 7.5, 20.0])
        {
            // dyn->shortRate(t, 0, 0) collapses to fitting_(t) = phi(t).
            Rate fromModel = dyn.ShortRate(t, 0.0, 0.0);
            Rate fromProcess = process.Phi(t);
            Assert.False(
                Math.Abs(fromProcess - fromModel) > tol,
                $"G2Process::phi disagrees with G2 model at t={t}: process={fromProcess}, model={fromModel}");
        }
    }

    [Fact]
    public void TestG2ProcessExpectationConsistentWithCurve()
    {
        // Testing that G2Process expectation reproduces phi(t)...

        const Real a = 0.1, sigma = 0.01, b = 0.2, eta = 0.013, rho = -0.4;
        YieldTermStructure curve = MakeFlatCurve(0.035);

        var process = new G2Process(a, sigma, b, eta, rho, curve);
        Vector iv = process.InitialValues();

        const Real tol = 1e-12;
        foreach (double t in (ReadOnlySpan<double>)[0.1, 0.5, 2.0, 5.0, 10.0])
        {
            // Expectation starting from t=0 in the initial state must satisfy
            // E[z1(t) + z2(t)] = phi(t), since z2(0) = 0 and y is zero-mean OU.
            Vector exp_t = process.Expectation(0.0, iv, t);
            Real expectedR = process.Phi(t);
            Real actualR = exp_t[0] + exp_t[1];
            Assert.False(
                Math.Abs(actualR - expectedR) > tol,
                $"E[r(t)] mismatch at t={t}: expected {expectedR}, got {actualR}");
        }
    }

    [Fact]
    public void TestG2ProcessPhiRequiresTermStructure()
    {
        // Testing that G2Process::phi throws without a term structure...

        var process = new G2Process(0.1, 0.01, 0.2, 0.013, -0.5);
        Assert.Null(process.TermStructure);
        Assert.Throws<NanoQLException>(() => process.Phi(1.0));

        // shortRate no longer touches the curve: it just sums the two components.
        process.ShortRate(1.0, 0.01, 0.01);
        Assert.False(
            Math.Abs(process.ShortRate(1.0, 0.01, 0.02) - 0.03) > 1e-14,
            "shortRate(t, z1, z2) should equal z1+z2 with no curve");

        // Without a curve, the process degenerates to two zero-mean OU factors.
        Vector iv = process.InitialValues();
        Assert.False(
            Math.Abs(iv[0]) > 1e-14 || Math.Abs(iv[1]) > 1e-14,
            $"initialValues should be (0,0) without a curve, got ({iv[0]}, {iv[1]})");
    }

    [Fact]
    public void TestG2ProcessPathGeneratorMatchesCurve()
    {
        // Testing that MultiPathGenerator on G2Process reproduces the curve-implied short-rate expectation...

        const Real a = 0.1, sigma = 0.01, b = 0.2, eta = 0.013, rho = -0.3;
        YieldTermStructure curve = MakeFlatCurve(0.03);

        var process = new G2Process(a, sigma, b, eta, rho, curve);

        const double horizon = 5.0;
        const Size steps = 50;
        var grid = new TimeGrid(horizon, steps);

        RsgType rsg = PseudoRandom.MakeSequenceGenerator(process.Factors * steps, 42);
        var generator = new MultiPathGenerator<RsgType>(process, grid, rsg, false);

        const Size nPaths = 20000;
        var sumR = new Real[steps + 1];
        for (Size n = 0; n < nPaths; ++n)
        {
            MultiPath mp = generator.Next().Value;
            for (Size i = 0; i <= steps; ++i)
            {
                sumR[i] += mp[0][i] + mp[1][i];
            }
        }

        // Empirical mean of r(t_i) should converge to phi(t_i). With 20k
        // antithetic-free paths the MC standard error is comfortably below 5e-4
        // for the parameters above.
        for (Size i = 0; i <= steps; ++i)
        {
            Real meanR = sumR[i] / nPaths;
            Real expected = process.Phi(grid[i]);
            Assert.False(
                Math.Abs(meanR - expected) > 5e-4,
                $"MC mean of r at t={grid[i]} is {meanR}, expected {expected} (diff {meanR - expected})");
        }
    }

    [Fact]
    public void TestG2ForwardProcessPhiAndShortRate()
    {
        // Testing G2ForwardProcess phi(t) and shortRate(t, z1, z2)...

        const Real a = 0.1, sigma = 0.01, b = 0.2, eta = 0.013, rho = -0.5;
        YieldTermStructure curve = MakeFlatCurve(0.035);

        var fwd = new G2ForwardProcess(a, sigma, b, eta, rho, curve);

        const Real tol = 1e-12;
        foreach (double t in (ReadOnlySpan<double>)[0.25, 1.0, 5.0])
        {
            Real expected = ReferencePhi(curve, t, a, sigma, b, eta, rho);
            Real actual = fwd.Phi(t);
            Assert.False(
                Math.Abs(actual - expected) > tol,
                $"G2ForwardProcess::phi mismatch at t={t}: expected {expected}, got {actual}");

            // shortRate sums the simulated components.
            Rate sr = fwd.ShortRate(t, 0.002, -0.001);
            Assert.False(
                Math.Abs(sr - 0.001) > tol,
                $"G2ForwardProcess::shortRate mismatch at t={t}: expected 0.001, got {sr}");
        }

        // Without a curve, phi() throws but shortRate() still works.
        var paramOnly = new G2ForwardProcess(a, sigma, b, eta, rho);
        Assert.Null(paramOnly.TermStructure);
        Assert.Throws<NanoQLException>(() => paramOnly.Phi(1.0));
        paramOnly.ShortRate(1.0, 0.01, 0.01);
    }

    // Reference implementation of the G2++ deterministic offset phi(t),
    // copied verbatim from G2::FittingParameter::Impl::value in
    // ql/models/shortrate/twofactormodels/g2.hpp. The tests check that
    // G2Process / G2ForwardProcess match this formula.
    private static Real ReferencePhi(YieldTermStructure h, double t, Real a, Real sigma, Real b, Real eta, Real rho)
    {
        Rate forward = h.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency).Rate;
        Real temp1 = sigma * (1.0 - Math.Exp(-a * t)) / a;
        Real temp2 = eta * (1.0 - Math.Exp(-b * t)) / b;
        return (0.5 * temp1 * temp1) + (0.5 * temp2 * temp2) + (rho * temp1 * temp2) + forward;
    }

    private static YieldTermStructure MakeFlatCurve(Rate rate) =>
        FlatForward.Create(0, new TARGET(), rate, new Actual365Fixed(), new EvaluationContext { EvaluationDate = Date.TodaysDate() });
}
