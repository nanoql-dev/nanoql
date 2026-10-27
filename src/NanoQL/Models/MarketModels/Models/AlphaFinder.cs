// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/alphafinder.hpp ql/models/marketmodels/models/alphafinder.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// The α of an alpha form, and the multipliers a and b, at which a swap rate's volatilities give a caplet its target
/// variance: by the form's α at the initial guess where it works, by a bisection on the quadratic's turning point
/// otherwise, or, with <see cref="SolveWithMaxHomogeneity"/>, at the α of the most homogeneous volatilities among those
/// that work.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mm plan's MR2 (b)): each solve keeps its inputs, as QuantLib's members, and sets the form's α;
/// the alpha calibration builds one per calibration around its own copy of the form. It isn't thread-safe.
/// </para>
/// <para>
/// Three departures from QuantLib (<c>Divergences/AlphaFinderTests.cs</c>): the downward bracket search stops at its
/// first acceptable α, testing the value it moves, where QuantLib's tests the upward one's (MR3 (ix);
/// <c>alphafinder.cpp:372-373</c>); the minimization's feasibility test reads the trial α, where QuantLib's reads the
/// objective's value there (MR3 (x); <c>:117</c>, <c>:145</c>); and each bisection and the minimization also stop when
/// their point no longer moves, where QuantLib's never end (MR3 (xii); <c>:30-111</c>). As in QuantLib,
/// <see cref="Solve"/> returns true where its final part fails, at α₀ or at the search's α (<c>:335-346</c>,
/// <c>:405-415</c>), the swap rate's last volatility then left as the caller's; nanoql's b is NaN there, where QuantLib
/// leaves the caller's value, and the alpha calibration counts the step as a failure (MR3 (viii) as the user ruled it
/// on 2026-10-27). Inputs shorter than the step index reads, which QuantLib reads past, are refused (MR3 (xx)).
/// </para>
/// </remarks>
public sealed class AlphaFinder
{
    private readonly AlphaForm parametricForm;
    private readonly bool quantLibsMinimizeTest;
    private int stepIndex;
    private double[] rateOneVols = [];
    private double[] rateTwoHomogeneousVols = [];
    private double[] putativeVols = [];
    private double[] correlations = [];
    private double w0;
    private double w1;
    private double constantPart;
    private double linearPart;
    private double quadraticPart;
    private double totalVar;
    private double targetVariance;

    /// <summary>Initializes a new instance of the <see cref="AlphaFinder"/> class.</summary>
    /// <param name="parametricForm">The alpha form, whose α each solve sets.</param>
    public AlphaFinder(AlphaForm parametricForm)
    {
        ArgumentNullException.ThrowIfNull(parametricForm);
        this.parametricForm = parametricForm;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlphaFinder"/> class whose maximum-homogeneity minimization tests
    /// the objective's value, as QuantLib's does: the alpha calibration's fallback where the fixed test leaves a caplet
    /// no α (MR3 (x) as the user ruled it on 2026-10-27).
    /// </summary>
    /// <param name="parametricForm">The alpha form, whose α each solve sets.</param>
    /// <param name="quantLibsMinimizeTest">Whether the minimization tests the objective's value, as QuantLib's.</param>
    internal AlphaFinder(AlphaForm parametricForm, bool quantLibsMinimizeTest)
        : this(parametricForm)
    {
        this.quantLibsMinimizeTest = quantLibsMinimizeTest;
    }

    /// <summary>
    /// The α, a and b at which the second rate's volatilities give the caplet its target variance: the initial α where
    /// it works, a bracketed bisection on the quadratic's turning point otherwise.
    /// </summary>
    /// <param name="alpha0">The initial α.</param>
    /// <param name="stepIndex">The caplet's index.</param>
    /// <param name="rateOneVols">The first rate's volatilities, at least the step index plus one.</param>
    /// <param name="rateTwoHomogeneousVols">The second rate's homogeneous volatilities, at least the step index plus
    /// two.</param>
    /// <param name="correlations">The two rates' correlations, at least the step index plus one.</param>
    /// <param name="w0">The first rate's weight.</param>
    /// <param name="w1">The second rate's weight.</param>
    /// <param name="targetVariance">The caplet's target variance.</param>
    /// <param name="tolerance">The bisection's tolerance.</param>
    /// <param name="alphaMax">The largest α.</param>
    /// <param name="alphaMin">The smallest α.</param>
    /// <param name="steps">The bracket search's steps.</param>
    /// <param name="alpha">The α found.</param>
    /// <param name="a">The multiplier of the second rate's volatilities to the caplet's reset.</param>
    /// <param name="b">The multiplier of its volatility after it; NaN where the final part fails.</param>
    /// <param name="rateTwoVols">The second rate's new volatilities, written to the step index plus one.</param>
    /// <returns>Whether an α was found.</returns>
    /// <exception cref="ArgumentException">A vector is shorter than the step index reads.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stepIndex"/> is negative.</exception>
    public bool Solve(
        double alpha0,
        int stepIndex,
        ReadOnlySpan<double> rateOneVols,
        ReadOnlySpan<double> rateTwoHomogeneousVols,
        ReadOnlySpan<double> correlations,
        double w0,
        double w1,
        double targetVariance,
        double tolerance,
        double alphaMax,
        double alphaMin,
        int steps,
        out double alpha,
        out double a,
        out double b,
        Span<double> rateTwoVols)
    {
        Setup(stepIndex, rateOneVols, rateTwoHomogeneousVols, correlations, w0, w1, targetVariance, rateTwoVols);
        alpha = a = b = double.NaN;

        // compute linear part with initial alpha
        double valueAtTP = ValueAtTurningPoint(alpha0);

        // a final part that fails leaves b NaN, which the alpha calibration counts as a failure (MR3 (viii))
        if (valueAtTP <= targetVariance)
        {
            FinalPart(alpha0, stepIndex, quadraticPart, linearPart, constantPart, out alpha, out a, out b, rateTwoVols);
            return true;
        }

        // we now have to solve
        double bottomValue = ValueAtTurningPoint(alphaMin);
        double bottomAlpha = alphaMin;
        double topValue = ValueAtTurningPoint(alphaMax);
        double topAlpha = alphaMax;
        double bilimit = alpha0;

        if (bottomValue > targetVariance && topValue > targetVariance)
        {
            // see if ok at some intermediate point by stepping through
            int i = 1;
            while (i < steps && topValue > targetVariance)
            {
                topAlpha = alpha0 + ((alphaMax - alpha0) * (i + 0.0) / (steps + 0.0));
                topValue = ValueAtTurningPoint(topAlpha);
                ++i;
            }

            if (topValue <= targetVariance)
            {
                bilimit = alpha0 + ((topAlpha - alpha0) * (i - 2.0) / (steps + 0.0));
            }
        }

        if (bottomValue > targetVariance && topValue > targetVariance)
        {
            // see if ok at some intermediate point by stepping through, testing the bottom value it moves, where
            // QuantLib's tests the top value (MR3 (ix))
            int i = 1;
            while (i < steps && bottomValue > targetVariance)
            {
                bottomAlpha = alpha0 + ((alphaMin - alpha0) * (i + 0.0) / (steps + 0.0));
                bottomValue = ValueAtTurningPoint(bottomAlpha);
                ++i;
            }

            if (bottomValue <= targetVariance)
            {
                bilimit = alpha0 + ((bottomAlpha - alpha0) * (i - 2.0) / (steps + 0.0));
            }
        }

        if (bottomValue > targetVariance && topValue > targetVariance)
        {
            return false;
        }

        if (bottomValue <= targetVariance)
        {
            // then find root of increasing function (or as if increasing function)
            alpha = Bisection(targetVariance, bottomAlpha, bilimit, tolerance, ValueAtTurningPoint);
        }
        else
        {
            // find root of decreasing function (or as if decreasing function)
            alpha = Bisection(-targetVariance, bilimit, topAlpha, tolerance, MinusValueAtTurningPoint);
        }

        FinalPart(alpha, stepIndex, quadraticPart, linearPart, constantPart, out alpha, out a, out b, rateTwoVols);
        return true;
    }

    /// <summary>
    /// The α, a and b at which the second rate's volatilities give the caplet its target variance, at the α, among
    /// those that work, whose volatilities are the closest to the homogeneous ones.
    /// </summary>
    /// <param name="alpha0">The initial α.</param>
    /// <param name="stepIndex">The caplet's index.</param>
    /// <param name="rateOneVols">The first rate's volatilities, at least the step index plus one.</param>
    /// <param name="rateTwoHomogeneousVols">The second rate's homogeneous volatilities, at least the step index plus
    /// two.</param>
    /// <param name="correlations">The two rates' correlations, at least the step index plus one.</param>
    /// <param name="w0">The first rate's weight.</param>
    /// <param name="w1">The second rate's weight.</param>
    /// <param name="targetVariance">The caplet's target variance.</param>
    /// <param name="tolerance">The searches' tolerance.</param>
    /// <param name="alphaMax">The largest α.</param>
    /// <param name="alphaMin">The smallest α.</param>
    /// <param name="steps">The search's steps where no end works.</param>
    /// <param name="alpha">The α found.</param>
    /// <param name="a">The multiplier of the second rate's volatilities to the caplet's reset.</param>
    /// <param name="b">The multiplier of its volatility after it; NaN where the final part fails.</param>
    /// <param name="rateTwoVols">The second rate's new volatilities, written to the step index plus one.</param>
    /// <returns>Whether an α was found.</returns>
    /// <exception cref="ArgumentException">A vector is shorter than the step index reads.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stepIndex"/> or <paramref name="steps"/> is
    /// negative.</exception>
    public bool SolveWithMaxHomogeneity(
        double alpha0,
        int stepIndex,
        ReadOnlySpan<double> rateOneVols,
        ReadOnlySpan<double> rateTwoHomogeneousVols,
        ReadOnlySpan<double> correlations,
        double w0,
        double w1,
        double targetVariance,
        double tolerance,
        double alphaMax,
        double alphaMin,
        int steps,
        out double alpha,
        out double a,
        out double b,
        Span<double> rateTwoVols)
    {
        // QuantLib's (Size)steps loops about 2^64 times at a negative count (MR3 (xx))
        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        Setup(stepIndex, rateOneVols, rateTwoHomogeneousVols, correlations, w0, w1, targetVariance, rateTwoVols);

        // QuantLib's resize keeps the earlier solves' values, which a failed final part leaves in place
        Array.Resize(ref putativeVols, rateTwoHomogeneousVols.Length);
        alpha = a = b = double.NaN;

        double alpha1 = alphaMin;
        double alpha2 = alphaMax;

        // compute linear part with initial alpha
        bool alpha0OK = TestIfSolutionExists(alpha0);
        bool alphaMaxOK = TestIfSolutionExists(alphaMax);
        bool alphaMinOK = TestIfSolutionExists(alphaMin);

        bool foundOKPoint = alpha0OK || alphaMaxOK || alphaMinOK;

        if (foundOKPoint)
        {
            if (!alphaMinOK)
            {
                // lower alpha is bad
                if (alpha0OK)
                {
                    // must die somewhere in between
                    alpha1 = FindLowestOK(alphaMin, alpha0, tolerance, TestIfSolutionExists);
                }
                else
                {
                    // alphaMaxOK must be true to get here
                    alpha1 = FindLowestOK(alpha0, alphaMax, tolerance, TestIfSolutionExists);
                }
            }

            if (!alphaMaxOK)
            {
                // higher alpha is bad
                alpha2 = FindHighestOK(alpha1, alphaMax, tolerance, TestIfSolutionExists);
            }
            else
            {
                alpha2 = alphaMax;
            }
        }
        else
        {
            // ok let's see if we can find a value of alpha that works
            bool foundUpOK = false;
            bool foundDownOK = false;
            double alphaUp = alpha0, alphaDown = alpha0;
            double stepSize = (alphaMax - alpha0) / steps;

            for (int j = 0; j < steps && !foundUpOK && !foundDownOK; ++j)
            {
                alphaUp = alpha0 + (j * stepSize);
                foundUpOK = TestIfSolutionExists(alphaUp);
                alphaDown = alpha0 - (j * stepSize);
                foundDownOK = TestIfSolutionExists(alphaDown);
            }

            foundOKPoint = foundUpOK || foundDownOK;
            if (!foundOKPoint)
            {
                return false;
            }

            if (foundUpOK)
            {
                alpha1 = alphaUp;
                alpha2 = FindHighestOK(alpha1, alphaMax, tolerance, TestIfSolutionExists);
            }
            else
            {
                alpha2 = alphaDown;
                alpha1 = FindLowestOK(alphaMin, alpha2, tolerance, TestIfSolutionExists);
            }
        }

        // we have now found alpha1, alpha2 such that solution exists at endpoints. we now want to minimize within that
        // interval
        alpha = Minimize(alpha1, alpha2, tolerance, HomogeneityFailure, TestIfSolutionExists, quantLibsMinimizeTest, out _);

        FinalPart(alpha, stepIndex, ComputeQuadraticPart(alpha), ComputeLinearPart(alpha), constantPart, out alpha, out a, out b, rateTwoVols);

        return true;
    }

    // QuantLib's anonymous Bisection, also stopping where its point no longer moves (MR3 (xii)).
    private static double Bisection(double target, double low, double high, double tolerance, Func<double, double> value)
    {
        double x = 0.5 * (low + high);
        double y = value(x);

        do
        {
            if (y < target)
            {
                low = x;
            }
            else if (y > target)
            {
                high = x;
            }

            double next = 0.5 * (low + high);
            if (next == x)
            {
                break;
            }

            x = next;
            y = value(x);
        }
        while (Math.Abs(high - low) > tolerance);

        return x;
    }

    // QuantLib's anonymous FindHighestOK, also stopping where its point no longer moves (MR3 (xii)).
    private static double FindHighestOK(double low, double high, double tolerance, Func<double, bool> value)
    {
        double x = 0.5 * (low + high);
        bool ok = value(x);

        do
        {
            if (ok)
            {
                low = x;
            }
            else
            {
                high = x;
            }

            double next = 0.5 * (low + high);
            if (next == x)
            {
                break;
            }

            x = next;
            ok = value(x);
        }
        while (Math.Abs(high - low) > tolerance);

        return x;
    }

    // QuantLib's anonymous FindLowestOK, also stopping where its point no longer moves (MR3 (xii)).
    private static double FindLowestOK(double low, double high, double tolerance, Func<double, bool> value)
    {
        double x = 0.5 * (low + high);
        bool ok = value(x);

        do
        {
            if (ok)
            {
                high = x;
            }
            else
            {
                low = x;
            }

            double next = 0.5 * (low + high);
            if (next == x)
            {
                break;
            }

            x = next;
            ok = value(x);
        }
        while (Math.Abs(high - low) > tolerance);

        return x;
    }

    // QuantLib's anonymous Minimize, its feasibility test on the trial α (MR3 (x)), on the objective's value as
    // QuantLib's where asked (the alpha calibration's fallback), and also stopping where its interval and point no
    // longer move (MR3 (xii)).
    private static double Minimize(
        double low,
        double high,
        double tolerance,
        Func<double, double> value,
        Func<double, bool> condition,
        bool testTheValue,
        out bool failed)
    {
        double leftValue = value(low);
        double rightValue = value(high);
        double w = 0.5 * (3.0 - Math.Sqrt(5.0));
        double x = (w * low) + ((1 - w) * high);
        double midValue = value(x);

        failed = true;

        while (high - low > tolerance)
        {
            double oldLow = low, oldHigh = high, oldX = x;
            if (x - low > high - x)
            {
                // left interval is bigger
                double tentativeNewMid = (w * low) + ((1 - w) * x);
                double tentativeNewMidValue = value(tentativeNewMid);
                bool conditioner = condition(testTheValue ? tentativeNewMidValue : tentativeNewMid);
                if (!conditioner)
                {
                    if (condition(x))
                    {
                        return x;
                    }

                    return leftValue < rightValue ? low : high;
                }

                if (tentativeNewMidValue < midValue)
                {
                    // go left
                    high = x;
                    rightValue = midValue;
                    x = tentativeNewMid;
                    midValue = tentativeNewMidValue;
                }
                else
                {
                    // go right
                    low = tentativeNewMid;
                    leftValue = tentativeNewMidValue;
                }
            }
            else
            {
                double tentativeNewMid = (w * x) + ((1 - w) * high);
                double tentativeNewMidValue = value(tentativeNewMid);
                bool conditioner = condition(testTheValue ? tentativeNewMidValue : tentativeNewMid);
                if (!conditioner)
                {
                    if (condition(x))
                    {
                        return x;
                    }

                    return leftValue < rightValue ? low : high;
                }

                if (tentativeNewMidValue < midValue)
                {
                    // go right
                    low = x;
                    leftValue = midValue;
                    x = tentativeNewMid;
                    midValue = tentativeNewMidValue;
                }
                else
                {
                    // go left
                    high = tentativeNewMid;
                    rightValue = tentativeNewMidValue;
                }
            }

            if (low == oldLow && high == oldHigh && x == oldX)
            {
                break;
            }
        }

        failed = false;
        return x;
    }

    private void Setup(
        int stepIndex,
        ReadOnlySpan<double> rateOneVols,
        ReadOnlySpan<double> rateTwoHomogeneousVols,
        ReadOnlySpan<double> correlations,
        double w0,
        double w1,
        double targetVariance,
        ReadOnlySpan<double> rateTwoVols)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stepIndex);

        // QuantLib reads the vectors to the step index plus one or two, past shorter ones (MR3 (xx)).
        CheckLength(rateOneVols.Length, stepIndex + 1, nameof(rateOneVols));
        CheckLength(rateTwoHomogeneousVols.Length, stepIndex + 2, nameof(rateTwoHomogeneousVols));
        CheckLength(correlations.Length, stepIndex + 1, nameof(correlations));
        CheckLength(rateTwoVols.Length, stepIndex + 2, nameof(rateTwoVols));

        this.stepIndex = stepIndex;
        this.rateOneVols = rateOneVols.ToArray();
        this.rateTwoHomogeneousVols = rateTwoHomogeneousVols.ToArray();
        this.correlations = correlations.ToArray();
        this.w0 = w0;
        this.w1 = w1;
        totalVar = 0;
        for (int i = 0; i <= stepIndex + 1; ++i)
        {
            totalVar += rateTwoHomogeneousVols[i] * rateTwoHomogeneousVols[i];
        }

        this.targetVariance = targetVariance;

        // constant part will not depend on alpha
        constantPart = 0.0;
        for (int i = 0; i < stepIndex + 1; ++i)
        {
            constantPart += rateOneVols[i] * rateOneVols[i];
        }

        constantPart *= w0 * w0;
    }

    private static void CheckLength(int length, int required, string paramName)
    {
        if (!(length >= required))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"{length} values given, the step index reads {required}"), paramName);
        }
    }

    private double ComputeLinearPart(double alpha)
    {
        double cov = 0.0;
        parametricForm.SetAlpha(alpha);

        for (int i = 0; i < stepIndex + 1; ++i)
        {
            double vol1 = rateTwoHomogeneousVols[i] * parametricForm.Evaluate(i);
            cov += vol1 * rateOneVols[i] * correlations[i];
        }

        cov *= 2 * w0 * w1;
        return cov;
    }

    private double ComputeQuadraticPart(double alpha)
    {
        double var = 0.0;
        parametricForm.SetAlpha(alpha);

        for (int i = 0; i < stepIndex + 1; ++i)
        {
            double vol = rateTwoHomogeneousVols[i] * parametricForm.Evaluate(i);
            var += vol * vol;
        }

        var *= w1 * w1;
        return var;
    }

    private double HomogeneityFailure(double alpha)
    {
        FinalPart(alpha, stepIndex, ComputeQuadraticPart(alpha), ComputeLinearPart(alpha), constantPart, out _, out _, out _, putativeVols);

        double result = 0.0;
        for (int i = 0; i <= stepIndex + 1; ++i)
        {
            double val = putativeVols[i] - rateTwoHomogeneousVols[i];
            result += val * val;
        }

        return result;
    }

    private bool FinalPart(
        double alphaFound,
        int stepIndex,
        double quadraticPart,
        double linearPart,
        double constantPart,
        out double alpha,
        out double a,
        out double b,
        Span<double> rateTwoVols)
    {
        alpha = alphaFound;
        var q2 = new Quadratic(quadraticPart, linearPart, constantPart - targetVariance);
        parametricForm.SetAlpha(alpha);
        q2.Roots(out a, out _);

        double varSoFar = 0.0;
        for (int i = 0; i < stepIndex + 1; ++i)
        {
            rateTwoVols[i] = rateTwoHomogeneousVols[i] * parametricForm.Evaluate(i) * a;
            varSoFar += rateTwoVols[i] * rateTwoVols[i];
        }

        double varToFind = totalVar - varSoFar;
        if (varToFind < 0)
        {
            b = double.NaN;
            return false;
        }

        double requiredSd = Math.Sqrt(varToFind);
        b = requiredSd / (rateTwoHomogeneousVols[stepIndex + 1] * parametricForm.Evaluate(stepIndex));
        rateTwoVols[stepIndex + 1] = requiredSd;
        return true;
    }

    private double ValueAtTurningPoint(double alpha)
    {
        linearPart = ComputeLinearPart(alpha);
        quadraticPart = ComputeQuadraticPart(alpha);

        var q = new Quadratic(quadraticPart, linearPart, constantPart);
        double valueAtTP = q.ValueAtTurningPoint();
        return valueAtTP;
    }

    private double MinusValueAtTurningPoint(double alpha) => -ValueAtTurningPoint(alpha);

    private bool TestIfSolutionExists(double alpha)
    {
        bool aExists = ValueAtTurningPoint(alpha) < targetVariance;
        if (!aExists)
        {
            return false;
        }

        return FinalPart(alpha, stepIndex, ComputeQuadraticPart(alpha), ComputeLinearPart(alpha), constantPart, out _, out _, out _, putativeVols);
    }
}
