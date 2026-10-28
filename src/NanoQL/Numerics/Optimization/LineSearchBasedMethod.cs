// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2009 Frédéric Degraeve
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/linesearchbasedmethod.hpp ql/math/optimization/linesearchbasedmethod.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using static NanoQL.Numerics.ArrayFunctions;

namespace NanoQL.Numerics;

/// <summary>A line-search-based optimization method.</summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: the methods derive from it and give the new search direction
/// (<see cref="ConjugateGradient"/>, <see cref="SteepestDescent"/>, <see cref="BFGS"/>). Immutable: it holds its line
/// search as a prototype and runs each minimization on a new one (<see cref="LineSearch.NewSearch"/>), which it passes
/// to <see cref="GetUpdatedDirection(ProblemState, double, Vector, LineSearch)"/>, where QuantLib's method mutates one
/// line search across minimizations and its derived classes read it as a member.
/// </para>
/// <para>
/// A method that keeps state through a minimization (BFGS's inverse Hessian, which QuantLib's keeps as a member across
/// minimizations) returns it from <see cref="NewMinimizationState"/> at each minimization's start and reads it in the
/// five-argument <see cref="GetUpdatedDirection(ProblemState, double, Vector, LineSearch, LineSearchBasedMethodState?)"/>
/// (the S-core plan's CR5 (a)); a method without state overrides the four-argument member, which the five-argument
/// one calls by default. A method overriding neither is refused at its first direction.
/// </para>
/// <para>
/// The problem's current value is the last point the line search accepted at every return, where QuantLib's
/// <c>minimize</c> returns on the function tolerance or the iteration cap before setting it, leaving the point before
/// with the function value of the point after: a documented divergence (<c>Divergences/LineSearchReturnTests.cs</c>).
/// </para>
/// </remarks>
public abstract class LineSearchBasedMethod : OptimizationMethod
{
    private readonly LineSearch lineSearch;

    /// <summary>Initializes a new instance of the <see cref="LineSearchBasedMethod"/> class.</summary>
    /// <param name="lSearch">The line search, a prototype each minimization runs a new copy of;
    /// <see langword="null"/> for an <see cref="ArmijoLineSearch"/> with its defaults.</param>
    protected LineSearchBasedMethod(LineSearch? lSearch = null)
    {
        lineSearch = lSearch?.NewSearch() ?? new ArmijoLineSearch();
    }

    /// <inheritdoc/>
    protected override EndCriteriaType MinimizeCore(ProblemState P, EndCriteria endCriteria)
    {
        // Initializations
        double ftol = endCriteria.FunctionEpsilon;
        int maxStationaryStateIterations = endCriteria.MaxStationaryStateIterations;
        EndCriteriaType ecType = EndCriteriaType.None;   // reset end criteria
        P.Reset();                                      // reset problem
        Vector x = P.CurrentValue.Clone();              // store the starting point
        int iterationNumber = 0;
        LineSearch search = lineSearch.NewSearch();
        // dimension line search
        search.SearchDirection = new Vector(x.Size);
        bool done = false;

        // function and squared norm of gradient values;
        double fnew, fold, gold2;
        double fdiff;
        // classical initial value for line-search step
        double t = 1.0;
        // Set gradient g at the size of the optimization problem
        // search direction
        int sz = search.SearchDirection.Size;
        Vector prevGradient = new(sz), direction;
        // Initialize cost function, gradient prevGradient and search direction
        P.FunctionValue = P.ValueAndGradient(prevGradient, x);
        P.GradientNormValue = DotProduct(prevGradient, prevGradient);
        search.SearchDirection = -prevGradient;

        bool firstTime = true;

        // the method's state for this minimization, a fresh one each time
        LineSearchBasedMethodState? minimizationState = NewMinimizationState(P);

        // Loop over iterations
        do
        {
            // Linesearch
            if (!firstTime)
            {
                prevGradient = search.LastGradient.Clone();
            }

            t = search.Evaluate(P, ref ecType, endCriteria, t);
            // don't throw: it can fail just because maxIterations exceeded
            //QL_REQUIRE(lineSearch_->succeed(), "line-search failed!");
            if (search.Succeed)
            {
                // Updates

                // New point
                x = search.LastX.Clone();
                // New function value
                fold = P.FunctionValue!.Value;
                P.FunctionValue = search.LastFunctionValue;
                // New gradient and search direction vectors

                // orthogonalization coef
                gold2 = P.GradientNormValue!.Value;
                P.GradientNormValue = search.LastGradientNorm2;

                // conjugate gradient search direction
                direction = GetUpdatedDirection(P, gold2, prevGradient, search, minimizationState);

                search.SearchDirection = direction;
                // Now compute accuracy and check end criteria
                // Numerical Recipes exit strategy on fx (see NR in C++, p.423)
                fnew = P.FunctionValue.Value;
                fdiff = 2.0 * Math.Abs(fnew - fold) / (Math.Abs(fnew) + Math.Abs(fold) + NumericLimits.QL_EPSILON);
                bool stop = fdiff < ftol;
                if (!stop && endCriteria.CheckMaxIterations(iterationNumber))
                {
                    ecType = EndCriteriaType.MaxIterations;
                    stop = true;
                }

                if (stop)
                {
                    EndCriteriaCheck stationary = endCriteria.CheckStationaryFunctionValue(
                        0.0, 0.0, maxStationaryStateIterations);
                    maxStationaryStateIterations = stationary.StationaryStateIterations;
                    if (stationary.Stop)
                    {
                        ecType = stationary.Type;
                    }

                    if (endCriteria.CheckMaxIterations(iterationNumber))
                    {
                        ecType = EndCriteriaType.MaxIterations;
                    }

                    // QuantLib returns here before setting the current value, which stays the point before.
                    P.CurrentValue = x;
                    return ecType;
                }

                P.CurrentValue = x;      // update problem current value
                ++iterationNumber;       // Increase iteration number
                firstTime = false;
            }
            else
            {
                done = true;
            }
        }
        while (!done);
        P.CurrentValue = x;
        return ecType;
    }

    /// <summary>The new search direction, QuantLib's <c>getUpdatedDirection</c>.</summary>
    /// <param name="P">The optimization problem's state, its gradient norm the new point's.</param>
    /// <param name="gold2">The gradient's squared norm at the previous point.</param>
    /// <param name="gradient">The gradient at the previous point.</param>
    /// <param name="lineSearch">
    /// The minimization's line search, after its search: its last gradient is the new point's, its search direction
    /// the previous one. QuantLib's derived methods read it as the base's member.
    /// </param>
    /// <returns>The new search direction.</returns>
    /// <exception cref="NanoQLException">The method overrides neither this member nor the five-argument one.</exception>
    protected virtual Vector GetUpdatedDirection(ProblemState P, double gold2, Vector gradient, LineSearch lineSearch) =>
        throw new NanoQLException("a line-search method overrides one of the GetUpdatedDirection members");

    /// <summary>
    /// The new search direction, from the method's state for the minimization; by default the four-argument member's.
    /// </summary>
    /// <param name="P">The optimization problem's state, its gradient norm the new point's.</param>
    /// <param name="gold2">The gradient's squared norm at the previous point.</param>
    /// <param name="gradient">The gradient at the previous point.</param>
    /// <param name="lineSearch">The minimization's line search, after its search.</param>
    /// <param name="minimizationState">The state <see cref="NewMinimizationState"/> returned at the minimization's
    /// start.</param>
    /// <returns>The new search direction.</returns>
    protected virtual Vector GetUpdatedDirection(
        ProblemState P, double gold2, Vector gradient, LineSearch lineSearch, LineSearchBasedMethodState? minimizationState) =>
        GetUpdatedDirection(P, gold2, gradient, lineSearch);

    /// <summary>The method's state for a minimization, created at its start; none by default.</summary>
    /// <param name="state">The optimization problem's state at the minimization's start.</param>
    /// <returns>The state, or <see langword="null"/> for none.</returns>
    protected virtual LineSearchBasedMethodState? NewMinimizationState(ProblemState state) => null;
}
