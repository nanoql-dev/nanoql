// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/problem.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The working state of a minimization: QuantLib's <c>Problem</c>, with its cost function and constraint, its
/// current value, and the function value, gradient norm and evaluation counts a method leaves in it.
/// </summary>
/// <remarks>
/// <para>
/// A working type: mutable, owned by one computation and not thread-safe. An optimization method works through it,
/// and a line search or a hybrid method passes it on, as QuantLib's pass their <c>Problem&amp;</c>:
/// <see cref="OptimizationMethod.Minimize(ProblemState, EndCriteria)"/> continues from <see cref="CurrentValue"/>.
/// </para>
/// <para>
/// <see cref="Value"/>, <see cref="Values"/>, <see cref="Gradient"/> and <see cref="ValueAndGradient"/> call the cost
/// function and count the evaluations, as QuantLib's do. A new state starts from the problem's initial value with no
/// evaluations and no function value, as after <see cref="Reset"/>, where QuantLib leaves the counts and values
/// uninitialized until its methods reset them.
/// </para>
/// </remarks>
[WorkingType]
public sealed class ProblemState
{
    private Vector currentValue;

    /// <summary>Initializes a new instance of the <see cref="ProblemState"/> class, at the problem's initial value.</summary>
    /// <param name="problem">The problem.</param>
    public ProblemState(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        CostFunction = problem.CostFunction;
        Constraint = problem.Constraint;
        currentValue = new Vector(problem.InitialValue.AsSpan());
    }

    /// <summary>Gets the cost function.</summary>
    public CostFunction CostFunction { get; }

    /// <summary>Gets the constraint.</summary>
    public Constraint Constraint { get; }

    /// <summary>
    /// Gets or sets the current value of the local minimum. The getter returns the state's own vector; the setter
    /// copies its argument, as QuantLib's <c>setCurrentValue</c> takes its <c>Array</c> by value.
    /// </summary>
    /// <remarks>
    /// <c>state.CurrentValue += v</c> calls the binary operator and the setter, so the state gets a new vector and a
    /// vector read from the getter before it keeps the old value.
    /// </remarks>
    public Vector CurrentValue
    {
        get => currentValue;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            currentValue = value.Clone();
        }
    }

    /// <summary>
    /// Gets or sets the value of the cost function at the current value, the last step's; <see langword="null"/>
    /// (QuantLib's <c>Null&lt;Real&gt;()</c>) after <see cref="Reset"/>.
    /// </summary>
    public double? FunctionValue { get; set; }

    /// <summary>
    /// Gets or sets the value of the cost function's gradient norm at the current value, the last step's;
    /// <see langword="null"/> (QuantLib's <c>Null&lt;Real&gt;()</c>) after <see cref="Reset"/>.
    /// </summary>
    public double? GradientNormValue { get; set; }

    /// <summary>Gets the number of evaluations of the cost function since the last <see cref="Reset"/>.</summary>
    public int FunctionEvaluation { get; private set; }

    /// <summary>Gets the number of evaluations of the cost function's gradient since the last <see cref="Reset"/>.</summary>
    public int GradientEvaluation { get; private set; }

    /// <summary>
    /// Sets the evaluation counts to 0 and the function value and gradient norm to <see langword="null"/>. It doesn't
    /// reset the current value.
    /// </summary>
    public void Reset()
    {
        FunctionEvaluation = GradientEvaluation = 0;
        FunctionValue = GradientNormValue = null;
    }

    /// <summary>The cost function's value, counting a function evaluation.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The cost.</returns>
    public double Value(Vector x)
    {
        ++FunctionEvaluation;
        return CostFunction.Value(x);
    }

    /// <summary>The cost function's values, counting a function evaluation.</summary>
    /// <param name="x">The point.</param>
    /// <returns>A new vector of the values.</returns>
    public Vector Values(Vector x)
    {
        ++FunctionEvaluation;
        return CostFunction.Values(x);
    }

    /// <summary>The cost function's gradient written into <paramref name="gradient"/>, counting a gradient evaluation.</summary>
    /// <param name="gradient">The gradient, at least of x's size.</param>
    /// <param name="x">The point.</param>
    public void Gradient(Vector gradient, Vector x)
    {
        ++GradientEvaluation;
        CostFunction.Gradient(gradient, x);
    }

    /// <summary>
    /// The cost function's gradient written into <paramref name="gradient"/>, and its value, counting a function and a
    /// gradient evaluation.
    /// </summary>
    /// <param name="gradient">The gradient, at least of x's size.</param>
    /// <param name="x">The point.</param>
    /// <returns>The cost.</returns>
    public double ValueAndGradient(Vector gradient, Vector x)
    {
        ++FunctionEvaluation;
        ++GradientEvaluation;
        return CostFunction.ValueAndGradient(gradient, x);
    }
}
