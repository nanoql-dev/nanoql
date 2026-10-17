// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/stochasticprocess.hpp ql/stochasticprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: StochasticProcess1D's defaults of expectation, stdDeviation and variance, which call the discretization, wait for S-eq's EulerDiscretization

using NanoQL.Numerics;

namespace NanoQL;

/// <summary>A 1-dimensional stochastic process, governed by dx_t = μ(t, x_t) dt + σ(t, x_t) dW_t.</summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: the 1-D processes derive from it. It implements the multi-dimensional
/// interface of <see cref="StochasticProcess"/> through the 1-D members, on vectors and matrices of size 1, which
/// QuantLib keeps private and nanoql exposes as the base's overrides.
/// </para>
/// <para>
/// QuantLib's <see cref="Expectation(double, double, double)"/>, <see cref="StdDeviation(double, double, double)"/>
/// and <see cref="Variance"/> default to the process's discretization; those defaults aren't ported yet, so a derived
/// process overrides the three.
/// </para>
/// </remarks>
public abstract class StochasticProcess1D : StochasticProcess
{
    /// <summary>The discretization of the 1-D process over a time interval, or <see langword="null"/> for none.</summary>
    protected new readonly StochasticProcess1DDiscretization? discretization;

    /// <summary>Initializes a new instance of the <see cref="StochasticProcess1D"/> class, with no discretization.</summary>
    protected StochasticProcess1D()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StochasticProcess1D"/> class with a discretization.</summary>
    /// <param name="discretization">The discretization.</param>
    protected StochasticProcess1D(StochasticProcess1DDiscretization discretization)
    {
        ArgumentNullException.ThrowIfNull(discretization);
        this.discretization = discretization;
    }

    /// <summary>Gets the initial value of the state variable.</summary>
    public abstract double X0 { get; }

    /// <summary>Gets 1, the number of dimensions of a 1-D process.</summary>
    public override int Size => 1;

    /// <summary>The drift part of the equation, μ(t, x_t).</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state.</param>
    /// <returns>The drift.</returns>
    public abstract double Drift(double t, double x);

    /// <summary>The diffusion part of the equation, σ(t, x_t).</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state.</param>
    /// <returns>The diffusion.</returns>
    public abstract double Diffusion(double t, double x);

    /// <summary>
    /// The expectation E(x_{t0 + Δt} | x_{t0} = x0) of the process after a time interval Δt, by the process's
    /// discretization in QuantLib, whose default waits for its port.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The expectation.</returns>
    public abstract double Expectation(double t0, double x0, double dt);

    /// <summary>
    /// The standard deviation S(x_{t0 + Δt} | x_{t0} = x0) of the process after a time interval Δt, by the process's
    /// discretization in QuantLib, whose default waits for its port.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The standard deviation.</returns>
    public abstract double StdDeviation(double t0, double x0, double dt);

    /// <summary>
    /// The variance V(x_{t0 + Δt} | x_{t0} = x0) of the process after a time interval Δt, by the process's
    /// discretization in QuantLib, whose default waits for its port.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The variance.</returns>
    public abstract double Variance(double t0, double x0, double dt);

    /// <summary>
    /// The state after a time interval Δt; by default E(x0, t0, Δt) + S(x0, t0, Δt) · Δw, where E is the expectation
    /// and S the standard deviation, applied to the expectation by <see cref="Apply(double, double)"/>.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <param name="dw">The Brownian increment.</param>
    /// <returns>The state.</returns>
    public virtual double Evolve(double t0, double x0, double dt, double dw) =>
        Apply(Expectation(t0, x0, dt), StdDeviation(t0, x0, dt) * dw);

    /// <summary>Applies a change to the state; by default x + Δx.</summary>
    /// <param name="x0">The state.</param>
    /// <param name="dx">The change.</param>
    /// <returns>The state.</returns>
    public virtual double Apply(double x0, double dx) => x0 + dx;

    /// <summary>The initial value, as a vector of size 1.</summary>
    /// <returns>A new vector.</returns>
    public override Vector InitialValues() => new(1, X0);

    /// <summary>The drift, as a vector of size 1, at the state's first element.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state, of size 1.</param>
    /// <returns>A new vector.</returns>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        return new Vector(1, Drift(t, x[0]));
    }

    /// <summary>The diffusion, as a 1 × 1 matrix, at the state's first element.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state, of size 1.</param>
    /// <returns>A new matrix.</returns>
    public override Matrix Diffusion(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        return new Matrix(1, 1, Diffusion(t, x[0]));
    }

    /// <summary>The expectation, as a vector of size 1, from the state's first element.</summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start, of size 1.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new vector.</returns>
    public override Vector Expectation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
        return new Vector(1, Expectation(t0, x0[0], dt));
    }

    /// <summary>The standard deviation, as a 1 × 1 matrix, from the state's first element.</summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start, of size 1.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new matrix.</returns>
    public override Matrix StdDeviation(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
        return new Matrix(1, 1, StdDeviation(t0, x0[0], dt));
    }

    /// <summary>The covariance, as a 1 × 1 matrix holding the variance, from the state's first element.</summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start, of size 1.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new matrix.</returns>
    public override Matrix Covariance(double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(x0);
        return new Matrix(1, 1, Variance(t0, x0[0], dt));
    }

    /// <summary>The state after a time interval, as a vector of size 1, from the 1-D evolution.</summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start, of size 1.</param>
    /// <param name="dt">The interval's length.</param>
    /// <param name="dw">The Brownian increment, of size 1.</param>
    /// <returns>A new vector.</returns>
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        return new Vector(1, Evolve(t0, x0[0], dt, dw[0]));
    }

    /// <summary>The changed state, as a vector of size 1, from the 1-D <see cref="Apply(double, double)"/>.</summary>
    /// <param name="x0">The state, of size 1.</param>
    /// <param name="dx">The change, of size 1.</param>
    /// <returns>A new vector.</returns>
    public override Vector Apply(Vector x0, Vector dx)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dx);
        return new Vector(1, Apply(x0[0], dx[0]));
    }
}
