// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmdupire1dop.hpp ql/experimental/finitedifferences/fdmdupire1dop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The Dupire local volatility pricing operator in the strike, ½σ²(K)·∂²/∂K², time reversed so that backward solvers
/// work.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-eq plan's SEQR1), its band built at construction and never changed, so a step allocates
/// nothing (the S-g1d plan's G1D11). Experimental, as in QuantLib.
/// </para>
/// <para>
/// One of QuantLib's defects is fixed (G1R6 (xi)): QuantLib's <c>apply_mixed</c> returns its input, where a 1-D
/// operator has no mixed part; here it is zero, as every other 1-D operator's.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public sealed class FdmDupire1dOp : FdmLinearOpComposite
{
    private readonly TripleBandLinearOp mapT;

    /// <summary>Initializes a new instance of the <see cref="FdmDupire1dOp"/> class.</summary>
    /// <param name="mesher">The mesher, its first direction the strike.</param>
    /// <param name="localVolatility">The local volatility at each of the mesher's points.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The local volatilities aren't one per point.</exception>
    public FdmDupire1dOp(FdmMesher mesher, Vector localVolatility)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(localVolatility);
        mapT = new SecondDerivativeOp(0, mesher).Mult(0.5 * localVolatility * localVolatility);
    }

    /// <inheritdoc/>
    public override int Size => 1;

    /// <inheritdoc/>
    public override void SetTime(double t1, double t2)
    {
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result) => mapT.Apply(r, result);

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> isn't 0.</exception>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == 0)
        {
            mapT.Apply(r, result);
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
    }

    /// <inheritdoc/>
    /// <remarks>Zero, where QuantLib's returns its input (G1R6 (xi)).</remarks>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => result[..r.Length].Clear();

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> isn't 0.</exception>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == 0)
        {
            mapT.SolveSplitting(r, s, 1.0, result);
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction too large");
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) => SolveSplitting(0, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() => [mapT.ToMatrix()];
}
