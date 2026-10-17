// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/problem.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Numerics;

/// <summary>A constrained optimization problem: a cost function, a constraint and an initial value.</summary>
/// <remarks>
/// Immutable: it copies its initial value. QuantLib's <c>Problem</c> also holds what a minimization changes (the
/// current value, the function value, the evaluation counts); here
/// <see cref="OptimizationMethod.Minimize(Problem, EndCriteria)"/> returns those as a <see cref="ProblemResults"/>,
/// and a method works on a <see cref="ProblemState"/>, built from the problem, which carries QuantLib's API.
/// </remarks>
public sealed class Problem
{
    /// <summary>Initializes a new instance of the <see cref="Problem"/> class.</summary>
    /// <param name="costFunction">The cost function.</param>
    /// <param name="constraint">The constraint.</param>
    /// <param name="initialValue">The initial value, which it copies; empty by default.</param>
    /// <exception cref="ArgumentNullException">
    /// The cost function, or the constraint, is <see langword="null"/>: QuantLib's "empty constraint given".
    /// </exception>
    public Problem(CostFunction costFunction, Constraint constraint, ReadOnlySpan<double> initialValue = default)
    {
        ArgumentNullException.ThrowIfNull(costFunction);
        ArgumentNullException.ThrowIfNull(constraint);
        CostFunction = costFunction;
        Constraint = constraint;
        InitialValue = [.. initialValue];
    }

    /// <summary>Gets the cost function.</summary>
    public CostFunction CostFunction { get; }

    /// <summary>Gets the constraint.</summary>
    public Constraint Constraint { get; }

    /// <summary>Gets the initial value, from which each minimization of the problem starts.</summary>
    public ImmutableArray<double> InitialValue { get; }
}
