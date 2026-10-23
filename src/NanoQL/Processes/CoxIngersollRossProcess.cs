// SPDX-FileCopyrightText: Copyright (C) 2020 Lew Wei Hao
// SPDX-FileCopyrightText: Copyright (C) 2021 Magnus Mencke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/coxingersollrossprocess.hpp ql/processes/coxingersollrossprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Processes;

/// <summary>
/// The Cox-Ingersoll-Ross process, governed by dx(t) = k(θ − x(t))dt + σ√x(t) dW(t), discretized by Leif Andersen's
/// quadratic-exponential scheme ("Efficient Simulation of the Heston Stochastic Volatility Model").
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Variance"/> is the conditional variance the scheme's own step reads, x₀σ²e^(−kΔt)(1 − e^(−kΔt))/k
/// + θσ²(1 − e^(−kΔt))²/(2k), at the state given, and <see cref="Diffusion"/> is σ√x, the header's SDE (the S-sr plan's
/// SSR7, K2): a divergence, QuantLib's variance reading the process's initial value for the state and θσ²/k for its
/// level term, and its diffusion σ (<c>coxingersollrossprocess.cpp:32-38</c>, <c>.hpp:90-92</c>;
/// <c>Divergences/CirProcessMomentsTests.cs</c>).
/// </para>
/// <para>
/// The step's (1 − e^(−kΔt))/k is written as Δt·(1 − e^(−x))/x, x = kΔt, the cancellation-free form of QuantLib's
/// master's mean-reversion kernels, exact in the limit k → 0, so the step's last bits differ from QuantLib's at every
/// speed (R8's fix of QuantLib's cancellation at small speed, the user's ruling of 2026-10-22 on the
/// Ornstein-Uhlenbeck variance's form).
/// </para>
/// </remarks>
public sealed class CoxIngersollRossProcess : StochasticProcess1D
{
    private readonly double x0;
    private readonly CumulativeNormalDistribution normal = new();

    /// <summary>Initializes a new instance of the <see cref="CoxIngersollRossProcess"/> class.</summary>
    /// <param name="speed">The speed of mean reversion, k.</param>
    /// <param name="vol">The volatility, σ, not negative.</param>
    /// <param name="x0">The initial value.</param>
    /// <param name="level">The level, θ.</param>
    /// <exception cref="ArgumentException"><paramref name="vol"/> is negative or NaN.</exception>
    public CoxIngersollRossProcess(double speed, double vol, double x0 = 0.0, double level = 0.0)
    {
        this.x0 = x0;
        Speed = speed;
        Level = level;
        Volatility = vol;
        if (!(vol >= 0.0))
        {
            throw new ArgumentException("negative volatility given", nameof(vol));
        }
    }

    /// <inheritdoc/>
    public override double X0 => x0;

    /// <summary>Gets the speed of mean reversion, k.</summary>
    public double Speed { get; }

    /// <summary>Gets the volatility, σ.</summary>
    public double Volatility { get; }

    /// <summary>Gets the level, θ.</summary>
    public double Level { get; }

    /// <inheritdoc/>
    public override double Drift(double t, double x) => Speed * (Level - x);

    /// <summary>The diffusion σ√x.</summary>
    /// <param name="t">The time, which the diffusion doesn't depend on.</param>
    /// <param name="x">The state.</param>
    /// <returns>σ√x.</returns>
    public override double Diffusion(double t, double x) => Volatility * Math.Sqrt(x);

    /// <inheritdoc/>
    public override double Expectation(double t0, double x0, double dt) => Level + ((x0 - Level) * Math.Exp(-Speed * dt));

    /// <inheritdoc/>
    public override double StdDeviation(double t0, double x0, double dt) => Math.Sqrt(Variance(t0, x0, dt));

    /// <summary>The conditional variance after Δt from the state: the quadratic-exponential step's s².</summary>
    /// <param name="t0">The start of the interval, which the variance doesn't depend on.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The variance.</returns>
    public override double Variance(double t0, double x0, double dt) => S2(x0, Math.Exp(-Speed * dt), dt);

    /// <summary>The state after Δt by the quadratic-exponential scheme, the draw dw a standard normal one.</summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <param name="dw">The draw.</param>
    /// <returns>The state.</returns>
    public override double Evolve(double t0, double x0, double dt, double dw)
    {
        double result;

        double ex = Math.Exp(-Speed * dt);

        double m = Level + ((x0 - Level) * ex);
        double s2 = S2(x0, ex, dt);
        double psi = s2 / (m * m);

        if (psi <= 1.5)
        {
            double b2 = (2 / psi) - 1 + Math.Sqrt(2 / psi * ((2 / psi) - 1));
            double b = Math.Sqrt(b2);
            double a = m / (1 + b2);

            result = a * (b + dw) * (b + dw);
        }
        else
        {
            double p = (psi - 1) / (psi + 1);
            double beta = (1 - p) / m;

            double u = normal.Evaluate(dw);

            result = u <= p ? 0.0 : Math.Log((1 - p) / (1 - u)) / beta;
        }

        return result;
    }

    // QuantLib's x0*sigma^2*ex/k*(1-ex) + theta*sigma^2/(2k)*(1-ex)^2, ex = exp(-k*dt), with (1-ex)/k as dt*f(k*dt)
    private double S2(double x, double ex, double dt)
    {
        double x1 = Speed * dt;
        double g = dt * (x1 == 0.0 ? 1.0 : -Expm1Functions.StdExpm1(-x1) / x1);
        double sigma2 = Volatility * Volatility;
        return (x * sigma2 * ex * g) + (Level * sigma2 * 0.5 * Speed * g * g);
    }
}
