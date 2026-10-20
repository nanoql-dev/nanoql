// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/eulerdiscretization.hpp ql/processes/eulerdiscretization.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Processes;

/// <summary>
/// Euler discretization for stochastic processes: over a time interval Δt, the drift μ(t0, x0) Δt, the diffusion
/// σ(t0, x0) √Δt and the covariance σ(t0, x0) σ(t0, x0)ᵀ Δt.
/// </summary>
/// <remarks>
/// QuantLib's class implements both of its process families' discretization interfaces. A C# class has one base, so
/// this one is a <see cref="StochasticProcess1DDiscretization"/>, declares the multi-dimensional members too, and
/// converts implicitly to a <see cref="StochasticProcessDiscretization"/> that forwards to them: a multi-dimensional
/// process is given <c>new EulerDiscretization()</c> as QuantLib's is.
/// </remarks>
public sealed class EulerDiscretization : StochasticProcess1DDiscretization
{
    /// <summary>The multi-dimensional discretization over this one's members.</summary>
    /// <param name="d">The Euler discretization.</param>
    public static implicit operator StochasticProcessDiscretization(EulerDiscretization d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return new MultiDimensional(d);
    }

    /// <summary>The drift over the interval, μ(t0, x0) Δt.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new vector.</returns>
    public Vector Drift(StochasticProcess process, double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(process);
        return process.Drift(t0, x0) * dt;
    }

    /// <summary>The drift over the interval, μ(t0, x0) Δt.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The drift.</returns>
    public override double Drift(StochasticProcess1D process, double t0, double x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(process);
        return process.Drift(t0, x0) * dt;
    }

    /// <summary>The diffusion over the interval, σ(t0, x0) √Δt.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new matrix.</returns>
    public Matrix Diffusion(StochasticProcess process, double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(process);
        return process.Diffusion(t0, x0) * Math.Sqrt(dt);
    }

    /// <summary>The diffusion over the interval, σ(t0, x0) √Δt.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The diffusion.</returns>
    public override double Diffusion(StochasticProcess1D process, double t0, double x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(process);
        return process.Diffusion(t0, x0) * Math.Sqrt(dt);
    }

    /// <summary>The covariance over the interval, σ(t0, x0) σ(t0, x0)ᵀ Δt.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>A new matrix.</returns>
    public Matrix Covariance(StochasticProcess process, double t0, Vector x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(process);
        Matrix sigma = process.Diffusion(t0, x0);
        Matrix result = sigma * MatrixFunctions.Transpose(sigma) * dt;
        return result;
    }

    /// <summary>The variance over the interval, σ(t0, x0)² Δt.</summary>
    /// <param name="process">The process.</param>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The variance.</returns>
    public override double Variance(StochasticProcess1D process, double t0, double x0, double dt)
    {
        ArgumentNullException.ThrowIfNull(process);
        double sigma = process.Diffusion(t0, x0);
        return sigma * sigma * dt;
    }

    // The multi-dimensional interface over the Euler discretization's members.
    private sealed class MultiDimensional(EulerDiscretization euler) : StochasticProcessDiscretization
    {
        public override Vector Drift(StochasticProcess process, double t0, Vector x0, double dt) =>
            euler.Drift(process, t0, x0, dt);

        public override Matrix Diffusion(StochasticProcess process, double t0, Vector x0, double dt) =>
            euler.Diffusion(process, t0, x0, dt);

        public override Matrix Covariance(StochasticProcess process, double t0, Vector x0, double dt) =>
            euler.Covariance(process, t0, x0, dt);
    }
}
