// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/projectedconstraint.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// A constraint on the free parameters of a <see cref="Projection"/>: a constraint on all the parameters, tested and
/// bounded with the fixed ones included.
/// </summary>
/// <remarks>
/// It holds a copy of the projection's state, as QuantLib's holds a <c>Projection</c> by value, so a projection's
/// overrides of <see cref="Projection.Project"/> and <see cref="Projection.Include"/> aren't the ones it calls; the
/// projection's refusals reach its caller as the projection raises them.
/// </remarks>
public sealed class ProjectedConstraint : Constraint
{
    private readonly Constraint constraint;
    private readonly Projection projection;

    /// <summary>Initializes a new instance of the <see cref="ProjectedConstraint"/> class on a projection it builds.</summary>
    /// <param name="constraint">The constraint on all the parameters.</param>
    /// <param name="parameterValues">The values of all the parameters.</param>
    /// <param name="fixParameters">Whether each parameter is fixed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="constraint"/> is null.</exception>
    /// <exception cref="ArgumentException">The projection refuses the values or the flags.</exception>
    public ProjectedConstraint(Constraint constraint, ReadOnlySpan<double> parameterValues, ReadOnlySpan<bool> fixParameters)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        this.constraint = constraint;
        projection = new Projection(parameterValues, fixParameters);
    }

    /// <summary>Initializes a new instance of the <see cref="ProjectedConstraint"/> class on a copy of a projection.</summary>
    /// <param name="constraint">The constraint on all the parameters.</param>
    /// <param name="projection">The projection.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ProjectedConstraint(Constraint constraint, Projection projection)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        ArgumentNullException.ThrowIfNull(projection);
        this.constraint = constraint;
        this.projection = new Projection(projection);
    }

    /// <summary>Whether the free parameters, with the fixed ones included, satisfy the constraint.</summary>
    /// <param name="p">The free parameters.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="p"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="p"/> isn't one value per free parameter.</exception>
    public override bool Test(Vector p) => constraint.Test(projection.Include(p));

    /// <inheritdoc/>
    protected override Vector UpperBoundCore(Vector parameters) =>
        projection.Project(constraint.UpperBound(projection.Include(parameters)));

    /// <inheritdoc/>
    protected override Vector LowerBoundCore(Vector parameters) =>
        projection.Project(constraint.LowerBound(projection.Include(parameters)));
}
