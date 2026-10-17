// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/method.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The base class of constrained optimization methods.</summary>
/// <remarks>
/// <para>
/// A method holds only its configuration, so it is immutable; its working state lives inside each minimization.
/// <see cref="Minimize(Problem, EndCriteria)"/> starts from the problem's initial value and returns the results;
/// <see cref="Minimize(ProblemState, EndCriteria)"/>, QuantLib's <c>minimize(Problem&amp;)</c>, continues from the
/// state's current value and leaves the results in it, for line searches and hybrid methods. Both call
/// <see cref="MinimizeCore"/>.
/// </para>
/// <para>
/// So a second minimization of one QuantLib <c>Problem</c> starts from the first's optimum, where
/// <see cref="Minimize(Problem, EndCriteria)"/> starts from the initial value each time.
/// </para>
/// <para>
/// An extension point, as in QuantLib: each optimization method derives from it.
/// </para>
/// </remarks>
public abstract class OptimizationMethod
{
    /// <summary>Initializes a new instance of the <see cref="OptimizationMethod"/> class.</summary>
    protected OptimizationMethod()
    {
    }

    /// <summary>Minimizes the problem, starting from its initial value.</summary>
    /// <param name="problem">The problem.</param>
    /// <param name="endCriteria">The criteria that end the minimization.</param>
    /// <returns>The results: the current value, the function value, the gradient norm, the counts and the end criterion.</returns>
    public ProblemResults Minimize(Problem problem, EndCriteria endCriteria)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(endCriteria);
        var state = new ProblemState(problem);
        EndCriteriaType type = MinimizeCore(state, endCriteria);
        return new ProblemResults(
            [.. state.CurrentValue.AsSpan()],
            state.FunctionValue,
            state.GradientNormValue,
            state.FunctionEvaluation,
            state.GradientEvaluation,
            type);
    }

    /// <summary>Minimizes from the state's current value, leaving the results in the state.</summary>
    /// <param name="state">The working state, which the minimization changes.</param>
    /// <param name="endCriteria">The criteria that end the minimization.</param>
    /// <returns>The end criterion met.</returns>
    public EndCriteriaType Minimize(ProblemState state, EndCriteria endCriteria)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(endCriteria);
        return MinimizeCore(state, endCriteria);
    }

    /// <summary>The method's algorithm, QuantLib's <c>minimize</c>: minimizes from the state's current value.</summary>
    /// <param name="P">The working state, which the minimization changes.</param>
    /// <param name="endCriteria">The criteria that end the minimization.</param>
    /// <returns>The end criterion met.</returns>
    protected abstract EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria);
}
