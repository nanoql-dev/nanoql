// SPDX-FileCopyrightText: Copyright (C) 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/schemes/boundaryconditionschemehelper.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>Applies a set of boundary conditions in turn, as the schemes do around their applies and solves.</summary>
/// <remarks>Immutable itself; the conditions it holds are the caller's.</remarks>
public sealed class BoundaryConditionSchemeHelper
{
    private readonly ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet;

    /// <summary>Initializes a new instance of the <see cref="BoundaryConditionSchemeHelper"/> class.</summary>
    /// <param name="bcSet">The boundary conditions; <c>default</c> or empty for none.</param>
    /// <exception cref="ArgumentException">A condition is <see langword="null"/>.</exception>
    public BoundaryConditionSchemeHelper(ImmutableArray<BoundaryCondition<FdmLinearOp>> bcSet)
    {
        this.bcSet = bcSet.IsDefault ? [] : bcSet;
        if (this.bcSet.Any(c => c is null))
        {
            throw new ArgumentException("null boundary condition in set", nameof(bcSet));
        }
    }

    /// <summary>Applies each condition before the operator is applied.</summary>
    /// <param name="op">The operator.</param>
    public void ApplyBeforeApplying(FdmLinearOp op)
    {
        foreach (BoundaryCondition<FdmLinearOp> i in bcSet)
        {
            i.ApplyBeforeApplying(op);
        }
    }

    /// <summary>Applies each condition before the operator's system is solved.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="a">The right-hand side, modified in place.</param>
    public void ApplyBeforeSolving(FdmLinearOp op, Span<double> a)
    {
        foreach (BoundaryCondition<FdmLinearOp> i in bcSet)
        {
            i.ApplyBeforeSolving(op, a);
        }
    }

    /// <summary>Applies each condition after the operator is applied.</summary>
    /// <param name="a">The values, modified in place.</param>
    public void ApplyAfterApplying(Span<double> a)
    {
        foreach (BoundaryCondition<FdmLinearOp> i in bcSet)
        {
            i.ApplyAfterApplying(a);
        }
    }

    /// <summary>Applies each condition after the operator's system is solved.</summary>
    /// <param name="a">The values, modified in place.</param>
    public void ApplyAfterSolving(Span<double> a)
    {
        foreach (BoundaryCondition<FdmLinearOp> i in bcSet)
        {
            i.ApplyAfterSolving(a);
        }
    }

    /// <summary>Sets each condition's time.</summary>
    /// <param name="t">The time.</param>
    public void SetTime(double t)
    {
        foreach (BoundaryCondition<FdmLinearOp> i in bcSet)
        {
            i.SetTime(t);
        }
    }
}
