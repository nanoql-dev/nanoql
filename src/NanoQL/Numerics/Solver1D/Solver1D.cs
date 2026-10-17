// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/solver1d.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The base class of 1-D solvers: QuantLib's curiously recurring <c>Solver1D&lt;Impl&gt;</c>. It brackets the zero
/// of a function, then hands the bracket to the solver's <see cref="SolveCore{TF}"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete solver, which the setters return.</typeparam>
/// <typeparam name="TFunction">
/// The function concept the solver needs: <see cref="ISolver1DFunction"/>, or <see cref="IDerivativeSolver1DFunction"/>
/// for a solver that also calls the derivative.
/// </typeparam>
/// <remarks>
/// <para>
/// A concrete solver is declared as <c>sealed class Foo : Solver1D&lt;Foo, ISolver1DFunction&gt;</c> and implements
/// <see cref="SolveCore{TF}"/>, which can assume what <see cref="Solver1DBracket"/> holds: a valid bracket, the
/// function's values at its ends, and a valid initial guess.
/// </para>
/// <para>
/// Immutable: <see cref="SetMaxEvaluations"/>, <see cref="SetLowerBound"/> and <see cref="SetUpperBound"/> return a
/// modified copy, where QuantLib's setters change the solver. The bracketing's state, which QuantLib keeps in
/// <c>mutable</c> members, lives inside each <c>Solve</c> call.
/// </para>
/// <para>
/// An extension point, as in QuantLib: the other 1-D solvers derive from it.
/// </para>
/// </remarks>
public abstract class Solver1D<TSelf, TFunction>
    where TSelf : Solver1D<TSelf, TFunction>
    where TFunction : ISolver1DFunction
{
    /// <summary>The default maximum number of function evaluations: QuantLib's <c>MAX_FUNCTION_EVALUATIONS</c>.</summary>
    public const int MaxFunctionEvaluations = 100;

    private int maxEvaluations = MaxFunctionEvaluations;
    private double lowerBound, upperBound;
    private bool lowerBoundEnforced, upperBoundEnforced;

    /// <summary>Initializes a new instance of the <see cref="Solver1D{TSelf, TFunction}"/> class.</summary>
    protected Solver1D()
    {
    }

    /// <summary>Gets the maximum number of function evaluations, for the bracketing and the solver alike.</summary>
    protected int MaxEvaluations => maxEvaluations;

    /// <summary>
    /// A copy of the solver with the maximum number of function evaluations set; the bracketing throws if it finds
    /// no bracket within that number.
    /// </summary>
    /// <param name="evaluations">The maximum number of function evaluations.</param>
    /// <returns>The modified copy.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The number is negative.</exception>
    public TSelf SetMaxEvaluations(int evaluations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(evaluations);
        var copy = (TSelf)MemberwiseClone();
        copy.maxEvaluations = evaluations;
        return copy;
    }

    /// <summary>A copy of the solver with the lower bound of the function's domain set.</summary>
    /// <param name="lowerBound">The lower bound.</param>
    /// <returns>The modified copy.</returns>
    public TSelf SetLowerBound(double lowerBound)
    {
        var copy = (TSelf)MemberwiseClone();
        copy.lowerBound = lowerBound;
        copy.lowerBoundEnforced = true;
        return copy;
    }

    /// <summary>A copy of the solver with the upper bound of the function's domain set.</summary>
    /// <param name="upperBound">The upper bound.</param>
    /// <returns>The modified copy.</returns>
    public TSelf SetUpperBound(double upperBound)
    {
        var copy = (TSelf)MemberwiseClone();
        copy.upperBound = upperBound;
        copy.upperBoundEnforced = true;
        return copy;
    }

    /// <summary>
    /// The zero of <paramref name="f"/>, determined with the given accuracy ε: depending on the solver, the returned x
    /// is such that |f(x)| &lt; ε, or |x − ξ| &lt; ε where ξ is the real zero. A bracketing routine first searches for
    /// a bracket, from the initial guess, in steps of <paramref name="step"/> that grow by 1.6 each time.
    /// </summary>
    /// <typeparam name="TF">The function's type.</typeparam>
    /// <param name="f">The function.</param>
    /// <param name="accuracy">The accuracy, positive; one below machine epsilon is taken as machine epsilon.</param>
    /// <param name="guess">The initial guess.</param>
    /// <param name="step">The first step of the bracketing.</param>
    /// <returns>The zero.</returns>
    /// <exception cref="ArgumentException">The accuracy isn't positive.</exception>
    /// <exception cref="NanoQLException">No bracket is found, or the solver exceeds its function evaluations.</exception>
    public double Solve<TF>(TF f, double accuracy, double guess, double step)
        where TF : struct, TFunction
    {
        if (!(accuracy > 0.0))
        {
            throw new ArgumentException($"accuracy ({Show(accuracy)}) must be positive", nameof(accuracy));
        }

        // check whether we really want to use epsilon
        accuracy = accuracy < NumericLimits.QL_EPSILON ? NumericLimits.QL_EPSILON : accuracy;

        const double growthFactor = 1.6;
        int flipflop = -1;

        double root = guess, xMin, xMax, fxMin;
        double fxMax = f.Evaluate(root);

        // monotonically crescent bias, as in optionValue(volatility)
        if (ComparisonFunctions.Close(fxMax, 0.0))
        {
            return root;
        }
        else if (fxMax > 0.0)
        {
            xMin = EnforceBounds(root - step);
            fxMin = f.Evaluate(xMin);
            xMax = root;
        }
        else
        {
            xMin = root;
            fxMin = fxMax;
            xMax = EnforceBounds(root + step);
            fxMax = f.Evaluate(xMax);
        }

        int evaluationNumber = 2;
        while (evaluationNumber <= maxEvaluations)
        {
            if (fxMin * fxMax <= 0.0)
            {
                if (ComparisonFunctions.Close(fxMin, 0.0))
                {
                    return xMin;
                }

                if (ComparisonFunctions.Close(fxMax, 0.0))
                {
                    return xMax;
                }

                root = (xMax + xMin) / 2.0;
                return SolveCore(f, accuracy, new Solver1DBracket(root, xMin, xMax, fxMin, fxMax, evaluationNumber));
            }

            if (Math.Abs(fxMin) < Math.Abs(fxMax))
            {
                xMin = EnforceBounds(xMin + (growthFactor * (xMin - xMax)));
                fxMin = f.Evaluate(xMin);
            }
            else if (Math.Abs(fxMin) > Math.Abs(fxMax))
            {
                xMax = EnforceBounds(xMax + (growthFactor * (xMax - xMin)));
                fxMax = f.Evaluate(xMax);
            }
            else if (flipflop == -1)
            {
                xMin = EnforceBounds(xMin + (growthFactor * (xMin - xMax)));
                fxMin = f.Evaluate(xMin);
                evaluationNumber++;
                flipflop = 1;
            }
            else if (flipflop == 1)
            {
                xMax = EnforceBounds(xMax + (growthFactor * (xMax - xMin)));
                fxMax = f.Evaluate(xMax);
                flipflop = -1;
            }

            evaluationNumber++;
        }

        throw new NanoQLException(
            $"unable to bracket root in {maxEvaluations} function evaluations (last bracket attempt: "
            + $"f[{Show(xMin)},{Show(xMax)}] -> [{Show(fxMin)},{Show(fxMax)}])");
    }

    /// <summary>
    /// The zero of <paramref name="f"/>, determined with the given accuracy ε, within a bracket: either
    /// f(<paramref name="xMin"/>) ≤ 0 ≤ f(<paramref name="xMax"/>), or f(<paramref name="xMax"/>) ≤ 0 ≤
    /// f(<paramref name="xMin"/>). Depending on the solver, the returned x is such that |f(x)| &lt; ε, or |x − ξ| &lt;
    /// ε where ξ is the real zero.
    /// </summary>
    /// <typeparam name="TF">The function's type.</typeparam>
    /// <param name="f">The function.</param>
    /// <param name="accuracy">The accuracy, positive; one below machine epsilon is taken as machine epsilon.</param>
    /// <param name="guess">The initial guess, strictly inside the bracket.</param>
    /// <param name="xMin">The bracket's lower end, not below an enforced lower bound.</param>
    /// <param name="xMax">The bracket's upper end, not above an enforced upper bound.</param>
    /// <returns>The zero.</returns>
    /// <exception cref="ArgumentException">
    /// The accuracy isn't positive, the range is empty or outside the enforced bounds, or the guess is outside it.
    /// </exception>
    /// <exception cref="NanoQLException">The range doesn't bracket a zero, or the solver exceeds its function evaluations.</exception>
    public double Solve<TF>(TF f, double accuracy, double guess, double xMin, double xMax)
        where TF : struct, TFunction
    {
        if (!(accuracy > 0.0))
        {
            throw new ArgumentException($"accuracy ({Show(accuracy)}) must be positive", nameof(accuracy));
        }

        // check whether we really want to use epsilon
        accuracy = accuracy < NumericLimits.QL_EPSILON ? NumericLimits.QL_EPSILON : accuracy;

        if (!(xMin < xMax))
        {
            throw new ArgumentException($"invalid range: xMin_ ({Show(xMin)}) >= xMax_ ({Show(xMax)})", nameof(xMin));
        }

        if (!(!lowerBoundEnforced || xMin >= lowerBound))
        {
            throw new ArgumentException(
                $"xMin_ ({Show(xMin)}) < enforced low bound ({Show(lowerBound)})", nameof(xMin));
        }

        if (!(!upperBoundEnforced || xMax <= upperBound))
        {
            throw new ArgumentException(
                $"xMax_ ({Show(xMax)}) > enforced hi bound ({Show(upperBound)})", nameof(xMax));
        }

        double fxMin = f.Evaluate(xMin);
        if (ComparisonFunctions.Close(fxMin, 0.0))
        {
            return xMin;
        }

        double fxMax = f.Evaluate(xMax);
        if (ComparisonFunctions.Close(fxMax, 0.0))
        {
            return xMax;
        }

        const int evaluationNumber = 2;

        if (!(fxMin * fxMax < 0.0))
        {
            throw new NanoQLException(
                $"root not bracketed: f[{Show(xMin)},{Show(xMax)}] -> [{Show(fxMin)},{Show(fxMax)}]");
        }

        if (!(guess > xMin))
        {
            throw new ArgumentException($"guess ({Show(guess)}) < xMin_ ({Show(xMin)})", nameof(guess));
        }

        if (!(guess < xMax))
        {
            throw new ArgumentException($"guess ({Show(guess)}) > xMax_ ({Show(xMax)})", nameof(guess));
        }

        return SolveCore(f, accuracy, new Solver1DBracket(guess, xMin, xMax, fxMin, fxMax, evaluationNumber));
    }

    /// <summary>
    /// The solver's algorithm, QuantLib's <c>solveImpl</c>: the zero of <paramref name="f"/> within the bracket the
    /// public <c>Solve</c> found or checked.
    /// </summary>
    /// <typeparam name="TF">The function's type.</typeparam>
    /// <param name="f">The function.</param>
    /// <param name="accuracy">The accuracy, at least machine epsilon.</param>
    /// <param name="bracket">The bracket, the function's values at its ends, the initial guess and the evaluations so far.</param>
    /// <returns>The zero.</returns>
    protected abstract double SolveCore<TF>(TF f, double accuracy, Solver1DBracket bracket)
        where TF : struct, TFunction;

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    private double EnforceBounds(double x)
    {
        if (lowerBoundEnforced && x < lowerBound)
        {
            return lowerBound;
        }

        if (upperBoundEnforced && x > upperBound)
        {
            return upperBound;
        }

        return x;
    }
}
