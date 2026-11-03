// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Processes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Cox-Ingersoll-Ross process's moments: QuantLib's <c>variance</c> (<c>coxingersollrossprocess.cpp:32-38</c>)
/// reads the constructor's x0 where the step's state is given, and its level term, θσ²(1 − e^(−kΔt))²/k, is twice that
/// of its own <c>evolve</c>'s s2 (<c>coxingersollrossprocess.hpp:117-118</c>); its <c>diffusion</c> is the constant σ
/// (<c>:94-96</c>), where the header's SDE is dr = k(θ − r)dt + σ√r dW. On the C++ harness, with k = 0.3, σ = 0.1, x0 =
/// 0.04, θ = 0.05 and Δt = 0.25, the variance is 9.808235281791348e-05 at the states 0.01, 0.04 and 0.1 alike, and the
/// diffusion 0.1. nanoql's variance is evolve's s2 at the state, written with (1 − e^(−kΔt))/k as Δt(1 − e^(−x))/x, x =
/// kΔt, the Ornstein-Uhlenbeck process's cancellation-free form, and its diffusion σ√x. QuantLib's check: its own
/// evolve formula, and the header's SDE.
/// </summary>
public class CirProcessMomentsTests
{
    [Fact]
    public void TheVarianceIsEvolvesS2AtTheState()
    {
        const double k = 0.3, sigma = 0.1, theta = 0.05, dt = 0.25;
        var p = new CoxIngersollRossProcess(k, sigma, 0.04, theta);
        foreach (double x in new[] { 0.01, 0.04, 0.1 })
        {
            // coxingersollrossprocess.hpp:116-118
            double ex = Math.Exp(-k * dt);
            double s2 = (x * sigma * sigma * ex / k * (1 - ex)) + (theta * sigma * sigma / (2 * k) * (1 - ex) * (1 - ex));
            double variance = p.Variance(0.0, x, dt);
            Assert.True(Math.Abs((variance / s2) - 1.0) <= 1e-14, $"x {x}: {variance} against {s2}");
        }
    }

    [Fact]
    public void TheDiffusionIsTheSdes()
    {
        var p = new CoxIngersollRossProcess(0.3, 0.1, 0.04, 0.05);
        foreach (double x in new[] { 0.0, 0.01, 0.04, 0.1 })
        {
            Assert.Equal(0.1 * Math.Sqrt(x), p.Diffusion(0.0, x));
        }
    }
}
