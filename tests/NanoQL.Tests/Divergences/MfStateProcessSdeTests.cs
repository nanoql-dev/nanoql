// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Markov-functional state process, dx = σ(t) e^{at} dW (the S-g1d plan's G1R6 (vi)): QuantLib's diffusion omits
/// the factor e^{at} (<c>mfstateprocess.cpp:62-66</c>), and its variance without step times omits the volatility's
/// square (<c>:80-84</c>). On the harness, at a = 0.05 and σ = 0.8 without steps, its diffusion at t = 2 is 0.8 and
/// its variance over [0, 1] 1.0517091807564771, where the same process stepped once past the horizon gives
/// 0.6730938756841454. nanoql's are its equation's. QuantLib's checks: its own stepped variance, of which the diffusion's
/// square is the derivative and which the variance without steps equals with one step past the horizon. The
/// Markov-functional model's prices don't move: they read the variance's ratios, which its level leaves.
/// </summary>
public class MfStateProcessSdeTests
{
    [Fact]
    public void TheDiffusionIsTheEquations()
    {
        var process = new MfStateProcess(0.05, [1.0, 2.0, 5.0], [0.8, 1.0, 1.2, 0.9]);
        const double h = 1e-4;
        foreach (double t in new[] { 0.5, 1.5, 3.0, 7.0 })
        {
            double slope = (process.Variance(0.0, 0.0, t + h) - process.Variance(0.0, 0.0, t - h)) / (2.0 * h);
            double diffusion = process.Diffusion(t, 0.0);
            Assert.True(Math.Abs((slope / (diffusion * diffusion)) - 1.0) <= 1e-7, $"{t}: {slope} against {diffusion * diffusion}");
        }

        // QuantLib's at t = 2 without steps: 0.8.
        Assert.True(Math.Abs(new MfStateProcess(0.05, [], [0.8]).Diffusion(2.0, 0.0) - 0.8) > 0.08);
    }

    [Fact]
    public void TheVarianceWithoutStepsIsTheSteppedOne()
    {
        foreach (double a in new[] { 0.0, 0.05 })
        {
            var noSteps = new MfStateProcess(a, [], [0.8]);
            var stepPastHorizon = new MfStateProcess(a, [100.0], [0.8, 0.8]);
            foreach (double dt in new[] { 1.0, 2.0 })
            {
                double v = noSteps.Variance(0.0, 0.0, dt), stepped = stepPastHorizon.Variance(0.0, 0.0, dt);
                Assert.True(Math.Abs((v / stepped) - 1.0) <= 1e-15, $"{a} {dt}: {v} against {stepped}");
            }
        }

        // QuantLib's without steps at a = 0 over [0, 1]: 1.0, against 0.64.
        Assert.Equal(0.64, new MfStateProcess(0.0, [], [0.8]).Variance(0.0, 0.0, 1.0), 15);
    }
}
