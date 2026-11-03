// SPDX-FileCopyrightText: Copyright (C) 2012 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmtimedepdirichletboundary.hpp ql/methods/finitedifferences/utilities/fdmtimedepdirichletboundary.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Methods;

/// <summary>
/// A time-dependent Dirichlet boundary condition: on one side of one direction of the grid, the value a function of
/// time gives at each step, one value for the whole side or one for each of its points, written into the solution
/// after each application of the operator and each solve.
/// </summary>
/// <remarks>
/// A working type: <see cref="SetTime"/> evaluates the function and holds its values for the step; the schemes call it
/// in QuantLib's order. Its values before the first <see cref="SetTime"/> are 0, where QuantLib's are uninitialized.
/// </remarks>
[WorkingType]
public sealed class FdmTimeDepDirichletBoundary : BoundaryCondition<FdmLinearOp>
{
    private readonly ImmutableArray<int> indices;
    private readonly Func<double, double>? valueOnBoundary;
    private readonly Func<double, ImmutableArray<double>>? valuesOnBoundary;

    // the side's values for the step: filled from valueOnBoundary in place, or the function's array
    private readonly double[] filled;
    private ImmutableArray<double> values;

    /// <summary>
    /// Initializes a new instance of the <see cref="FdmTimeDepDirichletBoundary"/> class with one value for the side.
    /// </summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="valueOnBoundary">The value on the boundary at a time.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="side">The side: <see cref="BoundaryConditionSide.Lower"/> or
    /// <see cref="BoundaryConditionSide.Upper"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the mesher's.</exception>
    public FdmTimeDepDirichletBoundary(
        FdmMesher mesher, Func<double, double> valueOnBoundary, int direction, BoundaryConditionSide side)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(valueOnBoundary);
        indices = new FdmIndicesOnBoundary(mesher.Layout, direction, side).Indices;
        this.valueOnBoundary = valueOnBoundary;
        filled = new double[indices.Length];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FdmTimeDepDirichletBoundary"/> class with a value for each point
    /// of the side.
    /// </summary>
    /// <param name="mesher">The mesher.</param>
    /// <param name="valuesOnBoundary">
    /// The values on the boundary at a time, one for each of the side's points in the layout's order.
    /// </param>
    /// <param name="direction">The direction.</param>
    /// <param name="side">The side: <see cref="BoundaryConditionSide.Lower"/> or
    /// <see cref="BoundaryConditionSide.Upper"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the mesher's.</exception>
    public FdmTimeDepDirichletBoundary(
        FdmMesher mesher, Func<double, ImmutableArray<double>> valuesOnBoundary, int direction, BoundaryConditionSide side)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        ArgumentNullException.ThrowIfNull(valuesOnBoundary);
        indices = new FdmIndicesOnBoundary(mesher.Layout, direction, side).Indices;
        this.valuesOnBoundary = valuesOnBoundary;
        filled = new double[indices.Length];
        values = [.. filled];
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The function's values are the default array.</exception>
    public override void SetTime(double t)
    {
        if (valueOnBoundary is not null)
        {
            Array.Fill(filled, valueOnBoundary(t));
        }
        else
        {
            ImmutableArray<double> v = valuesOnBoundary!(t);
            if (v.IsDefault)
            {
                throw new ArgumentException("default(ImmutableArray) is not an array of boundary values", nameof(t));
            }

            values = v;
        }
    }

    /// <inheritdoc/>
    public override void ApplyBeforeApplying(FdmLinearOp op)
    {
    }

    /// <inheritdoc/>
    public override void ApplyBeforeSolving(FdmLinearOp op, Span<double> rhs)
    {
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The function gave another number of values than the side has points.</exception>
    public override void ApplyAfterApplying(Span<double> a)
    {
        ReadOnlySpan<double> v = valueOnBoundary is not null ? filled : values.AsSpan();
        if (indices.Length != v.Length)
        {
            throw new NanoQLException(
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"values on boundary size ({v.Length}) does not match hypersurface size ({indices.Length})"));
        }

        for (int i = 0; i < indices.Length; ++i)
        {
            a[indices[i]] = v[i];
        }
    }

    /// <inheritdoc/>
    public override void ApplyAfterSolving(Span<double> a) => ApplyAfterApplying(a);
}
