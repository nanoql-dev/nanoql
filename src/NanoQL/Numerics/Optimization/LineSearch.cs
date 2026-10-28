// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/linesearch.hpp ql/math/optimization/linesearch.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The base class for line searches.</summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: the line searches derive from it (<see cref="ArmijoLineSearch"/>,
/// <see cref="GoldsteinLineSearch"/>). A line search is a working type (S2R2): <see cref="Evaluate"/> changes it,
/// and a search reads the state its previous call left (QuantLib's Armijo search reads the last gradient), so it
/// isn't thread-safe.
/// </para>
/// <para>
/// A <see cref="LineSearchBasedMethod"/> holds one as a prototype and runs each minimization on a new one,
/// <see cref="NewSearch"/>, so the method stays immutable and a minimization's result doesn't depend on the
/// minimizations before it, where QuantLib's method keeps one line search's state across them. The accessors return
/// the search's own vectors, valid until it next changes.
/// </para>
/// </remarks>
public abstract class LineSearch
{
    /// <summary>The current value of the search direction.</summary>
    private Vector searchDirection = new();

    /// <summary>Initializes a new instance of the <see cref="LineSearch"/> class.</summary>
    /// <param name="eps">An accuracy QuantLib's base accepts and reads nowhere.</param>
    protected LineSearch(double eps = 0.0)
    {
        _ = eps;
    }

    /// <summary>Gets the last point the search reached, x + t·d.</summary>
    public Vector LastX => Xtd;

    /// <summary>Gets the cost function's value at <see cref="LastX"/>.</summary>
    public double LastFunctionValue => Qt;

    /// <summary>Gets the cost function's gradient at <see cref="LastX"/>.</summary>
    public Vector LastGradient => GradientValue;

    /// <summary>Gets the squared norm of <see cref="LastGradient"/>.</summary>
    public double LastGradientNorm2 => Qpt;

    /// <summary>Gets a value indicating whether the last search succeeded.</summary>
    public bool Succeed => Succeeded;

    /// <summary>
    /// Gets or sets the current value of the search direction. The getter returns the search's own vector; the setter
    /// copies its argument, as QuantLib's assignment copies an <c>Array</c>.
    /// </summary>
    public Vector SearchDirection
    {
        get => searchDirection;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            searchDirection = value.Clone();
        }
    }

    /// <summary>Gets or sets the new x, QuantLib's <c>xtd_</c>.</summary>
    protected Vector Xtd { get; set; } = new();

    /// <summary>Gets or sets the gradient at the new x, QuantLib's <c>gradient_</c>.</summary>
    protected Vector GradientValue { get; set; } = new();

    /// <summary>Gets or sets the cost function's value at the new x, QuantLib's <c>qt_</c>.</summary>
    protected double Qt { get; set; }

    /// <summary>Gets or sets the gradient's squared norm at the new x, QuantLib's <c>qpt_</c>.</summary>
    protected double Qpt { get; set; }

    /// <summary>Gets or sets a value indicating whether the line search succeeded, QuantLib's <c>succeed_</c>.</summary>
    protected bool Succeeded { get; set; } = true;

    /// <summary>
    /// Performs the line search from the problem's current value along <see cref="SearchDirection"/>: QuantLib's
    /// <c>operator()</c>.
    /// </summary>
    /// <param name="P">The optimization problem's state.</param>
    /// <param name="ecType">The end criteria's outcome, which the search sets where it stops on one.</param>
    /// <param name="endCriteria">The end criteria.</param>
    /// <param name="t_ini">The initial value of the line-search step.</param>
    /// <returns>The step taken.</returns>
    public abstract double Evaluate(ProblemState P, ref EndCriteriaType ecType, EndCriteria endCriteria, double t_ini);

    /// <summary>
    /// Moves a point along a direction by a step, halving the step until the point satisfies the constraint.
    /// </summary>
    /// <param name="params">The point, moved in place.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="beta">The step to try first.</param>
    /// <param name="constraint">The constraint.</param>
    /// <returns>The step taken.</returns>
    /// <exception cref="NanoQLException">No step satisfies the constraint after 200 halvings.</exception>
    public double Update(Vector @params, Vector direction, double beta, Constraint constraint)
    {
        ArgumentNullException.ThrowIfNull(@params);
        ArgumentNullException.ThrowIfNull(direction);
        ArgumentNullException.ThrowIfNull(constraint);
        double diff = beta;
        Vector newParams = @params + (diff * direction);
        bool valid = constraint.Test(newParams);
        int icount = 0;
        while (!valid)
        {
            if (icount > 200)
            {
                throw new NanoQLException("can't update linesearch");
            }

            diff *= 0.5;
            icount++;
            newParams = @params + (diff * direction);
            valid = constraint.Test(newParams);
        }

        @params += diff * direction;
        return diff;
    }

    /// <summary>
    /// A new line search with this one's settings and none of its state, as built: what a
    /// <see cref="LineSearchBasedMethod"/> runs each minimization on.
    /// </summary>
    /// <returns>The new line search.</returns>
    protected internal abstract LineSearch NewSearch();
}
