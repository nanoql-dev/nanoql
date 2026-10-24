// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solvers1d/ridder.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Ridder's 1-D solver: the bracket's midpoint and an exponential fit through it.</summary>
/// <remarks>
/// The correctness of the returned values is tested by checking them against known good results. The solver works to
/// a hundredth of the accuracy asked, since tests on Black-Scholes implied volatilities show it otherwise delivers 100
/// times less than promised. Where <c>√(f(mid)² − f(xMin)·f(xMax))</c> is close to 0 QuantLib returns its previous
/// root, unset on the first iteration; <see cref="Solver1D{TSelf, TFunction}"/> returns first wherever |f| at a bracket
/// end is below its accuracy, so that branch isn't reached.
/// </remarks>
public sealed class Ridder : Solver1D<Ridder, ISolver1DFunction>
{
    /// <summary>
    /// The zero of <paramref name="f"/>, bracketed from an initial guess: <see cref="Solver1D{TSelf, TFunction}.Solve{TF}(TF, double, double, double)"/>
    /// over a delegate, as QuantLib-Python's <c>solve</c> takes a Python function.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="accuracy">The accuracy, positive.</param>
    /// <param name="guess">The initial guess.</param>
    /// <param name="step">The first step of the bracketing.</param>
    /// <returns>The zero.</returns>
    /// <exception cref="ArgumentException">The accuracy isn't positive.</exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    public double Solve(Func<double, double> f, double accuracy, double guess, double step)
    {
        ArgumentNullException.ThrowIfNull(f);
        return Solve(new Function(f), accuracy, guess, step);
    }

    /// <summary>
    /// The zero of <paramref name="f"/> within a bracket:
    /// <see cref="Solver1D{TSelf, TFunction}.Solve{TF}(TF, double, double, double, double)"/> over a delegate, as
    /// QuantLib-Python's <c>solve</c> takes a Python function.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="accuracy">The accuracy, positive.</param>
    /// <param name="guess">The initial guess, strictly inside the bracket.</param>
    /// <param name="xMin">The bracket's lower end.</param>
    /// <param name="xMax">The bracket's upper end.</param>
    /// <returns>The zero.</returns>
    /// <exception cref="ArgumentException">
    /// The accuracy isn't positive, the range is empty or outside the enforced bounds, or the guess is outside it.
    /// </exception>
    /// <exception cref="NanoQLException">The range doesn't bracket a zero, or the solver exceeds its function evaluations.</exception>
    public double Solve(Func<double, double> f, double accuracy, double guess, double xMin, double xMax)
    {
        ArgumentNullException.ThrowIfNull(f);
        return Solve(new Function(f), accuracy, guess, xMin, xMax);
    }

    /// <inheritdoc/>
    protected override double SolveCore<TF>(TF f, double xAcc, Solver1DBracket bracket)
    {
        /* The implementation of the algorithm was inspired by
           Press, Teukolsky, Vetterling, and Flannery,
           "Numerical Recipes in C", 2nd edition,
           Cambridge University Press
        */

        double xMin = bracket.XMin, xMax = bracket.XMax;
        double fxMin = bracket.FxMin, fxMax = bracket.FxMax;
        int evaluationNumber = bracket.EvaluationNumber;

        double fxMid, froot, s, xMid, nextRoot;

        // test on Black-Scholes implied volatility show that
        // Ridder solver algorithm actually provides an
        // accuracy 100 times below promised
        double xAccuracy = xAcc / 100.0;

        // Any highly unlikely value, to simplify logic below
        double root = NumericLimits.QL_MIN_REAL;

        while (evaluationNumber <= MaxEvaluations)
        {
            xMid = 0.5 * (xMin + xMax);

            // First of two function evaluations per iteraton
            fxMid = f.Evaluate(xMid);
            ++evaluationNumber;
            s = Math.Sqrt((fxMid * fxMid) - (fxMin * fxMax));
            if (ComparisonFunctions.Close(s, 0.0))
            {
                f.Evaluate(root);
                ++evaluationNumber;
                return root;
            }

            // Updating formula
            nextRoot = xMid + ((xMid - xMin) * ((fxMin >= fxMax ? 1.0 : -1.0) * fxMid / s));
            if (Math.Abs(nextRoot - root) <= xAccuracy)
            {
                f.Evaluate(root);
                ++evaluationNumber;
                return root;
            }

            root = nextRoot;

            // Second of two function evaluations per iteration
            froot = f.Evaluate(root);
            ++evaluationNumber;
            if (ComparisonFunctions.Close(froot, 0.0))
            {
                return root;
            }

            // Bookkeeping to keep the root bracketed on next iteration
            if (Sign(fxMid, froot) != fxMid)
            {
                xMin = xMid;
                fxMin = fxMid;
                xMax = root;
                fxMax = froot;
            }
            else if (Sign(fxMin, froot) != fxMin)
            {
                xMax = root;
                fxMax = froot;
            }
            else if (Sign(fxMax, froot) != fxMax)
            {
                xMin = root;
                fxMin = froot;
            }
            else
            {
                throw new NanoQLException("never get here.");
            }

            if (Math.Abs(xMax - xMin) <= xAccuracy)
            {
                f.Evaluate(root);
                ++evaluationNumber;
                return root;
            }
        }

        throw new NanoQLException($"maximum number of function evaluations ({MaxEvaluations}) exceeded");
    }

    private static double Sign(double a, double b) => b >= 0.0 ? Math.Abs(a) : -Math.Abs(a);

    // A delegate as a solver function.
    private readonly struct Function(Func<double, double> f) : ISolver1DFunction
    {
        public double Evaluate(double x) => f(x);
    }
}
