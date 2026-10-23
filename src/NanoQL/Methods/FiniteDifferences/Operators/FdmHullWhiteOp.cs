// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmhullwhiteop.hpp ql/methods/finitedifferences/operators/fdmhullwhiteop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The Hull-White model's backward operator in one direction of a mesher: the factor's drift −ax and diffusion ½σ²,
/// discounting at the short rate x + φ.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-eq plan's SEQR1): mutable, owned by one computation and not thread-safe. The direction's
/// operator is built at construction; <see cref="SetTime"/> rewrites the step's operator in place with φ at the step's
/// two ends' mean, and allocates nothing (the S-sr plan's SSD10), the fitting reading the curve's forward through
/// <see cref="TermStructures.YieldTermStructure"/>'s internal <c>ContinuousForwardRate</c>, its bits
/// <c>ForwardRate</c>'s.
/// </para>
/// <para>
/// The model's dynamics are built once, at construction, where QuantLib's <c>setTime</c> calls
/// <c>model->dynamics()</c> each step: the model is immutable, so they are the same each time.
/// </para>
/// </remarks>
public sealed class FdmHullWhiteOp : FdmLinearOpComposite
{
    private readonly int direction;
    private readonly double[] x;
    private readonly TripleBandLinearOp dzMap;
    private readonly TripleBandLinearOp mapT;
    private readonly OneFactorModelShortRateDynamics dynamics;

    // the step's diagonal, held
    private readonly double[] diagonal;

    /// <summary>Initializes a new instance of the <see cref="FdmHullWhiteOp"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="model">The Hull-White model.</param>
    /// <param name="direction">The factor's direction.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FdmHullWhiteOp(FdmMesher mesher, HullWhite model, int direction)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(model);
        this.direction = direction;
        x = [.. mesher.Locations(direction).AsSpan()];
        int n = mesher.Layout.Size;
        double a = model.A(), sigma = model.Sigma;
        dzMap = new FirstDerivativeOp(direction, mesher).Mult(FdmG2Op.Scaled(x, -1.0, a))
            .Add(new SecondDerivativeOp(direction, mesher).Mult(FdmG2Op.Filled(n, 0.5 * sigma * sigma * 1.0)));
        mapT = new TripleBandLinearOp(direction, mesher);
        dynamics = model.Dynamics();
        diagonal = new double[n];
    }

    /// <inheritdoc/>
    public override int Size => 1;

    /// <inheritdoc/>
    /// <remarks>The step's ends are in order, <paramref name="t1"/> at most <paramref name="t2"/>, as QuantLib requires.</remarks>
    public override void SetTime(double t1, double t2)
    {
        double phi = 0.5 * (dynamics.ShortRate(t1, 0.0) + dynamics.ShortRate(t2, 0.0));

        // -(x_ + phi)
        for (int i = 0; i < diagonal.Length; ++i)
        {
            diagonal[i] = -(x[i] + phi);
        }

        mapT.SetSumWithDiagonal(dzMap, diagonal);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result) => mapT.Apply(r, result);

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => result[..r.Length].Clear();

    /// <inheritdoc/>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == this.direction)
        {
            mapT.Apply(r, result);
        }
        else
        {
            result[..r.Length].Clear();
        }
    }

    /// <inheritdoc/>
    /// <remarks>Another direction's solution is zero, as QuantLib's.</remarks>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == this.direction)
        {
            mapT.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            result[..r.Length].Clear();
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        SolveSplitting(direction, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() => [mapT.ToMatrix()];
}
