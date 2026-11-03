// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/processes/gemanroncoroniprocess.hpp ql/experimental/processes/gemanroncoroniprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Experimental;

/// <summary>
/// Geman and Roncoroni's electricity price process: a seasonal mean-reverting diffusion with upward jumps below a
/// threshold over the mean and downward jumps above it: QuantLib's <c>GemanRoncoroniProcess</c>.
/// </summary>
/// <remarks>
/// <para>
/// A working type, mutable and not thread-safe, as the generators are: the one-dimensional
/// <see cref="Evolve(double, double, double, double)"/> draws its two jump uniforms from a Mersenne Twister it builds
/// on its first call, seeded from that call's Gaussian draw as QuantLib's (<c>1234·dw + 56789</c>, as an unsigned
/// 32-bit integer), and keeps for every later call; so a path's jumps depend on how often the process was evolved
/// before, as QuantLib's. <see cref="Clone"/> returns a copy with its own, unseeded generator, for a second simulation
/// or a second thread. The five-argument <see cref="Evolve(double, double, double, double, ReadOnlySpan{double})"/>,
/// taking the jump uniforms, is stateless. A process array holding it is as thread-safe as its least safe member.
/// </para>
/// <para>
/// The standard deviation over a step, √(σ²(t)(1 − e^(−2θ₁Δt))/(2θ₁)), cancels in <c>1 − exp(−2θ₁Δt)</c> at small
/// θ₁ and is NaN at θ₁ = 0 in QuantLib's: here below |2θ₁Δt| 1e-2 it is √(σ²(t)·(−expm1(−2θ₁Δt))/(2θ₁)), and σ(t)√Δt at
/// θ₁ = 0, QuantLib's formula at and above (<c>Divergences/GemanRoncoroniDeviationTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class GemanRoncoroniProcess : StochasticProcess1D
{
    private readonly double x0;
    private readonly double alpha, beta, gamma, delta, eps, zeta, d, k, tau, sig2, a, b, theta1, theta2, theta3, psi;

    // The jump generator: built on the first one-dimensional evolve, then continued.
    private MersenneTwisterUniformRng? urng;

    /// <summary>Initializes a new instance of the <see cref="GemanRoncoroniProcess"/> class.</summary>
    /// <param name="x0">The initial value.</param>
    /// <param name="alpha">The mean's level α.</param>
    /// <param name="beta">The mean's trend β.</param>
    /// <param name="gamma">The yearly seasonality's amplitude γ.</param>
    /// <param name="delta">The half-yearly seasonality's amplitude δ.</param>
    /// <param name="eps">The yearly seasonality's phase ε.</param>
    /// <param name="zeta">The half-yearly seasonality's phase ζ.</param>
    /// <param name="d">The threshold over the mean above which the jumps are downward.</param>
    /// <param name="k">The jump intensity's period k.</param>
    /// <param name="tau">The jump intensity's phase τ.</param>
    /// <param name="sig2">The variance's level σ².</param>
    /// <param name="a">The variance's seasonal amplitude a.</param>
    /// <param name="b">The variance's seasonal phase b.</param>
    /// <param name="theta1">The mean reversion θ₁.</param>
    /// <param name="theta2">The jump intensity's level θ₂.</param>
    /// <param name="theta3">The jump size distribution's decay θ₃.</param>
    /// <param name="psi">The jump size's bound ψ.</param>
    public GemanRoncoroniProcess(
        double x0,
        double alpha,
        double beta,
        double gamma,
        double delta,
        double eps,
        double zeta,
        double d,
        double k,
        double tau,
        double sig2,
        double a,
        double b,
        double theta1,
        double theta2,
        double theta3,
        double psi)
        : base(new EulerDiscretization())
    {
        this.x0 = x0;
        this.alpha = alpha;
        this.beta = beta;
        this.gamma = gamma;
        this.delta = delta;
        this.eps = eps;
        this.zeta = zeta;
        this.d = d;
        this.k = k;
        this.tau = tau;
        this.sig2 = sig2;
        this.a = a;
        this.b = b;
        this.theta1 = theta1;
        this.theta2 = theta2;
        this.theta3 = theta3;
        this.psi = psi;
    }

    private GemanRoncoroniProcess(GemanRoncoroniProcess other)
        : this(
            other.x0, other.alpha, other.beta, other.gamma, other.delta, other.eps, other.zeta, other.d, other.k,
            other.tau, other.sig2, other.a, other.b, other.theta1, other.theta2, other.theta3, other.psi)
    {
    }

    /// <inheritdoc/>
    public override double X0 => x0;

    /// <summary>A copy of the process with its own, unseeded jump generator.</summary>
    /// <returns>The copy.</returns>
    public GemanRoncoroniProcess Clone() => new(this);

    /// <inheritdoc/>
    /// <remarks>The seasonal mean's derivative plus θ₁ times its distance from the state.</remarks>
    public override double Drift(double t, double x)
    {
        double mu = alpha + (beta * t) + (gamma * Math.Cos(eps + (2 * Math.PI * t)))
            + (delta * Math.Cos(zeta + (4 * Math.PI * t)));
        double muPrime = beta - (gamma * 2 * Math.PI * Math.Sin(eps + (2 * Math.PI * t)))
            - (delta * 4 * Math.PI * Math.Sin(zeta + (4 * Math.PI * t)));
        return muPrime + (theta1 * (mu - x));
    }

    /// <inheritdoc/>
    /// <remarks>√(σ² + a·cos²(πt + b)), whatever the state.</remarks>
    public override double Diffusion(double t, double x)
    {
        double c = Math.Cos((Math.PI * t) + b);
        return Math.Sqrt(sig2 + (a * (c * c)));
    }

    /// <inheritdoc/>
    /// <remarks>The mean-reverting step's exact deviation at the start's variance.</remarks>
    public override double StdDeviation(double t0, double x0, double dt)
    {
        double c = Math.Cos((Math.PI * t0) + b);
        double sig2t = sig2 + (a * (c * c));
        double x = 2 * theta1 * dt;
        if (x == 0.0)
        {
            return Math.Sqrt(sig2t * dt);
        }

        return Math.Abs(x) < 1e-2
            ? Math.Sqrt(sig2t * -Expm1Functions.StdExpm1(-x) / (2 * theta1))
            : Math.Sqrt(sig2t / (2 * theta1) * (1.0 - Math.Exp(-2 * theta1 * dt)));
    }

    /// <inheritdoc/>
    /// <remarks>The jump uniforms from the process's generator, seeded on the first call from its draw.</remarks>
    public override double Evolve(double t0, double x0, double dt, double dw)
    {
        // random number generator for the jump part
        urng ??= new MersenneTwisterUniformRng(unchecked((uint)(long)((1234.0 * dw) + 56789.0)));
        Span<double> du = stackalloc double[3];
        du[0] = urng.Next().Value;
        du[1] = urng.Next().Value;
        return Evolve(t0, x0, dt, dw, du);
    }

    /// <summary>The state after a step, given the Brownian increment and the jump uniforms.</summary>
    /// <param name="t0">The start of the step.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The step's length.</param>
    /// <param name="dw">The Brownian increment.</param>
    /// <param name="du">The jump uniforms: the arrival's first, the size's second.</param>
    /// <returns>The state.</returns>
    /// <exception cref="ArgumentException"><paramref name="du"/> has fewer than two uniforms.</exception>
    public double Evolve(double t0, double x0, double dt, double dw, ReadOnlySpan<double> du)
    {
        if (du.Length < 2)
        {
            throw new ArgumentException("two jump uniforms needed", nameof(du));
        }

        double retVal;
        double t = t0 + (0.5 * dt);
        double mu = alpha + (beta * t) + (gamma * Math.Cos(eps + (2 * Math.PI * t)))
            + (delta * Math.Cos(zeta + (4 * Math.PI * t)));
        double j = -1.0 / theta3 * Math.Log(1.0 + (du[1] * (Math.Exp(-theta3 * psi) - 1.0)));
        if (x0 <= mu + d)
        {
            retVal = base.Evolve(t, x0, dt, dw);
            double jumpIntensity = theta2 * ((2.0 / (1 + Math.Abs(Math.Sin(Math.PI * (t - tau) / k)))) - 1);
            double interarrival = -1.0 / jumpIntensity * Math.Log(du[0]);
            if (interarrival < dt)
            {
                retVal += j;
            }
        }
        else
        {
            retVal = x0 - j;
        }

        return retVal;
    }
}
