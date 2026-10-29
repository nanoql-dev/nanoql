// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmklugeextouop.hpp ql/experimental/finitedifferences/fdmklugeextouop.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The finite-difference operator of a <see cref="KlugeExtOUProcess"/> on a three-dimensional mesher: the power
/// process's operator (<see cref="FdmExtOUJumpOp"/>) in directions 0 and 1, the gas process's Ornstein-Uhlenbeck
/// operator in direction 2 at a zero rate (the power operator discounts), and the correlation of X and the gas price as
/// a mixed derivative.
/// </summary>
/// <remarks>
/// A finite-difference operator is a solver's working state: <see cref="SetTime"/> sets both parts in place, as
/// QuantLib's does.
/// </remarks>
[Experimental("NQL0001")]
public sealed class FdmKlugeExtOUOp : FdmLinearOpComposite
{
    private readonly FdmMesher mesher;
    private readonly FdmExtOUJumpOp klugeOp;
    private readonly FdmExtendedOrnsteinUhlenbeckOp ouOp;
    private readonly NinePointLinearOp corrMap;

    // the parts' images, held
    private readonly double[] first;
    private readonly double[] second;

    /// <summary>Initializes a new instance of the <see cref="FdmKlugeExtOUOp"/> class.</summary>
    /// <param name="mesher">The three-dimensional mesher, X, Y and the gas price in directions 0, 1 and 2.</param>
    /// <param name="klugeOUProcess">The process.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="bcSet">The boundary conditions.</param>
    /// <param name="integroIntegrationOrder">The jump integral's Gauss-Laguerre order.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FdmKlugeExtOUOp(
        FdmMesher mesher,
        KlugeExtOUProcess klugeOUProcess,
        YieldTermStructure rTS,
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet,
        int integroIntegrationOrder)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(klugeOUProcess);
        ArgumentNullException.ThrowIfNull(rTS);
        this.mesher = mesher;
        ExtOUWithJumpsProcess kluge = klugeOUProcess.KlugeProcess;
        ExtendedOrnsteinUhlenbeckProcess extOU = klugeOUProcess.ExtOUProcess;
        ImmutableArray<BoundaryCondition<FdmLinearOp>> bc = bcSet.IsDefault ? [] : bcSet;
        klugeOp = new FdmExtOUJumpOp(mesher, kluge, rTS, bc, integroIntegrationOrder);
        ouOp = new FdmExtendedOrnsteinUhlenbeckOp(mesher, extOU, new FlatForward(rTS.ReferenceDate, 0.0, rTS.DayCounter), bc, 2);
        int n = mesher.Layout.Size;
        corrMap = new SecondOrderMixedDerivativeOp(0, 2, mesher).Mult(
            new Vector(n, klugeOUProcess.Rho * extOU.Volatility * kluge.ExtendedOrnsteinUhlenbeckProcess.Volatility));
        first = new double[n];
        second = new double[n];
    }

    /// <inheritdoc/>
    public override int Size => mesher.Layout.Dim.Length;

    /// <inheritdoc/>
    public override void SetTime(double t1, double t2)
    {
        ouOp.SetTime(t1, t2);
        klugeOp.SetTime(t1, t2);
    }

    /// <inheritdoc/>
    public override void Apply(ReadOnlySpan<double> r, Span<double> result)
    {
        // QuantLib's ouOp.apply(r) + klugeOp.apply(r) + corrMap.apply(r), summed in that order
        ouOp.Apply(r, result);
        klugeOp.Apply(r, first);
        corrMap.Apply(r, second);
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] = result[i] + first[i] + second[i];
        }
    }

    /// <inheritdoc/>
    public override void ApplyMixed(ReadOnlySpan<double> r, Span<double> result)
    {
        corrMap.Apply(r, result);
        klugeOp.ApplyMixed(r, first);
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] += first[i];
        }
    }

    /// <inheritdoc/>
    public override void ApplyDirection(int direction, ReadOnlySpan<double> r, Span<double> result)
    {
        klugeOp.ApplyDirection(direction, r, result);
        ouOp.ApplyDirection(direction, r, first);
        for (int i = 0; i < r.Length; ++i)
        {
            result[i] += first[i];
        }
    }

    /// <inheritdoc/>
    public override void SolveSplitting(int direction, ReadOnlySpan<double> r, double s, Span<double> result)
    {
        if (direction == 0 || direction == 1)
        {
            klugeOp.SolveSplitting(direction, r, s, result);
        }
        else if (direction == 2)
        {
            ouOp.SolveSplitting(direction, r, s, result);
        }
        else
        {
            r.CopyTo(result);
        }
    }

    /// <inheritdoc/>
    public override void Preconditioner(ReadOnlySpan<double> r, double s, Span<double> result) =>
        klugeOp.SolveSplitting(0, r, s, result);

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The operator has boundary conditions ("boundary conditions are not
    /// supported").</exception>
    public override ImmutableArray<SparseMatrix> ToMatrixDecomp()
    {
        ImmutableArray<SparseMatrix> klugeDecomp = klugeOp.ToMatrixDecomp();
        return [klugeDecomp[0], klugeDecomp[1], ouOp.ToMatrixDecomp()[0], corrMap.ToMatrix() + klugeDecomp[2]];
    }
}
