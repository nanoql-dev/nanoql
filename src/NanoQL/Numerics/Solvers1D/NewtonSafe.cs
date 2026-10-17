// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solvers1d/newtonsafe.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The safe (bracketed) Newton 1-D solver: Newton's steps, falling back to bisection.</summary>
/// <remarks>
/// <para>
/// The solver needs the function's derivative: its function concept is <see cref="IDerivativeSolver1DFunction"/>.
/// QuantLib also refuses a derivative equal to its <c>Null&lt;Real&gt;()</c> sentinel ("NewtonSafe requires
/// function's derivative"); nanoql has no such sentinel, and a function without a derivative doesn't meet the
/// concept, so the check has no counterpart.
/// </para>
/// <para>
/// The correctness of the returned values is tested by checking them against known good results. The last function
/// evaluation is at the returned root.
/// </para>
/// </remarks>
public sealed class NewtonSafe : Solver1D<NewtonSafe, IDerivativeSolver1DFunction>
{
    /// <summary>
    /// The zero of <paramref name="f"/>, bracketed from an initial guess: <see cref="Solver1D{TSelf, TFunction}.Solve{TF}(TF, double, double, double)"/>
    /// over delegates, as QuantLib-Python's <c>solve</c> takes a Python function and calls its <c>derivative</c>.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="derivative">The function's first derivative.</param>
    /// <param name="accuracy">The accuracy, positive.</param>
    /// <param name="guess">The initial guess.</param>
    /// <param name="step">The first step of the bracketing.</param>
    /// <returns>The zero.</returns>
    /// <exception cref="ArgumentException">The accuracy isn't positive.</exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    public double Solve(Func<double, double> f, Func<double, double> derivative, double accuracy, double guess, double step)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(derivative);
        return Solve(new Function(f, derivative), accuracy, guess, step);
    }

    /// <summary>
    /// The zero of <paramref name="f"/> within a bracket:
    /// <see cref="Solver1D{TSelf, TFunction}.Solve{TF}(TF, double, double, double, double)"/> over delegates, as
    /// QuantLib-Python's <c>solve</c> takes a Python function and calls its <c>derivative</c>.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="derivative">The function's first derivative.</param>
    /// <param name="accuracy">The accuracy, positive.</param>
    /// <param name="guess">The initial guess, strictly inside the bracket.</param>
    /// <param name="xMin">The bracket's lower end.</param>
    /// <param name="xMax">The bracket's upper end.</param>
    /// <returns>The zero.</returns>
    /// <exception cref="ArgumentException">
    /// The accuracy isn't positive, the range is empty or outside the enforced bounds, or the guess is outside it.
    /// </exception>
    /// <exception cref="NanoQLException">The range doesn't bracket a zero, or the solver exceeds its function evaluations.</exception>
    public double Solve(
        Func<double, double> f, Func<double, double> derivative, double accuracy, double guess, double xMin, double xMax)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(derivative);
        return Solve(new Function(f, derivative), accuracy, guess, xMin, xMax);
    }

    /// <inheritdoc/>
    protected override double SolveCore<TF>(TF f, double xAccuracy, Solver1DBracket bracket)
    {
        /* The implementation of the algorithm was inspired by
           Press, Teukolsky, Vetterling, and Flannery,
           "Numerical Recipes in C", 2nd edition,
           Cambridge University Press
        */

        double root = bracket.Root, xMin = bracket.XMin, xMax = bracket.XMax;
        int evaluationNumber = bracket.EvaluationNumber;

        double froot, dfroot, dx, dxold;
        double xh, xl;

        // Orient the search so that f(xl) < 0
        if (bracket.FxMin < 0.0)
        {
            xl = xMin;
            xh = xMax;
        }
        else
        {
            xh = xMin;
            xl = xMax;
        }

        // the "stepsize before last"
        dxold = xMax - xMin;
        // it was dxold=std::fabs(xMax_-xMin_); in Numerical Recipes
        // here (xMax_-xMin_ > 0) is verified in the constructor

        // and the last step
        dx = dxold;

        froot = f.Evaluate(root);
        dfroot = f.Derivative(root);
        ++evaluationNumber;

        while (evaluationNumber <= MaxEvaluations)
        {
            // Bisect if (out of range || not decreasing fast enough)
            if (((((root - xh) * dfroot) - froot) * (((root - xl) * dfroot) - froot) > 0.0)
                || (Math.Abs(2.0 * froot) > Math.Abs(dxold * dfroot)))
            {
                dxold = dx;
                dx = (xh - xl) / 2.0;
                root = xl + dx;
            }
            else
            {
                dxold = dx;
                dx = froot / dfroot;
                root -= dx;
            }

            // Convergence criterion
            if (Math.Abs(dx) < xAccuracy)
            {
                f.Evaluate(root);
                ++evaluationNumber;
                return root;
            }

            froot = f.Evaluate(root);
            dfroot = f.Derivative(root);
            ++evaluationNumber;
            if (froot < 0.0)
            {
                xl = root;
            }
            else
            {
                xh = root;
            }
        }

        throw new NanoQLException($"maximum number of function evaluations ({MaxEvaluations}) exceeded");
    }

    // Delegates as a solver function with its derivative.
    private readonly struct Function(Func<double, double> f, Func<double, double> derivative) : IDerivativeSolver1DFunction
    {
        public double Evaluate(double x) => f(x);

        public double Derivative(double x) => derivative(x);
    }
}
