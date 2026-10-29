// SPDX-FileCopyrightText: Copyright (C) 2010 SunTrust Bank
// SPDX-FileCopyrightText: Copyright (C) 2010 Cavit Hafizoglu
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/shortrate/generalizedornsteinuhlenbeckprocess.hpp ql/experimental/shortrate/generalizedornsteinuhlenbeckprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The Ornstein-Uhlenbeck process dx = a(t)(level − x)dt + σ(t)dW with time-dependent speed and volatility: QuantLib's
/// <c>GeneralizedOrnsteinUhlenbeckProcess</c>.
/// </summary>
/// <remarks>
/// The expectation and the variance over a step take the speed and the volatility at the step's start. The variance,
/// σ²(1 − e^(−2aΔt))/(2a), cancels at small speed: QuantLib cuts to σ²Δt below a √ε, which jumps there and cancels
/// just above, and takes that cut for a negative speed too (the variance at −0.05 4.9% low). Here, below |a| 1e-2, it is
/// σ²·(−expm1(−2aΔt))/(2a), σ²Δt at a = 0, and QuantLib's formula at and above, a negative speed past −1e-2 on it (the
/// S-xrt plan's XRR5 (xix); <c>Divergences/GeneralizedHullWhiteSmallReversionTests.cs</c>).
/// </remarks>
[Experimental("NQL0001")]
public sealed class GeneralizedOrnsteinUhlenbeckProcess : StochasticProcess1D
{
    private readonly double x0;
    private readonly double level;
    private readonly Func<double, double> speed;
    private readonly Func<double, double> volatility;

    /// <summary>Initializes a new instance of the <see cref="GeneralizedOrnsteinUhlenbeckProcess"/> class.</summary>
    /// <param name="speed">The speed a(t).</param>
    /// <param name="vol">The volatility σ(t).</param>
    /// <param name="x0">The initial value.</param>
    /// <param name="level">The level the process reverts to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="speed"/> or <paramref name="vol"/> is null.</exception>
    /// <exception cref="NanoQLException">The initial value or the level is negative, with QuantLib's texts.</exception>
    public GeneralizedOrnsteinUhlenbeckProcess(
        Func<double, double> speed, Func<double, double> vol, double x0 = 0.0, double level = 0.0)
    {
        this.speed = speed ?? throw new ArgumentNullException(nameof(speed));
        volatility = vol ?? throw new ArgumentNullException(nameof(vol));
        if (!(x0 >= 0.0))
        {
            throw new NanoQLException("negative initial data given");
        }

        if (!(level >= 0.0))
        {
            throw new NanoQLException("negative level given");
        }

        this.x0 = x0;
        this.level = level;
    }

    /// <inheritdoc/>
    public override double X0 => x0;

    /// <summary>Gets the level the process reverts to.</summary>
    public double Level => level;

    /// <inheritdoc/>
    public override double Drift(double t, double x) => speed(t) * (level - x);

    /// <inheritdoc/>
    public override double Diffusion(double t, double x) => volatility(t);

    /// <inheritdoc/>
    public override double Expectation(double t0, double x0, double dt) =>
        level + ((x0 - level) * Math.Exp(-speed(t0) * dt));

    /// <inheritdoc/>
    public override double StdDeviation(double t0, double x0, double dt) => Math.Sqrt(Variance(t0, x0, dt));

    /// <inheritdoc/>
    /// <remarks>Through <c>expm1</c> below |a| 1e-2 (XRR5 (xix)).</remarks>
    public override double Variance(double t0, double x0, double dt)
    {
        double a = speed(t0);
        double vol = volatility(t0);
        if (a == 0.0)
        {
            return vol * vol * dt;
        }

        return Math.Abs(a) < 1e-2
            ? vol * vol * -Expm1Functions.StdExpm1(-2.0 * a * dt) / (2.0 * a)
            : 0.5 * vol * vol / a * (1.0 - Math.Exp(-2.0 * a * dt));
    }

    /// <summary>The speed at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The speed.</returns>
    public double Speed(double t) => speed(t);

    /// <summary>The volatility at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The volatility.</returns>
    public double Volatility(double t) => volatility(t);
}
