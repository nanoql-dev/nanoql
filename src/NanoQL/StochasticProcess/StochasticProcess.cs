// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/stochasticprocess.hpp ql/stochasticprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL;

/// <summary>
/// A multi-dimensional stochastic process, governed by dx_t = μ(t, x_t) dt + σ(t, x_t) · dW_t.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: the processes derive from it. A process is a value: its members take vectors
/// and return new vectors and matrices, and never keep one they are given. QuantLib's observer interface is removed.
/// </para>
/// <para>
/// <see cref="Expectation"/>, <see cref="StdDeviation"/> and <see cref="Covariance"/> default to the process's
/// discretization, as QuantLib's do; a process built without one overrides the three, or they refuse.
/// </para>
/// <para>
/// <see cref="InitialValues(Span{double})"/> and
/// <see cref="Evolve(double, ReadOnlySpan{double}, double, ReadOnlySpan{double}, Span{double})"/> are the in-place
/// forms the multi-dimensional Monte Carlo loop steps through: each defaults to its allocating form and a copy, so a
/// process that doesn't override them keeps its bits; a process overrides them to step without allocating, by
/// QuantLib's arithmetic.
/// </para>
/// </remarks>
public abstract class StochasticProcess
{
    /// <summary>The discretization of the process over a time interval, or <see langword="null"/> for none.</summary>
    protected readonly StochasticProcessDiscretization? discretization;

    /// <summary>Initializes a new instance of the <see cref="StochasticProcess"/> class, with no discretization.</summary>
    protected StochasticProcess()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StochasticProcess"/> class with a discretization.</summary>
    /// <param name="discretization">The discretization.</param>
    protected StochasticProcess(StochasticProcessDiscretization discretization)
    {
        ArgumentNullException.ThrowIfNull(discretization);
        this.discretization = discretization;
    }

    /// <summary>Gets the number of dimensions of the process.</summary>
    public abstract int Size { get; }

    /// <summary>Gets the number of independent factors of the process; by default, its <see cref="Size"/>.</summary>
    public virtual int Factors => Size;

    /// <summary>The initial values of the state variables.</summary>
    /// <returns>A new vector.</returns>
    public abstract Vector InitialValues();

    /// <summary>
    /// The initial values of the state variables, written into the caller's span; by default
    /// <see cref="InitialValues()"/>'s values, copied.
    /// </summary>
    /// <param name="result">The values, of the process's <see cref="Size"/> at least.</param>
    /// <exception cref="ArgumentException"><paramref name="result"/> is shorter than the process's size.</exception>
    public virtual void InitialValues(Span<double> result) => CopyInto(InitialValues(), result);

    /// <summary>The drift part of the equation, μ(t, x_t).</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state.</param>
    /// <returns>A new vector.</returns>
    public abstract Vector Drift(double t, Vector x);

    /// <summary>The diffusion part of the equation, σ(t, x_t).</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state.</param>
    /// <returns>A new matrix.</returns>
    public abstract Matrix Diffusion(double t, Vector x);

    /// <summary>
    /// The expectation E(x_{t0 + Δt} | x_{t0} = x0) of the process after a time interval Δt; by default, the
    /// discretization's drift applied to x0 by <see cref="Apply"/>.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="NanoQLException">The process has no discretization.</exception>
    public virtual Vector Expectation(double t0, Vector x0, double dt) =>
        Apply(x0, Discretization().Drift(this, t0, x0, dt));

    /// <summary>
    /// The standard deviation S(x_{t0 + Δt} | x_{t0} = x0) of the process after a time interval Δt; by default, the
    /// discretization's diffusion.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="NanoQLException">The process has no discretization.</exception>
    public virtual Matrix StdDeviation(double t0, Vector x0, double dt) =>
        Discretization().Diffusion(this, t0, x0, dt);

    /// <summary>
    /// The covariance V(x_{t0 + Δt} | x_{t0} = x0) of the process after a time interval Δt; by default, the
    /// discretization's covariance.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new matrix.</returns>
    /// <exception cref="NanoQLException">The process has no discretization.</exception>
    public virtual Matrix Covariance(double t0, Vector x0, double dt) =>
        Discretization().Covariance(this, t0, x0, dt);

    /// <summary>
    /// The state after a time interval Δt; by default E(x0, t0, Δt) + S(x0, t0, Δt) · Δw, where E is the expectation
    /// and S the standard deviation, applied to the expectation by <see cref="Apply"/>.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <param name="dw">The Brownian increments, one per factor.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentException">The sizes don't match.</exception>
    public virtual Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        return Apply(Expectation(t0, x0, dt), StdDeviation(t0, x0, dt) * dw);
    }

    /// <summary>
    /// The state after a time interval Δt, written into the caller's span: the in-place form of
    /// <see cref="Evolve(double, Vector, double, Vector)"/>, by default its values, copied.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start, of the process's <see cref="Size"/>.</param>
    /// <param name="dt">The interval's length.</param>
    /// <param name="dw">The Brownian increments, one per factor.</param>
    /// <param name="result">The state at the interval's end, of the process's size at least; it mustn't overlap
    /// <paramref name="x0"/> or <paramref name="dw"/>.</param>
    /// <exception cref="ArgumentException">The sizes don't match.</exception>
    public virtual void Evolve(double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> result) =>
        CopyInto(Evolve(t0, new Vector(x0), dt, new Vector(dw)), result);

    /// <summary>Applies a change to the state; by default x + Δx.</summary>
    /// <param name="x0">The state.</param>
    /// <param name="dx">The change.</param>
    /// <returns>A new vector.</returns>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public virtual Vector Apply(Vector x0, Vector dx)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dx);
        return x0 + dx;
    }

    /// <summary>
    /// The time corresponding to a date in the process's reference system. A process that needs no such conversion
    /// keeps this default, which refuses.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The time.</returns>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">The process doesn't convert dates to times.</exception>
    public virtual double Time(Date date)
    {
        Date.ThrowIfDefault(date, nameof(date));
        throw new NanoQLException("date/time conversion not supported");
    }

    // QuantLib dereferences its discretization pointer, which is undefined when the process was built without one.
    private StochasticProcessDiscretization Discretization() =>
        discretization ?? throw new NanoQLException("no discretization given");

    // A state the process reads shorter than its size: QuantLib reads the state's variables past its end.
    private protected void CheckState(int length, string paramName)
    {
        if (!(length >= Size))
        {
            throw new ArgumentException($"{length} values given for a state of {Size}", paramName);
        }
    }

    // Increments fewer than the process's factors: QuantLib reads them past their end.
    private protected void CheckIncrements(int length, string paramName)
    {
        if (!(length >= Factors))
        {
            throw new ArgumentException($"{length} increments given for {Factors} factors", paramName);
        }
    }

    // A span to write a state into, shorter than the process's: the in-place forms' refusal, as CopyInto's.
    private protected void CheckResult(int length)
    {
        if (length < Size)
        {
            throw new ArgumentException("the result is shorter than the process's state", "result");
        }
    }

    // The in-place forms' default: an allocating form's vector, copied into the caller's span.
    private static void CopyInto(Vector values, Span<double> result)
    {
        if (result.Length < values.Size)
        {
            throw new ArgumentException("the result is shorter than the process's state", nameof(result));
        }

        values.AsSpan().CopyTo(result);
    }
}
