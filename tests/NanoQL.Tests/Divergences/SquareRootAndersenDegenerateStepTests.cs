// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The square-root volatility process's step where its scheme degenerates or leaves its domain (R8). QuantLib's
/// quadratic form takes b² = 2ψ⁻¹ − 1 + √(2ψ⁻¹(2ψ⁻¹ − 1)) (<c>squarerootandersen.cpp:102-103</c>), NaN where the
/// conditional variance is 0 (no vol of variance: ψ = 0, a = 0, v = 0·∞), where the variance and its mean are both 0
/// (an absorbed 0 at θ = 0: ψ = 0/0), and where ψ is below about 1.5e-154 (the product overflows): from v₀ 0.09, θ
/// 0.04, κ 4000 on times 1, 1.0002 and 1.0006, QuantLib's states are finite at vol of variance 3e-76 and NaN at 1e-76
/// and below (the C++ harness's <c>SquareRootAndersen.paths</c>, this run's probe; at κ 1 every state is NaN at 0).
/// nanoql takes the form's limit there, the conditional mean. And QuantLib chooses its form by ψ against any cut point
/// (<c>:100</c>), where the quadratic form holds for ψ ≤ 2 only and the exponential one for ψ ≥ 1: a cut below 1 gives
/// ψ below 1 the exponential form, its p no probability, the mean 3.38% low and the variance 12.6% high at ψ 0.5 and
/// cut 0.4 (64 standard errors at 2 million draws), and a cut above 2 gives ψ above 2 the quadratic form's square root
/// of a negative, NaN. nanoql clamps the cut to [1, 2]. Checks: QuantLib's states at the NaN band's edge bit for bit;
/// the conditional mean below it and CIR's deterministic path at 0; an absorbed 0 stays 0; every cut outside [1, 2]
/// QuantLib's own value at 1 or 2 (the vectors hold cuts 1 and 2), bit for bit; the moments restored at cut 0.4; and an
/// invalid process's negative or NaN conditional variance (κθ below 0) still QuantLib's NaN.
/// </summary>
public class SquareRootAndersenDegenerateStepTests
{
    private static readonly double[] Times = [1.0, 1.0002, 1.0006];

    [Theory]
    [InlineData(1e-74, new[] { 0.04, 0.04, 0.04000000000000001 })]
    [InlineData(3e-75, new[] { 0.04, 0.04, 0.04 })]
    [InlineData(1e-75, new[] { 0.04, 0.04, 0.039999999999999994 })]
    [InlineData(3e-76, new[] { 0.04, 0.04, 0.04000000000000001 })]
    [InlineData(1e-76, new[] { 0.04, 0.04, 0.04 })]
    [InlineData(1e-77, new[] { 0.04, 0.04, 0.04 })]
    [InlineData(0.0, new[] { 0.04, 0.04, 0.04 })]
    public void AVanishingVarianceStepsToItsMean(double volVar, double[] expected)
    {
        // κΔt at the first step is 4000, so e^{−κΔt} is 0, QuantLib's own first step: from 3e-76 up QuantLib's states
        // (the harness's), below them the conditional mean, θ, where QuantLib's are NaN
        var process = new SquareRootAndersen(0.04, 4000.0, volVar, 0.09, Times, 1, 0.5, 0.5);
        process.NextPath();
        for (int s = 0; s < Times.Length; ++s)
        {
            process.Nextstep([0.3]);
            Assert.Equal(expected[s], process.StateVariables()[0]);
        }
    }

    [Fact]
    public void NoVolOfVarianceIsCirsDeterministicPath()
    {
        const double theta = 0.04, kappa = 1.0, v0 = 0.09;
        var process = new SquareRootAndersen(theta, kappa, 0.0, v0, [0.5, 1.0, 1.5], 4, 0.5, 0.5);
        process.NextPath();
        for (int s = 0; s < 3; ++s)
        {
            process.Nextstep([0.3, -0.2, 0.1, 0.5]);
            double cir = theta + ((v0 - theta) * Math.Exp(-kappa * 0.5 * (s + 1)));
            Assert.Equal(cir, process.StateVariables()[0], 1e-15 * cir);
            Assert.True(double.IsFinite(process.StepSd()));
        }
    }

    [Fact]
    public void AnAbsorbedZeroStaysZero()
    {
        var process = new SquareRootAndersen(0.0, 1.0, 0.5, 0.09, [0.5, 1.0, 1.5, 2.0], 4, 0.5, 0.5);
        var rng = new MersenneTwisterUniformRng(42);
        var normal = new InverseCumulativeNormal();
        int zeros = 0;
        for (int p = 0; p < 200; ++p)
        {
            process.NextPath();
            bool absorbed = false;
            for (int s = 0; s < 4; ++s)
            {
                process.Nextstep([normal.Evaluate(rng.Next().Value), normal.Evaluate(rng.Next().Value), normal.Evaluate(rng.Next().Value), normal.Evaluate(rng.Next().Value)]);
                double v = process.StateVariables()[0];
                Assert.True(double.IsFinite(v), $"path {p}, step {s}: {v}");
                if (absorbed)
                {
                    Assert.Equal(0.0, v);
                }

                absorbed = v == 0.0;
                zeros += absorbed ? 1 : 0;
            }
        }

        Assert.True(zeros > 0);
    }

    [Theory]
    [InlineData(-1.0, 0.04)]
    [InlineData(1.0, -0.04)]
    public void AnInvalidProcessStaysNaN(double kappa, double theta)
    {
        // κθ < 0: a negative drift at 0, its conditional variance negative or NaN somewhere, where QuantLib's step is
        // NaN, kept (its form's negative states on such a process are QuantLib's too)
        var process = new SquareRootAndersen(theta, kappa, 0.5, 0.04, [0.5, 1.0, 1.5, 2.0], 4, 0.5, 0.5);
        var rng = new MersenneTwisterUniformRng(42);
        var normal = new InverseCumulativeNormal();
        int nan = 0;
        for (int p = 0; p < 200; ++p)
        {
            process.NextPath();
            for (int s = 0; s < 4; ++s)
            {
                process.Nextstep([normal.Evaluate(rng.Next().Value), normal.Evaluate(rng.Next().Value), normal.Evaluate(rng.Next().Value), normal.Evaluate(rng.Next().Value)]);
                nan += double.IsNaN(process.StateVariables()[0]) ? 1 : 0;
            }
        }

        Assert.True(nan > 0);
    }

    [Theory]
    [InlineData(0.4, 1.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(-3.0, 1.0)]
    [InlineData(2.5, 2.0)]
    [InlineData(10.0, 2.0)]
    public void ACutOutsideTheDomainIsItsNearestEnd(double cut, double end)
    {
        // ψ from about 0.06 to 9 over the steps (vol of variance 5, 25 and 60 at κ 4000, the vectors' inputs)
        foreach (double volVar in new[] { 5.0, 25.0, 60.0 })
        {
            var outside = new SquareRootAndersen(0.04, 4000.0, volVar, 0.01, [1.0, 1.0002, 1.0006, 1.0016, 1.004], 2, 0.5, 0.5, cut);
            var atEnd = new SquareRootAndersen(0.04, 4000.0, volVar, 0.01, [1.0, 1.0002, 1.0006, 1.0016, 1.004], 2, 0.5, 0.5, end);
            var rng = new MersenneTwisterUniformRng(7);
            var normal = new InverseCumulativeNormal();
            for (int p = 0; p < 50; ++p)
            {
                outside.NextPath();
                atEnd.NextPath();
                for (int s = 0; s < 5; ++s)
                {
                    double[] z = [normal.Evaluate(rng.Next().Value), normal.Evaluate(rng.Next().Value)];
                    outside.Nextstep(z);
                    atEnd.Nextstep(z);
                    Assert.Equal(atEnd.StateVariables()[0], outside.StateVariables()[0]);
                    Assert.Equal(atEnd.StepSd(), outside.StepSd());
                }
            }
        }
    }

    [Fact]
    public void ACutBelowOneKeepsTheMoments()
    {
        // one step of one sub-step from v₀ = θ at ψ 0.5: e = e^{−κΔt}, s² = θε²(1 − e²)/(2κ), ψ = s²/θ²
        const double theta = 0.04, kappa = 1.0, dt = 0.5;
        double e = Math.Exp(-kappa * dt);
        double eps = Math.Sqrt(0.5 * 2.0 * kappa * theta / (1 - (e * e)));
        double s2 = theta * eps * eps * (1 - (e * e)) / (2 * kappa);
        var process = new SquareRootAndersen(theta, kappa, eps, theta, [dt], 1, 0.5, 0.5, 0.4);
        var rng = new MersenneTwisterUniformRng(42);
        var normal = new InverseCumulativeNormal();
        const int draws = 200000;
        double sum = 0.0, sum2 = 0.0;
        for (int i = 0; i < draws; ++i)
        {
            process.NextPath();
            process.Nextstep([normal.Evaluate(rng.Next().Value)]);
            double v = process.StateVariables()[0];
            sum += v;
            sum2 += v * v;
        }

        double mean = sum / draws, variance = (sum2 / draws) - (mean * mean);
        double error = Math.Sqrt(variance / draws);
        Assert.True(Math.Abs(mean - theta) < 4 * error, $"mean {mean} against {theta}, standard error {error}");
        Assert.Equal(s2, variance, 0.02 * s2);
    }
}
