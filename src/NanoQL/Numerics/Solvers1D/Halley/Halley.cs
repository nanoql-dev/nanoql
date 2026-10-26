// SPDX-FileCopyrightText: Copyright (C) 2024 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solvers1d/halley.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>Halley's 1-D solver: Halley's steps from the guess, falling back to <see cref="NewtonSafe"/> when a step
/// leaves the bracket.</summary>
/// <remarks>
/// The solver needs the function's first and second derivatives: its function concept is
/// <see cref="ISecondDerivativeSolver1DFunction"/>. The correctness of the returned values is tested by checking them
/// against known good results. The last function evaluation is at the returned root.
/// </remarks>
public sealed class Halley : Solver1D<Halley, ISecondDerivativeSolver1DFunction>
{
    /// <summary>
    /// The zero of <paramref name="f"/>, bracketed from an initial guess:
    /// <see cref="Solver1D{TSelf, TFunction}.Solve{TF}(TF, double, double, double)"/> over delegates.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="derivative">Its first derivative.</param>
    /// <param name="secondDerivative">Its second derivative.</param>
    /// <param name="accuracy">The accuracy.</param>
    /// <param name="guess">The initial guess.</param>
    /// <param name="step">The initial step of the bracketing.</param>
    /// <returns>The zero.</returns>
    /// <exception cref="ArgumentNullException">A delegate is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The accuracy isn't positive.</exception>
    /// <exception cref="NanoQLException">The zero can't be bracketed, or the evaluations run out.</exception>
    public double Solve(
        Func<double, double> f,
        Func<double, double> derivative,
        Func<double, double> secondDerivative,
        double accuracy,
        double guess,
        double step)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(derivative);
        ArgumentNullException.ThrowIfNull(secondDerivative);
        return Solve(new Function(f, derivative, secondDerivative), accuracy, guess, step);
    }

    /// <summary>
    /// The zero of <paramref name="f"/> in a bracket:
    /// <see cref="Solver1D{TSelf, TFunction}.Solve{TF}(TF, double, double, double, double)"/> over delegates.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="derivative">Its first derivative.</param>
    /// <param name="secondDerivative">Its second derivative.</param>
    /// <param name="accuracy">The accuracy.</param>
    /// <param name="guess">The initial guess.</param>
    /// <param name="xMin">The bracket's lower end.</param>
    /// <param name="xMax">The bracket's upper end.</param>
    /// <returns>The zero.</returns>
    /// <exception cref="ArgumentNullException">A delegate is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The accuracy isn't positive, or the bracket is empty.</exception>
    /// <exception cref="NanoQLException">The bracket holds no zero, or the evaluations run out.</exception>
    public double Solve(
        Func<double, double> f,
        Func<double, double> derivative,
        Func<double, double> secondDerivative,
        double accuracy,
        double guess,
        double xMin,
        double xMax)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentNullException.ThrowIfNull(derivative);
        ArgumentNullException.ThrowIfNull(secondDerivative);
        return Solve(new Function(f, derivative, secondDerivative), accuracy, guess, xMin, xMax);
    }

    /// <inheritdoc/>
    protected override double SolveCore<TF>(TF f, double xAccuracy, Solver1DBracket bracket)
    {
        double root = bracket.Root, xMin = bracket.XMin, xMax = bracket.XMax;
        int evaluationNumber = bracket.EvaluationNumber;

        while (++evaluationNumber <= MaxEvaluations)
        {
            double fx = f.Evaluate(root);
            double fPrime = f.Derivative(root);
            double lf = fx * f.SecondDerivative(root) / (fPrime * fPrime);
            double step = 1.0 / (1.0 - (0.5 * lf)) * fx / fPrime;
            root -= step;

            // jumped out of brackets, switch to NewtonSafe
            if ((xMin - root) * (root - xMax) < 0.0)
            {
                NewtonSafe s = new NewtonSafe().SetMaxEvaluations(MaxEvaluations - evaluationNumber);
                return s.Solve(f, xAccuracy, root + step, xMin, xMax);
            }

            if (Math.Abs(step) < xAccuracy)
            {
                f.Evaluate(root);
                ++evaluationNumber;
                return root;
            }
        }

        throw new NanoQLException($"maximum number of function evaluations ({MaxEvaluations}) exceeded");
    }

    // Delegates as a solver function with its two derivatives.
    private readonly struct Function(
        Func<double, double> f, Func<double, double> derivative, Func<double, double> secondDerivative)
        : ISecondDerivativeSolver1DFunction
    {
        public double Evaluate(double x) => f(x);

        public double Derivative(double x) => derivative(x);

        public double SecondDerivative(double x) => secondDerivative(x);
    }
}
