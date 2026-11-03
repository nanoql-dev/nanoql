// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmdirichletboundary.hpp ql/methods/finitedifferences/utilities/fdmdirichletboundary.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// A Dirichlet boundary condition: a fixed value on one side of one direction of the grid, written into the solution
/// after each application of the operator and each solve.
/// </summary>
/// <remarks>
/// Immutable, its indices fixed at construction; the schemes call it in QuantLib's order, and it writes into the
/// caller's span, allocating nothing.
/// </remarks>
public sealed class FdmDirichletBoundary : BoundaryCondition<FdmLinearOp>
{
    private readonly BoundaryConditionSide side;
    private readonly double valueOnBoundary;
    private readonly ImmutableArray<int> indices;

    // one past the boundary's last index
    private readonly int end;
    private readonly double xExtreme;

    /// <summary>Initializes a new instance of the <see cref="FdmDirichletBoundary"/> class.</summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="valueOnBoundary">The value on the boundary.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="side">The side: <see cref="BoundaryConditionSide.Lower"/> or
    /// <see cref="BoundaryConditionSide.Upper"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mesher"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The direction isn't one of the mesher's, where QuantLib reads past its vectors' ends.
    /// </exception>
    /// <exception cref="ArgumentException">The side is <see cref="BoundaryConditionSide.None"/>, with QuantLib's
    /// "internal error".</exception>
    public FdmDirichletBoundary(FdmMesher mesher, double valueOnBoundary, int direction, BoundaryConditionSide side)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        this.side = side;
        this.valueOnBoundary = valueOnBoundary;
        indices = new FdmIndicesOnBoundary(mesher.Layout, direction, side).Indices;
        end = indices.IsEmpty ? 0 : indices.Max() + 1;
        if (side == BoundaryConditionSide.Lower)
        {
            xExtreme = mesher.Locations(direction)[0];
        }
        else if (side == BoundaryConditionSide.Upper)
        {
            xExtreme = mesher.Locations(direction)[mesher.Layout.Dim[direction] - 1];
        }
        else
        {
            throw new ArgumentException("internal error", nameof(side));
        }
    }

    /// <inheritdoc/>
    public override void ApplyBeforeApplying(FdmLinearOp op)
    {
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="a"/> doesn't reach the boundary's last index, where QuantLib writes past its end.
    /// </exception>
    public override void ApplyAfterApplying(Span<double> a)
    {
        if (!(a.Length >= end))
        {
            throw new ArgumentException($"{a.Length} values don't reach the boundary's index {end - 1}", nameof(a));
        }

        foreach (int i in indices)
        {
            a[i] = valueOnBoundary;
        }
    }

    /// <inheritdoc/>
    public override void ApplyBeforeSolving(FdmLinearOp op, Span<double> rhs)
    {
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="a"/> doesn't reach the boundary's last index, where QuantLib writes past its end.
    /// </exception>
    public override void ApplyAfterSolving(Span<double> a) => ApplyAfterApplying(a);

    /// <inheritdoc/>
    public override void SetTime(double t)
    {
    }

    /// <summary>
    /// A value at a location, the boundary's value beyond the boundary's extreme location and the given value inside.
    /// </summary>
    /// <param name="x">The location.</param>
    /// <param name="value">The value.</param>
    /// <returns>The boundary's value below a lower boundary or above an upper one; otherwise the value.</returns>
    public double ApplyAfterApplying(double x, double value) =>
        ((side == BoundaryConditionSide.Lower && x < xExtreme) || (side == BoundaryConditionSide.Upper && x > xExtreme))
            ? valueOnBoundary
            : value;
}
