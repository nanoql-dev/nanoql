// SPDX-FileCopyrightText: Copyright (C) 2006 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/hullwhiteprocess.hpp ql/processes/hullwhiteprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Processes.HullWhiteProcessFunctions;

namespace NanoQL.Processes;

/// <summary>
/// The forward Hull-White stochastic process: <see cref="HullWhiteProcess"/>'s dynamics in the forward measure of
/// a time T, the drift less B(t, T)σ² and the expectation less M_T(t₀, t₀ + Δt, T).
/// </summary>
/// <remarks>
/// <para>
/// The drift's and α's terms are written as <see cref="HullWhiteProcess"/>'s; B(t, T) = (1 − e^(−a(T − t)))/a as
/// (T − t)·(1 − e^(−a(T − t)))/(a(T − t)); and M_T(s, t, T), QuantLib's σ²/a²[(1 − e^(−u)) − ½(e^(−v) − e^(−v−2u))],
/// u = a(t − s), v = a(T − t), as σ²(t − s)f(u)[(T − t)f(v) + ½e^(−v)(t − s)f(u)], f(x) = (1 − e^(−x))/x, every term
/// positive (the S-sr plan's SSR3 and SSR6 (ii)): a divergence, QuantLib's cutting at QL_EPSILON and cancelling to
/// O(σ²) from terms of order σ²(t − s)/a above it, its half-year expectation −5.55e9 at a = 1e-15
/// (<c>Divergences/HullWhiteForwardProcessSmallReversionTests.cs</c>).
/// </para>
/// <para>
/// QuantLib's constructor leaves the forward measure time unset; <see cref="WithForwardMeasureTime"/> gives a copy at
/// one, and the drift and the expectation refuse to read an unset one.
/// </para>
/// </remarks>
public sealed class HullWhiteForwardProcess : ForwardMeasureProcess1D
{
    private readonly OrnsteinUhlenbeckProcess process;
    private readonly YieldTermStructure h;

    /// <summary>Initializes a new instance of the <see cref="HullWhiteForwardProcess"/> class, with no forward measure time.</summary>
    /// <param name="h">The curve.</param>
    /// <param name="a">The mean-reversion speed.</param>
    /// <param name="sigma">The volatility, not negative.</param>
    /// <exception cref="ArgumentNullException"><paramref name="h"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sigma"/> is negative or NaN (the Ornstein-Uhlenbeck process's refusal).</exception>
    public HullWhiteForwardProcess(YieldTermStructure h, double a, double sigma)
    {
        ArgumentNullException.ThrowIfNull(h);
        process = new OrnsteinUhlenbeckProcess(a, sigma, h.ForwardRate(0.0, 0.0, Compounding.Continuous, Frequency.NoFrequency).Rate);
        this.h = h;
        A = a;
        Sigma = sigma;
    }

    /// <summary>Gets the mean-reversion speed a.</summary>
    public double A { get; }

    /// <summary>Gets the volatility σ.</summary>
    public double Sigma { get; }

    /// <inheritdoc/>
    public override double X0 => process.X0;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The process has no forward measure time.</exception>
    public override double Drift(double t, double x)
    {
        // sigma^2/(2a)*(1 - exp(-2at)), without its cancellation as a -> 0
        double alphaDrift = Sigma * Sigma * t * OneMinusExpOverX(2.0 * A * t);
        double shift = 0.0001;
        double f = h.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency).Rate;
        double fup = h.ForwardRate(t + shift, t + shift, Compounding.Continuous, Frequency.NoFrequency).Rate;
        double fPrime = (fup - f) / shift;
        alphaDrift += (A * f) + fPrime;
        return process.Drift(t, x) + alphaDrift - (B(t, ForwardMeasureTime) * Sigma * Sigma);
    }

    /// <inheritdoc/>
    public override double Diffusion(double t, double x) => process.Diffusion(t, x);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The process has no forward measure time.</exception>
    public override double Expectation(double t0, double x0, double dt) =>
        process.Expectation(t0, x0, dt) + Alpha(t0 + dt) - (Alpha(t0) * Math.Exp(-A * dt)) - M_T(t0, t0 + dt, ForwardMeasureTime);

    /// <inheritdoc/>
    public override double StdDeviation(double t0, double x0, double dt) => process.StdDeviation(t0, x0, dt);

    /// <inheritdoc/>
    public override double Variance(double t0, double x0, double dt) => process.Variance(t0, x0, dt);

    /// <summary>A copy of the process at another forward measure time.</summary>
    /// <param name="forwardMeasureTime">The forward measure time T.</param>
    /// <returns>The copy; the receiver doesn't change.</returns>
    public override HullWhiteForwardProcess WithForwardMeasureTime(double forwardMeasureTime) =>
        (HullWhiteForwardProcess)base.WithForwardMeasureTime(forwardMeasureTime);

    /// <summary>α(t) = f(t) + ½(σ(1 − e^(−at))/a)², f the curve's instantaneous forward.</summary>
    /// <param name="t">The time.</param>
    /// <returns>α(t).</returns>
    public double Alpha(double t)
    {
        double alfa = Sigma * t * OneMinusExpOverX(A * t);
        alfa *= 0.5 * alfa;

        // ForwardRate(t, t, Continuous)'s rate without its InterestRate, so a Monte Carlo step allocates nothing
        alfa += h.ContinuousForwardRate(t, t);
        return alfa;
    }

    /// <summary>The forward measure's correction to the expectation from s to t: M_T(s, t, T).</summary>
    /// <param name="s">The start.</param>
    /// <param name="t">The end.</param>
    /// <param name="T">The forward measure time.</param>
    /// <returns>M_T(s, t, T).</returns>
    public double M_T(double s, double t, double T)
    {
        double fu = OneMinusExpOverX(A * (t - s));
        double v = A * (T - t);
        return Sigma * Sigma * (t - s) * fu * (((T - t) * OneMinusExpOverX(v)) + (0.5 * Math.Exp(-v) * (t - s) * fu));
    }

    /// <summary>B(t, T) = (1 − e^(−a(T − t)))/a.</summary>
    /// <param name="t">The time.</param>
    /// <param name="T">The maturity.</param>
    /// <returns>B(t, T).</returns>
    public double B(double t, double T) => (T - t) * OneMinusExpOverX(A * (T - t));
}
