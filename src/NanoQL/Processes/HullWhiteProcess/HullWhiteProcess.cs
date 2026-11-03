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
/// The Hull-White stochastic process: the short rate r = x + α(t), x an Ornstein-Uhlenbeck process of speed a and
/// volatility σ from the curve's instantaneous forward at 0, and α(t) = f(t) + ½(σ(1 − e^(−at))/a)², f the curve's
/// instantaneous forward.
/// </summary>
/// <remarks>
/// The drift's σ²(1 − e^(−2at))/(2a) and α's σ(1 − e^(−at))/a are written as σ²t·(1 − e^(−2at))/(2at) and σt·(1 −
/// e^(−at))/(at), the cancellation-free forms of QuantLib's master's Vasicek: a divergence, QuantLib's drift NaN at a =
/// 0, which the constructor accepts, and cancelling below, its α cut at QL_EPSILON
/// (<c>Divergences/HullWhiteProcessSmallReversionTests.cs</c>). The drift's derivative of the forward is QuantLib's
/// forward difference over 0.0001.
/// </remarks>
public sealed class HullWhiteProcess : StochasticProcess1D
{
    private readonly OrnsteinUhlenbeckProcess process;
    private readonly YieldTermStructure h;

    /// <summary>Initializes a new instance of the <see cref="HullWhiteProcess"/> class.</summary>
    /// <param name="h">The curve.</param>
    /// <param name="a">The mean-reversion speed, not negative.</param>
    /// <param name="sigma">The volatility, not negative.</param>
    /// <exception cref="ArgumentNullException"><paramref name="h"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sigma"/> is negative or NaN (the Ornstein-Uhlenbeck process's refusal).</exception>
    /// <exception cref="ArgumentException"><paramref name="a"/> or <paramref name="sigma"/> is negative.</exception>
    public HullWhiteProcess(YieldTermStructure h, double a, double sigma)
    {
        ArgumentNullException.ThrowIfNull(h);
        process = new OrnsteinUhlenbeckProcess(a, sigma, h.ForwardRate(0.0, 0.0, Compounding.Continuous, Frequency.NoFrequency).Rate);
        this.h = h;
        A = a;
        Sigma = sigma;
        if (!(a >= 0.0))
        {
            throw new ArgumentException("negative a given", nameof(a));
        }

        if (!(sigma >= 0.0))
        {
            throw new ArgumentException("negative sigma given", nameof(sigma));
        }
    }

    /// <summary>Gets the mean-reversion speed a.</summary>
    public double A { get; }

    /// <summary>Gets the volatility σ.</summary>
    public double Sigma { get; }

    /// <inheritdoc/>
    public override double X0 => process.X0;

    /// <inheritdoc/>
    public override double Drift(double t, double x)
    {
        // sigma^2/(2a)*(1 - exp(-2at)), without its cancellation as a -> 0
        double alphaDrift = Sigma * Sigma * t * OneMinusExpOverX(2.0 * A * t);
        double shift = 0.0001;
        double f = h.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency).Rate;
        double fup = h.ForwardRate(t + shift, t + shift, Compounding.Continuous, Frequency.NoFrequency).Rate;
        double fPrime = (fup - f) / shift;
        alphaDrift += (A * f) + fPrime;
        return process.Drift(t, x) + alphaDrift;
    }

    /// <inheritdoc/>
    public override double Diffusion(double t, double x) => process.Diffusion(t, x);

    /// <inheritdoc/>
    public override double Expectation(double t0, double x0, double dt) =>
        process.Expectation(t0, x0, dt) + Alpha(t0 + dt) - (Alpha(t0) * Math.Exp(-A * dt));

    /// <inheritdoc/>
    public override double StdDeviation(double t0, double x0, double dt) => process.StdDeviation(t0, x0, dt);

    /// <inheritdoc/>
    public override double Variance(double t0, double x0, double dt) => process.Variance(t0, x0, dt);

    /// <summary>α(t) = f(t) + ½(σ(1 − e^(−at))/a)², f the curve's instantaneous forward.</summary>
    /// <param name="t">The time.</param>
    /// <returns>α(t).</returns>
    public double Alpha(double t)
    {
        double alfa = Sigma * t * OneMinusExpOverX(A * t);
        alfa *= 0.5 * alfa;
        alfa += h.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency).Rate;
        return alfa;
    }
}
