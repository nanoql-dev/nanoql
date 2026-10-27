// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/leastsquare.hpp ql/math/optimization/leastsquare.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Non-linear least squares: min r(x) over x ∈ Rⁿ, r(x) = |f(x)|² for f(x) = b − φ(x), by an optimization method
/// (by default the conjugate gradient), under a constraint.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, with QuantLib's settings (the S-bar plan's BR3 (v)): <see cref="Perform"/> takes the initial value and
/// returns the results, where QuantLib's object holds both. QuantLib's <c>iterationsNumber()</c> isn't ported: it
/// returns a member QuantLib never sets (<c>leastsquare.cpp:109</c>, its assignment commented out), its value undefined
/// (BR5 (xxxvii)). The results are the point the method returns: on the conjugate gradient, the last one its line
/// search accepted (the S-sr plan's SSR6 (xvii), <c>Divergences/LineSearchReturnTests.cs</c>), where QuantLib's is the
/// point before; the residual norm, the function value there, is QuantLib's.
/// </para>
/// </remarks>
public sealed class NonLinearLeastSquare
{
    private readonly Constraint c;
    private readonly double accuracy;
    private readonly int maxIterations;
    private readonly OptimizationMethod om;

    /// <summary>Initializes a new instance of the <see cref="NonLinearLeastSquare"/> class on the conjugate gradient.</summary>
    /// <param name="c">The constraint.</param>
    /// <param name="accuracy">The accuracy of the end criteria.</param>
    /// <param name="maxiter">The most iterations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="c"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxiter"/> is negative.</exception>
    public NonLinearLeastSquare(Constraint c, double accuracy = 1e-4, int maxiter = 100)
        : this(c, accuracy, maxiter, new ConjugateGradient())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NonLinearLeastSquare"/> class.</summary>
    /// <param name="c">The constraint.</param>
    /// <param name="accuracy">The accuracy of the end criteria.</param>
    /// <param name="maxiter">The most iterations.</param>
    /// <param name="om">The optimization method.</param>
    /// <exception cref="ArgumentNullException"><paramref name="c"/> or <paramref name="om"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxiter"/> is negative.</exception>
    public NonLinearLeastSquare(Constraint c, double accuracy, int maxiter, OptimizationMethod om)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(om);
        ArgumentOutOfRangeException.ThrowIfNegative(maxiter);
        this.c = c;
        this.accuracy = accuracy;
        maxIterations = maxiter;
        this.om = om;
    }

    /// <summary>Fits the problem from an initial value.</summary>
    /// <param name="lsProblem">The problem.</param>
    /// <param name="initialValue">The initial value.</param>
    /// <returns>The point found, the residual norm, the last value and the end criterion met.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lsProblem"/> is <see langword="null"/>.</exception>
    public NonLinearLeastSquareResults Perform(LeastSquareProblem lsProblem, ReadOnlySpan<double> initialValue)
    {
        ArgumentNullException.ThrowIfNull(lsProblem);
        double eps = accuracy;

        // wrap the least square problem in an optimization function
        var lsf = new LeastSquareFunction(lsProblem);

        // define optimization problem
        var p = new Problem(lsf, c, initialValue);

        // minimize
        var ec = new EndCriteria(maxIterations, Math.Min(maxIterations / 2, 100), eps, eps, eps);
        ProblemResults results = om.Minimize(p, ec);

        // summarize results of minimization
        double functionValue = results.FunctionValue ?? double.NaN;
        return new NonLinearLeastSquareResults(results.CurrentValue, functionValue, functionValue, results.EndCriteriaType);
    }
}
