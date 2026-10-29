// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmextendedornsteinuhlenbeckop.hpp ql/experimental/finitedifferences/fdmextendedornsteinuhlenbeckop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The finite-difference operator of an <see cref="ExtendedOrnsteinUhlenbeckProcess"/> in one direction of a mesher:
/// its drift at the step's mid-time, its diffusion, and the discount at the forward rate over the step.
/// </summary>
/// <remarks>
/// A finite-difference operator is a solver's working state: <see cref="SetTime"/> sets the step's map in place, as
/// QuantLib's does. QuantLib's boundary conditions, which it holds and never reads, are held likewise.
/// </remarks>
[Experimental("NQL0001")]
public sealed class FdmExtendedOrnsteinUhlenbeckOp : FdmLinearOpComposite
{
    private readonly FdmMesher mesher;
    private readonly ExtendedOrnsteinUhlenbeckProcess process;
    private readonly YieldTermStructure rTS;
    private readonly int direction;
    private readonly Vector x;
    private readonly FirstDerivativeOp dxMap;
    private readonly TripleBandLinearOp dxxMap;
    private readonly TripleBandLinearOp mapX;
    private readonly Vector drift;
    private readonly Vector minusR = new(1);

    /// <summary>Initializes a new instance of the <see cref="FdmExtendedOrnsteinUhlenbeckOp"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="process">The process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="bcSet">The boundary conditions, held as QuantLib holds them.</param>
    /// <param name="direction">The process's direction on the mesher.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FdmExtendedOrnsteinUhlenbeckOp(
        FdmMesher mesher,
        ExtendedOrnsteinUhlenbeckProcess process,
        YieldTermStructure rTS,
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet,
        int direction = 0)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(rTS);
        this.mesher = mesher;
        this.process = process;
        this.rTS = rTS;
        BcSet = bcSet.IsDefault ? [] : bcSet;
        this.direction = direction;
        x = mesher.Locations(direction);
        dxMap = new FirstDerivativeOp(direction, mesher);
        int n = mesher.Layout.Size;
        double vol = process.Volatility;
        dxxMap = new SecondDerivativeOp(direction, mesher).Mult(0.5 * (vol * vol) * new Vector(n, 1.0));
        mapX = new TripleBandLinearOp(direction, mesher);
        drift = new Vector(n);
    }

    /// <summary>Gets the boundary conditions, which QuantLib holds and never reads.</summary>
    public ImmutableArray<BoundaryCondition<FdmLinearOp>> BcSet { get; }

    /// <inheritdoc/>
    public override int Size => mesher.Layout.Dim.Length;

    /// <inheritdoc/>
    public override void SetTime(double t1, double t2)
    {
        double r = rTS.ContinuousForwardRate(t1, t2);
        int n = mesher.Layout.Size;
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int k = 0; k < n; ++k, iter.Increment())
        {
            int i = iter.Index;
            drift[i] = process.Drift(0.5 * (t1 + t2), x[i]);
        }

        minusR[0] = -r;
        mapX.Axpyb(drift, dxMap, dxxMap, minusR);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result) => mapX.Apply(r, result);

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result) => result[..r.Length].Clear();

    /// <inheritdoc/>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        if (direction == this.direction)
        {
            mapX.Apply(r, result);
        }
        else
        {
            result[..r.Length].Clear();
        }
    }

    /// <inheritdoc/>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == this.direction)
        {
            mapX.SolveSplitting(r, s, 1.0, result);
        }
        else
        {
            r.CopyTo(result);
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        SolveSplitting(direction, r, s, result);

    /// <inheritdoc/>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp() => [mapX.ToMatrix()];
}
