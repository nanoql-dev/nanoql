// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/fdmcevop.hpp ql/methods/finitedifferences/operators/fdmcevop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The backward operator of the constant elasticity of variance (CEV) process df = α·f^β·dW, absorbed at f = 0, in
/// one direction of a mesher: the diffusion ½α²f^{2β}, discounting at the curve's forward rate.
/// </summary>
/// <remarks>
/// <para>
/// A working type: <see cref="SetTime"/> rewrites the step's operator in place through a buffer it holds, and the apply
/// and splitting solves write into the caller's spans, so a step allocates nothing. The forward rate is the curve's
/// over its own times, read through <see cref="YieldTermStructure"/>'s internal <c>ContinuousForwardRate</c>, its bits
/// <c>ForwardRate</c>'s.
/// </para>
/// <para>
/// The second derivative is taken in the direction given, where QuantLib's is built on direction 0 whatever the
/// direction (<c>fdmcevop.cpp:39-40</c>), so that in another direction of a mesher of more than one it differentiates
/// along the wrong axis: QuantLib's operator in direction 1 of a two-dimensional mesher differs from its operator in
/// direction 0 of the transposed mesher. In direction 0, QuantLib's bits.
/// </para>
/// </remarks>
[WorkingType]
public sealed class FdmCEVOp : FdmLinearOpComposite
{
    private readonly YieldTermStructure rTS;
    private readonly int direction;
    private readonly TripleBandLinearOp dxxMap;
    private readonly TripleBandLinearOp mapT;

    // the step's diagonal, -r, held
    private readonly double[] minusR = new double[1];

    /// <summary>Initializes a new instance of the <see cref="FdmCEVOp"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="f0">The forward's value today, which QuantLib's operator doesn't read.</param>
    /// <param name="alpha">The volatility parameter α.</param>
    /// <param name="beta">The elasticity β.</param>
    /// <param name="direction">The forward's direction.</param>
    /// <exception cref="ArgumentNullException">The mesher or the curve is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FdmCEVOp(FdmMesher mesher, YieldTermStructure rTS, double f0, double alpha, double beta, int direction)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(rTS);
        _ = f0;
        this.rTS = rTS;
        this.direction = direction;

        // the direction given, where QuantLib's is 0
        dxxMap = new SecondDerivativeOp(direction, mesher)
            .Mult(0.5 * alpha * alpha * ArrayFunctions.Pow(mesher.Locations(direction), 2.0 * beta));
        mapT = new TripleBandLinearOp(direction, mesher);
    }

    /// <inheritdoc/>
    public override int Size => 1;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="t2"/> is before <paramref name="t1"/>, or the curve can't be read between them.
    /// </exception>
    public override void SetTime(double t1, double t2)
    {
        double r = rTS.ContinuousForwardRate(t1, t2);
        minusR[0] = -r;
        mapT.SetSumWithDiagonal(dxxMap, minusR);
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
