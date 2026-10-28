// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solvers1d/secant.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The secant one-dimensional solver.</summary>
public sealed class Secant : Solver1D<Secant, ISolver1DFunction>
{
    /// <summary>Solves f(x) = 0 near <paramref name="guess"/>, bracketing the root by steps.</summary>
    /// <param name="f">The function.</param>
    /// <param name="accuracy">The accuracy in x, positive.</param>
    /// <param name="guess">The first guess.</param>
    /// <param name="step">The bracketing step.</param>
    /// <returns>The root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is null.</exception>
    /// <exception cref="ArgumentException">The accuracy isn't positive.</exception>
    /// <exception cref="NanoQLException">No bracket is found, or the evaluations run out.</exception>
    public double Solve(Func<double, double> f, double accuracy, double guess, double step)
    {
        ArgumentNullException.ThrowIfNull(f);
        return Solve(new Function(f), accuracy, guess, step);
    }

    /// <summary>Solves f(x) = 0 in [<paramref name="xMin"/>, <paramref name="xMax"/>].</summary>
    /// <param name="f">The function.</param>
    /// <param name="accuracy">The accuracy in x, positive.</param>
    /// <param name="guess">The first guess, inside the bracket.</param>
    /// <param name="xMin">The bracket's low end.</param>
    /// <param name="xMax">The bracket's high end.</param>
    /// <returns>The root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is null.</exception>
    /// <exception cref="ArgumentException">The accuracy, bracket or guess is invalid.</exception>
    /// <exception cref="NanoQLException">The root isn't bracketed, or the evaluations run out.</exception>
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
           "Numerical Recipes in C", 2nd edition,
           Cambridge University Press
        */

        double fl, froot, dx, xl, root;
        int evaluationNumber = bracket.EvaluationNumber;

        // Pick the bound with the smaller function value
        // as the most recent guess
        if (Math.Abs(bracket.FxMin) < Math.Abs(bracket.FxMax))
        {
            root = bracket.XMin;
            froot = bracket.FxMin;
            xl = bracket.XMax;
            fl = bracket.FxMax;
        }
        else
        {
            root = bracket.XMax;
            froot = bracket.FxMax;
            xl = bracket.XMin;
            fl = bracket.FxMin;
        }

        while (evaluationNumber <= MaxEvaluations)
        {
            dx = (xl - root) * froot / (froot - fl);
            xl = root;
            fl = froot;
            root += dx;
            froot = f.Evaluate(root);
            ++evaluationNumber;
            if (Math.Abs(dx) < xAccuracy || ComparisonFunctions.Close(froot, 0.0))
            {
                return root;
            }
        }

        throw new NanoQLException($"maximum number of function evaluations ({MaxEvaluations}) exceeded");
    }

    // A delegate as a solver function.
    private readonly struct Function(Func<double, double> f) : ISolver1DFunction
    {
        public double Evaluate(double x) => f(x);
    }
}
