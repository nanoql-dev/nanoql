// SPDX-FileCopyrightText: Copyright (C) 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmdiscountdirichletboundary.hpp ql/methods/finitedifferences/utilities/fdmdiscountdirichletboundary.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// A Dirichlet boundary condition holding a cash flow paid at maturity: on one side of one direction of the grid, its
/// value discounted from maturity to each step's time on the curve, D(T)/D(t).
/// </summary>
/// <remarks>
/// A working type, as the <see cref="FdmTimeDepDirichletBoundary"/> it applies through. The times are the curve's own,
/// as QuantLib reads them.
/// </remarks>
public sealed class FdmDiscountDirichletBoundary : BoundaryCondition<FdmLinearOp>
{
    private readonly FdmTimeDepDirichletBoundary bc;

    /// <summary>Initializes a new instance of the <see cref="FdmDiscountDirichletBoundary"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="maturityTime">The cash flow's time on the curve.</param>
    /// <param name="valueOnBoundary">The cash flow.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="side">The side: <see cref="BoundaryConditionSide.Lower"/> or
    /// <see cref="BoundaryConditionSide.Upper"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the mesher's.</exception>
    public FdmDiscountDirichletBoundary(
        FdmMesher mesher,
        YieldTermStructure rTS,
        double maturityTime,
        double valueOnBoundary,
        int direction,
        BoundaryConditionSide side)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(rTS);
        bc = new FdmTimeDepDirichletBoundary(
            mesher, t => valueOnBoundary * rTS.Discount(maturityTime) / rTS.Discount(t), direction, side);
    }

    /// <inheritdoc/>
    public override void SetTime(double t) => bc.SetTime(t);

    /// <inheritdoc/>
    public override void ApplyBeforeApplying(FdmLinearOp op) => bc.ApplyBeforeApplying(op);

    /// <inheritdoc/>
    public override void ApplyBeforeSolving(FdmLinearOp op, Span<double> rhs) => bc.ApplyBeforeSolving(op, rhs);

    /// <inheritdoc/>
    public override void ApplyAfterApplying(Span<double> a) => bc.ApplyAfterApplying(a);

    /// <inheritdoc/>
    public override void ApplyAfterSolving(Span<double> a) => bc.ApplyAfterSolving(a);
}
