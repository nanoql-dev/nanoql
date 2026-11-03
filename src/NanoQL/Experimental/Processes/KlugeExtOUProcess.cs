// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/processes/klugeextouprocess.hpp ql/experimental/processes/klugeextouprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// Kluge's power model beside a correlated extended Ornstein-Uhlenbeck gas model: the power state (X, Y) of an
/// <see cref="ExtOUWithJumpsProcess"/>, and the gas log-price U, dU = κ(μ(t) − U)dt + σdW_U, its Brownian motion
/// correlated with X's by ρ.
/// </summary>
/// <remarks>
/// <para>
/// Immutable; three state variables (X, Y, U) and four factors (the power process's three, then U's own draw, mixed
/// with X's by ρ). The in-place
/// <see cref="Evolve(double, ReadOnlySpan{double}, double, ReadOnlySpan{double}, Span{double})"/> allocates nothing.
/// </para>
/// <para>
/// Documented divergence (<c>Divergences/KlugeExtOUDiffusionTests.cs</c>): QuantLib's diffusion writes the gas row at
/// row <c>size()</c> and column <c>factors()</c>, one past each end of its <c>size() × factors()</c> matrix, so its gas
/// row is 0 and the writes corrupt the heap; here the gas row is the last, ρσ in the first column and √(1 − ρ²)σ in
/// the last, so the diffusion's square is the covariance.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class KlugeExtOUProcess : StochasticProcess
{
    private readonly double sqrtMRho;

    /// <summary>Initializes a new instance of the <see cref="KlugeExtOUProcess"/> class.</summary>
    /// <param name="rho">The correlation ρ of the power and gas Brownian motions.</param>
    /// <param name="klugeProcess">The power process.</param>
    /// <param name="ouProcess">The gas process.</param>
    /// <exception cref="ArgumentNullException">A process is <see langword="null"/>.</exception>
    public KlugeExtOUProcess(double rho, ExtOUWithJumpsProcess klugeProcess, ExtendedOrnsteinUhlenbeckProcess ouProcess)
    {
        ArgumentNullException.ThrowIfNull(klugeProcess);
        ArgumentNullException.ThrowIfNull(ouProcess);
        Rho = rho;
        sqrtMRho = Math.Sqrt(1 - (rho * rho));
        KlugeProcess = klugeProcess;
        ExtOUProcess = ouProcess;
    }

    /// <summary>Gets the power process.</summary>
    public ExtOUWithJumpsProcess KlugeProcess { get; }

    /// <summary>Gets the gas process.</summary>
    public ExtendedOrnsteinUhlenbeckProcess ExtOUProcess { get; }

    /// <summary>Gets the correlation ρ of the power and gas Brownian motions.</summary>
    public double Rho { get; }

    /// <inheritdoc/>
    public override int Size => KlugeProcess.Size + 1;

    /// <inheritdoc/>
    public override int Factors => KlugeProcess.Factors + 1;

    /// <inheritdoc/>
    public override Vector InitialValues()
    {
        var retVal = new Vector(Size);
        Vector x0 = KlugeProcess.InitialValues();
        x0.AsSpan().CopyTo(retVal.AsSpan());
        retVal[Size - 1] = ExtOUProcess.X0;
        return retVal;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="result"/> is shorter than the process's size.</exception>
    public override void InitialValues(Span<double> result)
    {
        CheckResult(result.Length);
        KlugeProcess.InitialValues(result[..KlugeProcess.Size]);
        result[Size - 1] = ExtOUProcess.X0;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> holds fewer values than the jump process's state, where QuantLib reads past its end.
    /// </exception>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        if (!(x.Size >= KlugeProcess.Size))
        {
            throw new ArgumentException(
                $"{x.Size} values given for the jump process's state of {KlugeProcess.Size}", nameof(x));
        }

        var retVal = new Vector(Size);
        Vector mu = KlugeProcess.Drift(t, x);
        mu.AsSpan().CopyTo(retVal.AsSpan());
        retVal[Size - 1] = ExtOUProcess.Drift(t, x[x.Size - 1]);
        return retVal;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="x"/> holds fewer values than the state.</exception>
    public override Matrix Diffusion(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        CheckState(x.Size, nameof(x));
        var retVal = new Matrix(Size, Factors, 0.0);
        double vol = ExtOUProcess.Diffusion(t, x[x.Size - 1]);
        retVal[0, 0] = KlugeProcess.Diffusion(t, x)[0, 0];

        // the gas row is the last, its columns the first and the last: QuantLib writes one past each end
        retVal[Size - 1, 0] = Rho * vol;
        retVal[Size - 1, Factors - 1] = sqrtMRho * vol;
        return retVal;
    }

    /// <inheritdoc/>
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        var retVal = new Vector(Size);
        Evolve(t0, x0.AsSpan(), dt, dw.AsSpan(), retVal.AsSpan());
        return retVal;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> holds fewer values than the jump process's state, or <paramref name="dw"/> fewer
    /// increments than its factors, where QuantLib reads past their end; or <paramref name="result"/> is shorter than
    /// the state.
    /// </exception>
    public override void Evolve(double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> result)
    {
        if (!(x0.Length >= KlugeProcess.Size))
        {
            throw new ArgumentException(
                $"{x0.Length} values given for the jump process's state of {KlugeProcess.Size}", nameof(x0));
        }

        if (!(dw.Length >= KlugeProcess.Factors))
        {
            throw new ArgumentException(
                $"{dw.Length} increments given for the jump process's {KlugeProcess.Factors} factors", nameof(dw));
        }

        CheckResult(result.Length);
        int n = KlugeProcess.Size;
        KlugeProcess.Evolve(t0, x0[..n], dt, dw[..KlugeProcess.Factors], result[..n]);
        double dz = (dw[^1] * sqrtMRho) + (dw[0] * Rho);
        result[n] = ExtOUProcess.Evolve(t0, x0[^1], dt, dz);
    }
}
