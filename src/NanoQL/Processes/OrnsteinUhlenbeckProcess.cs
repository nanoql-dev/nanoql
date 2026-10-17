// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/ornsteinuhlenbeckprocess.hpp ql/processes/ornsteinuhlenbeckprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Processes;

/// <summary>The Ornstein-Uhlenbeck process, governed by dx = a (r − x_t) dt + σ dW_t.</summary>
/// <remarks>Its expectation, standard deviation and variance are exact, with the algebraic limit for a small speed.</remarks>
public sealed class OrnsteinUhlenbeckProcess : StochasticProcess1D
{
    private readonly double x0;
    private readonly double speed;
    private readonly double level;
    private readonly double volatility;

    /// <summary>Initializes a new instance of the <see cref="OrnsteinUhlenbeckProcess"/> class.</summary>
    /// <param name="speed">The speed of mean reversion, a.</param>
    /// <param name="vol">The volatility, σ, not negative.</param>
    /// <param name="x0">The initial value.</param>
    /// <param name="level">The level the process reverts to, r.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="vol"/> is negative or NaN.</exception>
    public OrnsteinUhlenbeckProcess(double speed, double vol, double x0 = 0.0, double level = 0.0)
    {
        this.x0 = x0;
        this.speed = speed;
        this.level = level;
        volatility = vol;
        if (!(volatility >= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(vol), vol, "negative volatility given");
        }
    }

    /// <inheritdoc/>
    public override double X0 => x0;

    /// <summary>Gets the speed of mean reversion, a.</summary>
    public double Speed => speed;

    /// <summary>Gets the volatility, σ.</summary>
    public double Volatility => volatility;

    /// <summary>Gets the level the process reverts to, r.</summary>
    public double Level => level;

    /// <summary>The drift, a (r − x).</summary>
    /// <param name="t">The time, which the drift doesn't depend on.</param>
    /// <param name="x">The state.</param>
    /// <returns>The drift.</returns>
    public override double Drift(double t, double x) => speed * (level - x);

    /// <summary>The diffusion, σ.</summary>
    /// <param name="t">The time, which the diffusion doesn't depend on.</param>
    /// <param name="x">The state, which the diffusion doesn't depend on.</param>
    /// <returns>The volatility.</returns>
    public override double Diffusion(double t, double x) => volatility;

    /// <summary>The expectation after Δt, r + (x0 − r) e^(−a Δt).</summary>
    /// <param name="t0">The start of the interval, which the expectation doesn't depend on.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The expectation.</returns>
    public override double Expectation(double t0, double x0, double dt) => level + ((x0 - level) * Math.Exp(-speed * dt));

    /// <summary>The standard deviation after Δt, the square root of the variance.</summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The standard deviation.</returns>
    public override double StdDeviation(double t0, double x0, double dt) => Math.Sqrt(Variance(t0, x0, dt));

    /// <summary>
    /// The variance after Δt, σ² (1 − e^(−2a Δt)) / 2a; σ² Δt, its algebraic limit, when |a| is below the square root
    /// of machine epsilon.
    /// </summary>
    /// <param name="t0">The start of the interval, which the variance doesn't depend on.</param>
    /// <param name="x0">The state at its start, which the variance doesn't depend on.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The variance.</returns>
    public override double Variance(double t0, double x0, double dt)
    {
        if (Math.Abs(speed) < Math.Sqrt(NumericLimits.QL_EPSILON))
        {
            // algebraic limit for small speed
            return volatility * volatility * dt;
        }

        return 0.5 * volatility * volatility / speed * (1.0 - Math.Exp(-2.0 * speed * dt));
    }
}
