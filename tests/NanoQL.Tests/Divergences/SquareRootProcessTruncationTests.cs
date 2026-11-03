// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="SquareRootProcess"/> below 0: QuantLib's diffusion is σ√x (<c>squarerootprocess.cpp:40-42</c>), and its
/// default Euler step x + a(b − x)dt + σ√x√dt·dw leaves [0, ∞), so a path that steps below 0 turns NaN for good. The
/// port's diffusion is σ√max(x, 0), the partial truncation QuantLib's <c>HestonProcess</c> takes for its variance
/// (<c>hestonprocess.cpp:350-359</c>), the drift on the state as it is. QuantLib's check: the Heston process's
/// partially truncated variance step with no correlation, on the same draws.
/// </summary>
public class SquareRootProcessTruncationTests
{
    private static readonly Date Today = new(22, Month.October, 2026);

    [Fact]
    public void AStepFromANegativeStateIsHestonsPartialTruncation()
    {
        // The variance component of Heston's step: v + κ(θ − v)dt + σ·√max(v, 0)·√dt·(ρ·dw₀ + √(1 − ρ²)·dw₁), ρ = 0.
        foreach ((double theta, double kappa, double sigma) in new[] { (0.04, 1.0, 0.5), (0.04, 2.0, 0.2), (0.1, 0.1, 0.2) })
        {
            var process = new SquareRootProcess(theta, kappa, sigma);
            var dc = new Actual365Fixed();
            foreach (double v in new[] { -0.03, -1e-6, 0.0, 0.02, 0.5 })
            {
                var heston = new HestonProcess(
                    new FlatForward(Today, 0.02, dc), new FlatForward(Today, 0.0, dc), 100.0, v, kappa, theta, sigma, 0.0,
                    HestonProcessDiscretization.PartialTruncation);
                foreach ((double dt, double dw) in new[] { (1.0 / 12.0, 0.7), (1.0 / 52.0, -1.3), (0.25, 2.1) })
                {
                    double expected = heston.Evolve(0.0, new Vector([100.0, v]), dt, new Vector([0.4, dw]))[1];
                    Assert.Equal(expected, process.Evolve(0.0, v, dt, dw));
                }
            }
        }
    }

    [Fact]
    public void AnEulerPathBelowZeroStaysFinite()
    {
        // θ 0.04, κ 1, σ 0.5 from 0.04, twelve monthly steps: with QuantLib's diffusion, 6,021 of 10,000 Mersenne Twister
        // paths (seed 42) end NaN (the port before the fix); with the truncation none does.
        var grid = new TimeGrid(1.0, 12);
        IPathGenerator<MonteCarloPath> generator = SingleVariate<PseudoRandom>.MakePathGenerator(
            new SquareRootProcess(0.04, 1.0, 0.5, 0.04), grid, PseudoRandom.MakeSequenceGenerator(12, 42), false);
        int below = 0;
        for (int i = 0; i < 2000; i++)
        {
            MonteCarloPath path = generator.Next().Value;
            bool negative = false;
            for (int j = 0; j < path.Length; j++)
            {
                Assert.False(double.IsNaN(path[j]));
                negative |= path[j] < 0.0;
            }

            below += negative ? 1 : 0;
        }

        Assert.True(below > 0);
    }
}
