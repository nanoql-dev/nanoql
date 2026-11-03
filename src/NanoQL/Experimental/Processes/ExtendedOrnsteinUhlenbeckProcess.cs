// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/processes/extendedornsteinuhlenbeckprocess.hpp ql/experimental/processes/extendedornsteinuhlenbeckprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Experimental;

/// <summary>
/// The extended Ornstein-Uhlenbeck process dx = a(b(t) − x)dt + σdW, its level b a function of time.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. The expectation over a step integrates the level by the discretization chosen; the standard deviation
/// and variance are the Ornstein-Uhlenbeck process's at level 0.
/// </para>
/// <para>
/// Two of QuantLib's defects are fixed (<c>Divergences/ExtendedOrnsteinUhlenbeckExpectationTests.cs</c>): the
/// trapezoidal rule's (1 − e^(−a·dt))/(a·dt), 0/0 at a speed of 0 and cancelling below, is a series below a·dt of 1e-2
/// and QuantLib's quotient from it; and the Gauss-Lobatto rule, whose integrand b(x)e^(ax) QuantLib integrates to an
/// absolute accuracy e^(a(t₀+dt)) puts out of reach (it throws from a(t₀+dt) of 35), integrates the bounded
/// b(x)e^(−a(t₀+dt−x)) where a(t₀+dt) passes 30, with a relative accuracy of 1e-10 beside the absolute one, and
/// QuantLib's integrand at and below 30.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class ExtendedOrnsteinUhlenbeckProcess : StochasticProcess1D
{
    // a·dt below which the trapezoidal rule's quotient is its series
    private const double SeriesBelow = 1e-2;

    // a(t₀ + dt) past which the Gauss-Lobatto rule integrates the bounded integrand, and its relative accuracy
    private const double BoundedAbove = 30.0;
    private const double BoundedRelativeAccuracy = 1e-10;
    private const int MaxIterations = 100000;

    private readonly Func<double, double> b;
    private readonly double intEps;
    private readonly OrnsteinUhlenbeckProcess ouProcess;
    private readonly ExtendedOrnsteinUhlenbeckProcessDiscretization discretizationScheme;

    /// <summary>Initializes a new instance of the <see cref="ExtendedOrnsteinUhlenbeckProcess"/> class.</summary>
    /// <param name="speed">The mean-reversion speed a, non-negative.</param>
    /// <param name="sigma">The volatility σ, non-negative.</param>
    /// <param name="x0">The process's value today.</param>
    /// <param name="b">The level b as a function of time.</param>
    /// <param name="discretization">How a step's expectation integrates the level.</param>
    /// <param name="intEps">The Gauss-Lobatto rule's absolute accuracy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="b"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The volatility is negative ("negative volatility given"), the
    /// speed is ("negative a given"), or the discretization isn't a member of its type.</exception>
    public ExtendedOrnsteinUhlenbeckProcess(
        double speed,
        double sigma,
        double x0,
        Func<double, double> b,
        ExtendedOrnsteinUhlenbeckProcessDiscretization discretization = ExtendedOrnsteinUhlenbeckProcessDiscretization.MidPoint,
        double intEps = 1e-4)
    {
        ArgumentNullException.ThrowIfNull(b);
        EnumArguments.Check(discretization, nameof(discretization));
        this.b = b;
        this.intEps = intEps;
        ouProcess = new OrnsteinUhlenbeckProcess(speed, sigma, x0);
        discretizationScheme = discretization;
        if (!(speed >= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "negative a given");
        }

        Speed = speed;
        Volatility = sigma;
    }

    /// <inheritdoc/>
    public override double X0 => ouProcess.X0;

    /// <summary>Gets the mean-reversion speed a.</summary>
    public double Speed { get; }

    /// <summary>Gets the volatility σ.</summary>
    public double Volatility { get; }

    /// <inheritdoc/>
    public override double Drift(double t, double x) => ouProcess.Drift(t, x) + (Speed * b(t));

    /// <inheritdoc/>
    public override double Diffusion(double t, double x) => ouProcess.Diffusion(t, x);

    /// <inheritdoc/>
    public override double StdDeviation(double t0, double x0, double dt) => ouProcess.StdDeviation(t0, x0, dt);

    /// <inheritdoc/>
    public override double Variance(double t0, double x0, double dt) => ouProcess.Variance(t0, x0, dt);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The Gauss-Lobatto rule doesn't converge.</exception>
    public override double Expectation(double t0, double x0, double dt)
    {
        switch (discretizationScheme)
        {
            case ExtendedOrnsteinUhlenbeckProcessDiscretization.MidPoint:
                return ouProcess.Expectation(t0, x0, dt) + (b(t0 + (0.5 * dt)) * (1.0 - Math.Exp(-Speed * dt)));
            case ExtendedOrnsteinUhlenbeckProcessDiscretization.Trapezodial:
            {
                double t = t0 + dt;
                double u = t0;
                double bt = b(t);
                double bu = b(u);
                double ex = Math.Exp(-Speed * dt);
                double x = Speed * dt;

                // (1 − e^(−x))/x: QuantLib's quotient, 0/0 at a speed of 0, from x of 1e-2, its series below
                double ratio = x < SeriesBelow
                    ? 1.0 - (x * ((1.0 / 2.0) - (x * ((1.0 / 6.0) - (x * ((1.0 / 24.0) - (x * ((1.0 / 120.0) - (x * ((1.0 / 720.0) - (x / 5040.0)))))))))))
                    : (1 - ex) / (Speed * dt);
                return ouProcess.Expectation(t0, x0, dt) + bt - (ex * bu) - ((bt - bu) * ratio);
            }

            default:
                return GaussLobattoExpectation(t0, x0, dt);
        }
    }

    // apart, so its integrands' closure is made only on its own route
    private double GaussLobattoExpectation(double t0, double x0, double dt)
    {
        double end = t0 + dt;
        double a = Speed;
        if (a * end <= BoundedAbove)
        {
            return ouProcess.Expectation(t0, x0, dt)
                + (a * Math.Exp(-a * end) * new GaussLobattoIntegral(MaxIterations, intEps).Integrate(x => b(x) * Math.Exp(a * x), t0, end));
        }

        // the bounded integrand b(x)e^(−a(t₀+dt−x)), where e^(a(t₀+dt)) puts QuantLib's accuracy out of reach
        return ouProcess.Expectation(t0, x0, dt)
            + (a * new GaussLobattoIntegral(MaxIterations, intEps, BoundedRelativeAccuracy).Integrate(x => b(x) * Math.Exp(-a * (end - x)), t0, end));
    }
}
