// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/processes/extouwithjumpsprocess.hpp ql/experimental/processes/extouwithjumpsprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// Kluge's spot model, an extended Ornstein-Uhlenbeck process plus exponential jumps (an extension of Lucia and
/// Schwartz's): S = e^(X + Y), dX = α(μ(t) − X)dt + σdW, dY = −βY dt + J dN, the jumps exponential with rate η.
/// </summary>
/// <remarks>
/// <para>
/// Immutable; two state variables, X and Y, and three factors: X's Brownian draw, the draw that times a jump and the
/// draw that sizes it, each an inverse normal mapped to a uniform within [ε, 1 − ε]. The in-place
/// <see cref="Evolve(double, ReadOnlySpan{double}, double, ReadOnlySpan{double}, Span{double})"/> allocates nothing,
/// for QuantLib's swing cases' 200,000 paths.
/// </para>
/// <para>
/// A step draws at most one jump, QuantLib's first-order scheme: the probability of a jump in a step of length dt is 1
/// − e^(−λdt), and two or more are taken as one, a bias of order (λdt)² that shrinks with the step.
/// </para>
/// <para>
/// References: T. Kluge, Pricing Swing Options and other Electricity Derivatives, 2008; B. Hambly, S. Howison and T.
/// Kluge, Modelling spikes and pricing swing options in electricity markets.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class ExtOUWithJumpsProcess : StochasticProcess
{
    private static readonly CumulativeNormalDistribution CumNormalDist = new();

    private readonly double y0;

    /// <summary>Initializes a new instance of the <see cref="ExtOUWithJumpsProcess"/> class.</summary>
    /// <param name="process">The extended Ornstein-Uhlenbeck process X.</param>
    /// <param name="y0">The jump component Y's value today.</param>
    /// <param name="beta">The jump component's mean-reversion speed β.</param>
    /// <param name="jumpIntensity">The jumps' intensity λ.</param>
    /// <param name="eta">The jump sizes' exponential rate η.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    public ExtOUWithJumpsProcess(ExtendedOrnsteinUhlenbeckProcess process, double y0, double beta, double jumpIntensity, double eta)
    {
        ArgumentNullException.ThrowIfNull(process);
        ExtendedOrnsteinUhlenbeckProcess = process;
        this.y0 = y0;
        Beta = beta;
        JumpIntensity = jumpIntensity;
        Eta = eta;
    }

    /// <summary>Gets the extended Ornstein-Uhlenbeck process X.</summary>
    public ExtendedOrnsteinUhlenbeckProcess ExtendedOrnsteinUhlenbeckProcess { get; }

    /// <summary>Gets the jump component's mean-reversion speed β.</summary>
    public double Beta { get; }

    /// <summary>Gets the jump sizes' exponential rate η.</summary>
    public double Eta { get; }

    /// <summary>Gets the jumps' intensity λ.</summary>
    public double JumpIntensity { get; }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <inheritdoc/>
    public override int Factors => 3;

    /// <inheritdoc/>
    public override Vector InitialValues() => new([ExtendedOrnsteinUhlenbeckProcess.X0, y0]);

    /// <inheritdoc/>
    public override void InitialValues(Span<double> result)
    {
        result[0] = ExtendedOrnsteinUhlenbeckProcess.X0;
        result[1] = y0;
    }

    /// <inheritdoc/>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        return new([ExtendedOrnsteinUhlenbeckProcess.Drift(t, x[0]), -Beta * x[1]]);
    }

    /// <inheritdoc/>
    /// <remarks>QuantLib's 2 × 2 matrix, X's volatility its one non-zero entry; the jump component has no diffusion.</remarks>
    public override Matrix Diffusion(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        var retVal = new Matrix(2, 2, 0.0);
        retVal[0, 0] = ExtendedOrnsteinUhlenbeckProcess.Diffusion(t, x[0]);
        return retVal;
    }

    /// <inheritdoc/>
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        var retVal = new Vector(2);
        Evolve(t0, x0.AsSpan(), dt, dw.AsSpan(), retVal.AsSpan());
        return retVal;
    }

    /// <inheritdoc/>
    public override void Evolve(double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> result)
    {
        double x = ExtendedOrnsteinUhlenbeckProcess.Evolve(t0, x0[0], dt, dw[0]);
        double y = x0[1] * Math.Exp(-Beta * dt);
        double u1 = Math.Max(NumericLimits.QL_EPSILON, Math.Min(CumNormalDist.Evaluate(dw[1]), 1.0 - NumericLimits.QL_EPSILON));
        double interarrival = -1.0 / JumpIntensity * Math.Log(u1);
        if (interarrival < dt)
        {
            double u2 = Math.Max(NumericLimits.QL_EPSILON, Math.Min(CumNormalDist.Evaluate(dw[2]), 1.0 - NumericLimits.QL_EPSILON));
            double jumpSize = -1.0 / Eta * Math.Log(u2);
            y += jumpSize;
        }

        result[0] = x;
        result[1] = y;
    }
}
