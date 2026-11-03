// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The square-root volatility process at small reversion (fixed with an independent check). QuantLib's conditional
/// variance divides (1 − e^{−κΔt}) by κ (<c>squarerootandersen.cpp:96-97</c>), which cancels about ε/(κΔt) relative:
/// 9e-5 at κ 1e-12, the variance 0 at 1e-16, so its path is NaN at the next step, and NaN at κ 0, where the limit is v
/// ε² Δt (at κ 1e-16 and 0 from v₀ = θ 0.04, ε 0.5 on times 0.5 and 1, QuantLib's state is NaN after the second step
/// and the first). nanoql takes −expm1(−κΔt)/κ below κΔt = 1e-2, Δt at κ 0, and QuantLib's form at and above it.
/// Checks: the κ → 0 limit, CIR without reversion, at κ 0, 1e-16 and 1e-12; and the two forms' agreement across the
/// cut.
/// </summary>
public class SquareRootAndersenSmallReversionTests
{
    private const double Theta = 0.04, Eps = 0.5, Dt = 0.5, Z = 0.3;

    [Theory]
    [InlineData(0.0)]
    [InlineData(1e-16)]
    [InlineData(1e-12)]
    public void SmallReversionIsTheLimitWithoutReversion(double kappa)
    {
        var process = new SquareRootAndersen(Theta, kappa, Eps, Theta, [Dt, 2 * Dt], 1, 0.5, 0.5);
        process.NextPath();
        double v = Theta;
        for (int s = 0; s < 2; ++s)
        {
            process.Nextstep([Z]);
            v = Limit(v);
            double state = process.StateVariables()[0];
            Assert.True(double.IsFinite(state), $"step {s}: {state}");
            Assert.Equal(v, state, 1e-11 * v);
        }
    }

    [Fact]
    public void TheFormsAgreeAcrossTheCut()
    {
        double below = State(0.02 * (1 - 1e-9)), above = State(0.02 * (1 + 1e-9));
        Assert.Equal(below, above, 1e-10 * below);
    }

    // The quadratic-exponential step at κ = 0: the conditional mean v, the variance v ε² Δt.
    private static double Limit(double v)
    {
        double m = v, s2 = v * Eps * Eps * Dt;
        double psi = s2 / (m * m);
        Assert.True(psi > 1.5);
        double p = (psi - 1.0) / (psi + 1.0), beta = (1.0 - p) / m;
        double u = new CumulativeNormalDistribution().Evaluate(Z);
        return u < p ? 0.0 : Math.Log((1.0 - p) / (1.0 - u)) / beta;
    }

    private static double State(double kappa)
    {
        var process = new SquareRootAndersen(Theta, kappa, Eps, 0.09, [Dt], 1, 0.5, 0.5);
        process.NextPath();
        process.Nextstep([Z]);
        return process.StateVariables()[0];
    }
}
