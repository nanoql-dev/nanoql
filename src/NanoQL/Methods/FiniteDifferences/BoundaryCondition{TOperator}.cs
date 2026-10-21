// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/boundarycondition.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>A boundary condition of a finite-difference problem, applied around an operator's applies and
/// solves.</summary>
/// <typeparam name="TOperator">The operator the condition modifies.</typeparam>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: later units derive their boundary conditions from it. A condition modifies the
/// values in place, through the caller's span. QuantLib's deprecated <c>NeumannBC</c> and <c>DirichletBC</c>, on the
/// old framework's tridiagonal operator, aren't ported.
/// </para>
/// </remarks>
public abstract class BoundaryCondition<TOperator>
{
    /// <summary>
    /// Modifies an operator L before it is applied to values u, so that v = Lu satisfies the condition.
    /// </summary>
    /// <param name="op">The operator.</param>
    public abstract void ApplyBeforeApplying(TOperator op);

    /// <summary>Modifies values u, in place, so that they satisfy the condition.</summary>
    /// <param name="a">The values.</param>
    public abstract void ApplyAfterApplying(Span<double> a);

    /// <summary>
    /// Modifies an operator L and the right-hand side before the system Lu' = u is solved, so that u' satisfies the
    /// condition.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="rhs">The right-hand side, modified in place.</param>
    public abstract void ApplyBeforeSolving(TOperator op, Span<double> rhs);

    /// <summary>Modifies values u, in place, so that they satisfy the condition.</summary>
    /// <param name="a">The values.</param>
    public abstract void ApplyAfterSolving(Span<double> a);

    /// <summary>Sets the current time of a time-dependent condition.</summary>
    /// <param name="t">The time.</param>
    public abstract void SetTime(double t);
}
