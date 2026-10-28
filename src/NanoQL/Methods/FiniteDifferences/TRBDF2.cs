// SPDX-FileCopyrightText: Copyright (C) 2011 Fabien Le Floc'h
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/trbdf2.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The TR-BDF2 scheme of the old finite-difference framework, over a <see cref="TridiagonalOperator"/>: a trapezoidal
/// stage to t − αΔt, then a BDF2 stage to t − Δt, α = 2 − √2 (Bank et al.; <see href="http://ssrn.com/abstract=1648878"/>).
/// The operator must be linear.
/// </summary>
/// <remarks>
/// <para>
/// A working type, as the old framework's operators: it holds a copy of the operator, whose time it sets, and the
/// boundary conditions it is given, and <see cref="Step"/> writes into the caller's span. Its step's parts are built
/// by <see cref="SetStep"/>; a step before it refuses, as QuantLib's operators of size 0 do.
/// </para>
/// <para>
/// For a time-dependent operator QuantLib builds the trapezoidal stage's implicit part at t − Δt, where the stage ends
/// at t − αΔt, so it steps at first order (on u_t = (1 + t)u_xx, errors 0.21, 0.040, 0.0025, 0.0030 at 25 to 200
/// steps where TR-BDF2's are 0.25, 0.066, 0.017, 0.0041). Here the trapezoidal implicit part is built at t − αΔt and
/// the BDF2 stage's at t − Δt (the S-core plan's CR7 (xvi), a documented divergence); for an operator constant in time,
/// QuantLib's scheme, bit for bit.
/// </para>
/// </remarks>
public sealed class TRBDF2 : IFdmScheme
{
    private readonly double alpha;
    private readonly TridiagonalOperator l;
    private readonly TridiagonalOperator i;
    private readonly ImmutableArray<BoundaryCondition<TridiagonalOperator>> bcs;
    private TridiagonalOperator explicitTrapezoidalPart = new(0);
    private TridiagonalOperator explicitBDF2PartFull = new(0);
    private TridiagonalOperator explicitBDF2PartMid = new(0);
    private TridiagonalOperator implicitPart = new(0);
    private double dt;

    /// <summary>Initializes a new instance of the <see cref="TRBDF2"/> class.</summary>
    /// <param name="l">The operator, which the scheme copies, its time setter shared.</param>
    /// <param name="bcs">The boundary conditions, applied in their order.</param>
    /// <exception cref="ArgumentNullException">An argument or a condition is null.</exception>
    public TRBDF2(TridiagonalOperator l, IEnumerable<BoundaryCondition<TridiagonalOperator>> bcs)
    {
        ArgumentNullException.ThrowIfNull(l);
        ArgumentNullException.ThrowIfNull(bcs);
        this.l = l.Clone();
        i = TridiagonalOperator.Identity(l.Size);
        dt = 0.0;
        this.bcs = [.. bcs];
        foreach (BoundaryCondition<TridiagonalOperator> bc in this.bcs)
        {
            ArgumentNullException.ThrowIfNull(bc, nameof(bcs));
        }

        alpha = 2.0 - Math.Sqrt(2.0);
    }

    /// <inheritdoc/>
    public void SetStep(double dt)
    {
        this.dt = dt;

        implicitPart = i + (0.5 * alpha * dt * l);
        explicitTrapezoidalPart = i - (0.5 * alpha * dt * l);
        explicitBDF2PartFull = -(1.0 - alpha) * (1.0 - alpha) / (alpha * (2.0 - alpha)) * i;
        explicitBDF2PartMid = 1.0 / (alpha * (2.0 - alpha)) * i;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The values aren't of the operator's size.</exception>
    /// <exception cref="NanoQLException">No step was set: QuantLib's "uninitialized TridiagonalOperator".</exception>
    public void Step(Span<double> a, double t)
    {
        var aInit = new Vector(a);
        var v = new Vector(a);
        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.SetTime(t);
        }

        // trapezoidal explicit part
        if (l.IsTimeDependent)
        {
            l.SetTime(t);
            explicitTrapezoidalPart = i - (0.5 * alpha * dt * l);
        }

        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.ApplyBeforeApplying(explicitTrapezoidalPart);
        }

        v = explicitTrapezoidalPart.ApplyTo(v);
        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.ApplyAfterApplying(v.AsSpan());
        }

        // trapezoidal implicit part, at the stage's end t - alpha dt (CR7 (xvi)); for a constant operator, the step's
        TridiagonalOperator trapezoidalImplicitPart = implicitPart;
        if (l.IsTimeDependent)
        {
            l.SetTime(t - (alpha * dt));
            trapezoidalImplicitPart = i + (0.5 * alpha * dt * l);
        }

        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.ApplyBeforeSolving(trapezoidalImplicitPart, v.AsSpan());
        }

        v = trapezoidalImplicitPart.SolveFor(v);
        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.ApplyAfterSolving(v.AsSpan());
        }

        // BDF2 explicit part
        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.ApplyBeforeApplying(explicitBDF2PartFull);
        }

        Vector b0 = explicitBDF2PartFull.ApplyTo(aInit);
        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.ApplyAfterApplying(b0.AsSpan());
        }

        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.ApplyBeforeApplying(explicitBDF2PartMid);
        }

        Vector b1 = explicitBDF2PartMid.ApplyTo(v);
        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.ApplyAfterApplying(b1.AsSpan());
        }

        v = b0 + b1;

        // the BDF2 implicit part at t - dt; reuse the step's for a constant operator - works only for alpha=2-sqrt(2)
        if (l.IsTimeDependent)
        {
            l.SetTime(t - dt);
            implicitPart = i + (0.5 * alpha * dt * l);
        }

        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.ApplyBeforeSolving(implicitPart, v.AsSpan());
        }

        v = implicitPart.SolveFor(v);
        foreach (BoundaryCondition<TridiagonalOperator> bc in bcs)
        {
            bc.ApplyAfterSolving(v.AsSpan());
        }

        v.AsSpan().CopyTo(a);
    }
}
