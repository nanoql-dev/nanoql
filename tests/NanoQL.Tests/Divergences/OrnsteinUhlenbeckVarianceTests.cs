// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Ornstein-Uhlenbeck variance at small speed (the S-sr plan's SSR3 class, by the user's ruling of 2026-10-22):
/// QuantLib's <c>variance</c> (<c>ornsteinuhlenbeckprocess.cpp:34-42</c>) is σ²Δt below |a| = √ε, dropping the terms of
/// order aΔt, and above it σ²(1 − e^(−2aΔt))/(2a), whose 1 − e^(−2aΔt) cancels: 8.3e-8 relative at a = 1.5e-8 and
/// Δt = 0.01, 1.4e-8 below the cut at a = 1.4e-8 and Δt = 1. nanoql's is σ²Δt·(1 − e^(−x))/x, x = 2aΔt, the
/// cancellation-free form of QuantLib's master's kernels. QuantLib's check: its own closed form at ordinary speed and
/// its own limit at a = 0; at small speed, the series σ²Δt(1 − aΔt + ⅔(aΔt)²), exact there to well below the tolerance.
/// </summary>
public class OrnsteinUhlenbeckVarianceTests
{
    [Fact]
    public void TheVarianceAtSmallSpeedIsItsSeries()
    {
        // σ = 0.01; QuantLib on the wheel: 1.0000000827403712e-06, 1.000000082740371e-06, 1.00000002722922e-06,
        // 9.999999883714141e-07 and 0.0001.
        foreach ((double a, double dt) in new[] { (1.5e-8, 0.01), (2e-8, 0.01), (1e-7, 0.01), (1e-6, 0.01), (1.4e-8, 1.0) })
        {
            double x = a * dt;
            double series = 0.01 * 0.01 * dt * (1.0 - x + (2.0 / 3.0 * x * x));
            double variance = new OrnsteinUhlenbeckProcess(a, 0.01).Variance(0.0, 0.0, dt);
            Assert.True(Math.Abs((variance / series) - 1.0) <= 1e-15, $"a = {a}, dt = {dt}: {variance} against {series}");
        }
    }

    [Fact]
    public void TheVarianceAtOrdinarySpeedAndAtZeroIsQuantLibs()
    {
        // QuantLib on the wheel, σ = 0.01
        foreach ((double a, double dt, double quantLib) in new[]
                 {
                     (0.1, 0.01, 9.990006663334605e-07), (0.1, 0.25, 2.4385287749642992e-05),
                     (0.1, 1.0, 9.06346234610091e-05), (0.5, 2.0, 8.646647167633873e-05),
                 })
        {
            Assert.True(Tolerance.Within(quantLib, new OrnsteinUhlenbeckProcess(a, 0.01).Variance(0.0, 0.0, dt)));
        }

        Assert.Equal(5e-05, new OrnsteinUhlenbeckProcess(0.0, 0.01).Variance(0.0, 0.0, 0.5));
    }
}
