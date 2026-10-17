// SPDX-FileCopyrightText: Copyright (C) 2006, 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Nicolas Di Césaré
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/optimization/endcriteria.hpp ql/math/optimization/endcriteria.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The criteria that end an optimization: the maximum number of iterations, and the minimum number of iterations
/// around a stationary point; a stationary point of x, the independent variable; a stationary value of y = f(x),
/// the dependent variable; and a stationary gradient.
/// </summary>
/// <remarks>
/// Immutable. Each check returns its outcome, where QuantLib's write the stationary count and the criterion met into
/// <c>Size&amp;</c> and <c>EndCriteria::Type&amp;</c> arguments: a check that returns <see cref="bool"/> holds when its
/// own criterion is met (<see cref="CheckMaxIterations"/>'s is <see cref="EndCriteriaType.MaxIterations"/>), and one
/// that counts stationary iterations returns an <see cref="EndCriteriaCheck"/>.
/// </remarks>
public sealed class EndCriteria
{
    /// <summary>Initializes a new instance of the <see cref="EndCriteria"/> class.</summary>
    /// <param name="maxIterations">The maximum number of iterations.</param>
    /// <param name="maxStationaryStateIterations">
    /// The maximum number of iterations in a stationary state, more than 1 and fewer than
    /// <paramref name="maxIterations"/>; <see langword="null"/> (QuantLib's <c>Null&lt;Size&gt;()</c>) for half of
    /// <paramref name="maxIterations"/>, at most 100.
    /// </param>
    /// <param name="rootEpsilon">The epsilon on the independent variable's change.</param>
    /// <param name="functionEpsilon">The epsilon on the function value and its change.</param>
    /// <param name="gradientNormEpsilon">
    /// The epsilon on the gradient's norm; <see langword="null"/> (QuantLib's <c>Null&lt;Real&gt;()</c>) for
    /// <paramref name="functionEpsilon"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The maximum number of stationary iterations isn't more than 1, or isn't fewer than the maximum number of
    /// iterations.
    /// </exception>
    public EndCriteria(
        int maxIterations,
        int? maxStationaryStateIterations,
        double rootEpsilon,
        double functionEpsilon,
        double? gradientNormEpsilon)
    {
        MaxIterations = maxIterations;
        RootEpsilon = rootEpsilon;
        FunctionEpsilon = functionEpsilon;

        int stationary = maxStationaryStateIterations ?? Math.Min(maxIterations / 2, 100);
        if (!(stationary > 1))
        {
            throw new ArgumentException(
                $"maxStationaryStateIterations_ ({stationary}) must be greater than one",
                nameof(maxStationaryStateIterations));
        }

        if (!(stationary < maxIterations))
        {
            throw new ArgumentException(
                $"maxStationaryStateIterations_ ({stationary}) must be less than maxIterations_ ({maxIterations})",
                nameof(maxStationaryStateIterations));
        }

        MaxStationaryStateIterations = stationary;
        GradientNormEpsilon = gradientNormEpsilon ?? functionEpsilon;
    }

    /// <summary>Gets the maximum number of iterations.</summary>
    public int MaxIterations { get; }

    /// <summary>Gets the maximum number of iterations in a stationary state.</summary>
    public int MaxStationaryStateIterations { get; }

    /// <summary>Gets the epsilon on the independent variable's change.</summary>
    public double RootEpsilon { get; }

    /// <summary>Gets the epsilon on the function value and its change.</summary>
    public double FunctionEpsilon { get; }

    /// <summary>Gets the epsilon on the gradient's norm.</summary>
    public double GradientNormEpsilon { get; }

    /// <summary>Whether an optimization that ended for a given reason succeeded: at a stationary point, value or accuracy.</summary>
    /// <param name="ecType">The reason.</param>
    /// <returns>
    /// <see langword="true"/> for <see cref="EndCriteriaType.StationaryPoint"/>,
    /// <see cref="EndCriteriaType.StationaryFunctionValue"/> and <see cref="EndCriteriaType.StationaryFunctionAccuracy"/>.
    /// </returns>
    public static bool Succeeded(EndCriteriaType ecType) =>
        ecType == EndCriteriaType.StationaryPoint
        || ecType == EndCriteriaType.StationaryFunctionValue
        || ecType == EndCriteriaType.StationaryFunctionAccuracy;

    /// <summary>
    /// Whether the number of iterations has reached the maximum, so <see cref="EndCriteriaType.MaxIterations"/> holds.
    /// </summary>
    /// <param name="iteration">The iteration.</param>
    /// <returns><see langword="true"/> when the optimization stops.</returns>
    public bool CheckMaxIterations(int iteration)
    {
        if (iteration < MaxIterations)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the root has been stationary for more than the maximum number of stationary iterations: a change below
    /// the root epsilon adds a stationary iteration, and any other change resets the count to 0.
    /// </summary>
    /// <param name="xOld">The previous root.</param>
    /// <param name="xNew">The new root.</param>
    /// <param name="stationaryStateIterations">The count of stationary iterations so far.</param>
    /// <returns>
    /// The outcome: stopping with <see cref="EndCriteriaType.StationaryPoint"/>, and the count after the check.
    /// </returns>
    public EndCriteriaCheck CheckStationaryPoint(double xOld, double xNew, int stationaryStateIterations)
    {
        if (Math.Abs(xNew - xOld) >= RootEpsilon)
        {
            return new EndCriteriaCheck(false, EndCriteriaType.None, 0);
        }

        ++stationaryStateIterations;
        if (stationaryStateIterations <= MaxStationaryStateIterations)
        {
            return new EndCriteriaCheck(false, EndCriteriaType.None, stationaryStateIterations);
        }

        return new EndCriteriaCheck(true, EndCriteriaType.StationaryPoint, stationaryStateIterations);
    }

    /// <summary>
    /// Whether the function value has been stationary for more than the maximum number of stationary iterations: a
    /// change below the function epsilon adds a stationary iteration, and any other change resets the count to 0.
    /// </summary>
    /// <param name="fxOld">The previous function value.</param>
    /// <param name="fxNew">The new function value.</param>
    /// <param name="stationaryStateIterations">The count of stationary iterations so far.</param>
    /// <returns>
    /// The outcome: stopping with <see cref="EndCriteriaType.StationaryFunctionValue"/>, and the count after the check.
    /// </returns>
    public EndCriteriaCheck CheckStationaryFunctionValue(double fxOld, double fxNew, int stationaryStateIterations)
    {
        if (Math.Abs(fxNew - fxOld) >= FunctionEpsilon)
        {
            return new EndCriteriaCheck(false, EndCriteriaType.None, 0);
        }

        ++stationaryStateIterations;
        if (stationaryStateIterations <= MaxStationaryStateIterations)
        {
            return new EndCriteriaCheck(false, EndCriteriaType.None, stationaryStateIterations);
        }

        return new EndCriteriaCheck(true, EndCriteriaType.StationaryFunctionValue, stationaryStateIterations);
    }

    /// <summary>
    /// Whether, in a positive optimization, the function value is below the function epsilon, so
    /// <see cref="EndCriteriaType.StationaryFunctionAccuracy"/> holds.
    /// </summary>
    /// <param name="f">The function value.</param>
    /// <param name="positiveOptimization">Whether the function is known to be positive.</param>
    /// <returns><see langword="true"/> when the optimization stops.</returns>
    public bool CheckStationaryFunctionAccuracy(double f, bool positiveOptimization)
    {
        if (!positiveOptimization)
        {
            return false;
        }

        if (f >= FunctionEpsilon)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the gradient's norm is below the gradient-norm epsilon, so <see cref="EndCriteriaType.ZeroGradientNorm"/>
    /// holds.
    /// </summary>
    /// <param name="gradientNorm">The gradient's norm.</param>
    /// <returns><see langword="true"/> when the optimization stops.</returns>
    public bool CheckZeroGradientNorm(double gradientNorm)
    {
        if (gradientNorm >= GradientNormEpsilon)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the optimization stops, QuantLib's <c>operator()</c>: the checks in turn, the first met deciding, as
    /// <see cref="CheckMaxIterations"/>, then <see cref="CheckStationaryFunctionValue"/>, then
    /// <see cref="CheckStationaryFunctionAccuracy"/> on the new value, then <see cref="CheckZeroGradientNorm"/> on the
    /// new gradient's norm.
    /// </summary>
    /// <param name="iteration">The iteration.</param>
    /// <param name="stationaryStateIterations">The count of stationary iterations so far.</param>
    /// <param name="positiveOptimization">Whether the function is known to be positive.</param>
    /// <param name="fOld">The previous function value.</param>
    /// <param name="normGOld">The previous gradient's norm, which no check reads.</param>
    /// <param name="fNew">The new function value.</param>
    /// <param name="normGNew">The new gradient's norm.</param>
    /// <returns>
    /// The outcome: whether to stop and why, and the stationary count, which only
    /// <see cref="CheckStationaryFunctionValue"/> changes.
    /// </returns>
    public EndCriteriaCheck Evaluate(
        int iteration,
        int stationaryStateIterations,
        bool positiveOptimization,
        double fOld,
        double normGOld,
        double fNew,
        double normGNew)
    {
        _ = normGOld;
        if (CheckMaxIterations(iteration))
        {
            return new EndCriteriaCheck(true, EndCriteriaType.MaxIterations, stationaryStateIterations);
        }

        EndCriteriaCheck stationary = CheckStationaryFunctionValue(fOld, fNew, stationaryStateIterations);
        if (stationary.Stop)
        {
            return stationary;
        }

        stationaryStateIterations = stationary.StationaryStateIterations;
        if (CheckStationaryFunctionAccuracy(fNew, positiveOptimization))
        {
            return new EndCriteriaCheck(true, EndCriteriaType.StationaryFunctionAccuracy, stationaryStateIterations);
        }

        if (CheckZeroGradientNorm(normGNew))
        {
            return new EndCriteriaCheck(true, EndCriteriaType.ZeroGradientNorm, stationaryStateIterations);
        }

        return new EndCriteriaCheck(false, EndCriteriaType.None, stationaryStateIterations);
    }
}
