// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solvers1d/finitedifferencenewtonsafe.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The safe (bracketed) Newton 1-D solver with finite-difference derivatives: Newton's steps on the slope between the
/// last two evaluations, falling back to bisection.
/// </summary>
/// <remarks>
/// The correctness of the returned values is tested by checking them against known good results. Unlike
/// <see cref="Brent"/>'s, the last function evaluation isn't at the returned root: the solver returns its Newton or
/// bisection step once the step is below the accuracy, without evaluating there, as QuantLib's does. The bootstraps
/// rely on it: their function writes each evaluated value into the curve, which keeps the last one.
/// </remarks>
public sealed class FiniteDifferenceNewtonSafe : Solver1D<FiniteDifferenceNewtonSafe, ISolver1DFunction>
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
        double root = bracket.Root, xMin = bracket.XMin, xMax = bracket.XMax;
        double fxMin = bracket.FxMin, fxMax = bracket.FxMax;
        int evaluationNumber = bracket.EvaluationNumber;

        // Orient the search so that f(xl) < 0
        double xh, xl;
        if (fxMin < 0.0)
        {
            xl = xMin;
            xh = xMax;
        }
        else
        {
            xh = xMin;
            xl = xMax;
        }

        double froot = f.Evaluate(root);
        ++evaluationNumber;

        // first order finite difference derivative
        double dfroot = xMax - root < root - xMin
            ? (fxMax - froot) / (xMax - root)
            : (fxMin - froot) / (xMin - root);

        // xMax_-xMin_>0 is verified in the constructor
        double dx = xMax - xMin;
        while (evaluationNumber <= MaxEvaluations)
        {
            double frootold = froot;
            double rootold = root;
            double dxold = dx;

            // Bisect if (out of range || not decreasing fast enough)
            if (((((root - xh) * dfroot) - froot) * (((root - xl) * dfroot) - froot) > 0.0)
                || (Math.Abs(2.0 * froot) > Math.Abs(dxold * dfroot)))
            {
                dx = (xh - xl) / 2.0;
                root = xl + dx;

                // if the root estimate just computed is close to the
                // previous one, we should calculate dfroot at root and
                // xh rather than root and rootold (xl instead of xh would
                // be just as good)
                if (ComparisonFunctions.Close(root, rootold, 2500))
                {
                    rootold = xh;
                    frootold = f.Evaluate(xh);
                }
            }
            else
            {
                // Newton
                dx = froot / dfroot;
                root -= dx;
            }

            // Convergence criterion
            if (Math.Abs(dx) < xAccuracy)
            {
                return root;
            }

            froot = f.Evaluate(root);
            ++evaluationNumber;
            dfroot = (frootold - froot) / (rootold - root);

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

    private readonly struct Function(Func<double, double> f) : ISolver1DFunction
    {
        public double Evaluate(double x) => f(x);
    }
}
