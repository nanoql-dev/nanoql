// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solvers1d/brent.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Brent's 1-D solver: inverse quadratic interpolation, falling back to bisection.</summary>
/// <remarks>
/// The correctness of the returned values is tested by checking them against known good results. The last function
/// evaluation is at the returned root.
/// </remarks>
public sealed class Brent : Solver1D<Brent, ISolver1DFunction>
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
    protected override double SolveCore<TF>(TF f, double xAccuracy, Solver1DBracket bracket)
    {
        /* The implementation of the algorithm was inspired by
           Press, Teukolsky, Vetterling, and Flannery,
           "Numerical Recipes in C", 2nd edition, Cambridge
           University Press
        */

        double root = bracket.Root, xMin = bracket.XMin, xMax = bracket.XMax;
        double fxMin = bracket.FxMin, fxMax = bracket.FxMax;
        int evaluationNumber = bracket.EvaluationNumber;

        double min1, min2;
        double froot, p, q, r, s, xAcc1, xMid;

        // we want to start with root (which equals the guess) on
        // one side of the bracket and both xMin and xMax on the
        // other.
        froot = f.Evaluate(root);
        ++evaluationNumber;
        if (froot * fxMin < 0)
        {
            xMax = xMin;
            fxMax = fxMin;
        }
        else
        {
            xMin = xMax;
            fxMin = fxMax;
        }

        double d = root - xMax;
        double e = d;

        while (evaluationNumber <= MaxEvaluations)
        {
            if ((froot > 0.0 && fxMax > 0.0) || (froot < 0.0 && fxMax < 0.0))
            {
                // Rename xMin, root, xMax and adjust bounds
                xMax = xMin;
                fxMax = fxMin;
                e = d = root - xMin;
            }

            if (Math.Abs(fxMax) < Math.Abs(froot))
            {
                xMin = root;
                root = xMax;
                xMax = xMin;
                fxMin = froot;
                froot = fxMax;
                fxMax = fxMin;
            }

            // Convergence check
            xAcc1 = (2.0 * NumericLimits.QL_EPSILON * Math.Abs(root)) + (0.5 * xAccuracy);
            xMid = (xMax - root) / 2.0;
            if (Math.Abs(xMid) <= xAcc1 || ComparisonFunctions.Close(froot, 0.0))
            {
                f.Evaluate(root);
                ++evaluationNumber;
                return root;
            }

            if (Math.Abs(e) >= xAcc1 && Math.Abs(fxMin) > Math.Abs(froot))
            {
                // Attempt inverse quadratic interpolation
                s = froot / fxMin;
                if (ComparisonFunctions.Close(xMin, xMax))
                {
                    p = 2.0 * xMid * s;
                    q = 1.0 - s;
                }
                else
                {
                    q = fxMin / fxMax;
                    r = froot / fxMax;
                    p = s * ((2.0 * xMid * q * (q - r)) - ((root - xMin) * (r - 1.0)));
                    q = (q - 1.0) * (r - 1.0) * (s - 1.0);
                }

                if (p > 0.0)
                {
                    q = -q;  // Check whether in bounds
                }

                p = Math.Abs(p);
                min1 = (3.0 * xMid * q) - Math.Abs(xAcc1 * q);
                min2 = Math.Abs(e * q);
                if (2.0 * p < (min1 < min2 ? min1 : min2))
                {
                    e = d;                // Accept interpolation
                    d = p / q;
                }
                else
                {
                    d = xMid;  // Interpolation failed, use bisection
                    e = d;
                }
            }
            else
            {
                // Bounds decreasing too slowly, use bisection
                d = xMid;
                e = d;
            }

            xMin = root;
            fxMin = froot;
            if (Math.Abs(d) > xAcc1)
            {
                root += d;
            }
            else
            {
                root += Sign(xAcc1, xMid);
            }

            froot = f.Evaluate(root);
            ++evaluationNumber;
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
