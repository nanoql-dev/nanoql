// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The base class for 1-D interpolations: interpolated values from two sequences of equal length, the discretized
/// values of a variable and of a function of it.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: an interpolation copies its inputs and computes its coefficients when it is built. QuantLib's
/// interpolations keep iterators into their caller's data and recompute on <c>update()</c>; here a change of data is a
/// new interpolation, so neither <c>update()</c> nor <c>empty()</c> is ported. Extrapolation follows
/// <see cref="Extrapolator"/>: <see cref="ExtrapolatorExtensions.EnableExtrapolation{T}"/> returns a copy.
/// </para>
/// <para>
/// An extension point, as in QuantLib: a derived interpolation passes its inputs and the points it requires to the
/// base constructor, computes what it needs from <see cref="X"/> and <see cref="Y"/>, and implements the four
/// <c>…Core</c> members, which the public members call after the range check.
/// </para>
/// </remarks>
public abstract class Interpolation : Extrapolator
{
    private readonly ImmutableArray<double> xValues;
    private readonly ImmutableArray<double> yValues;

    /// <summary>
    /// Initializes a new instance of the <see cref="Interpolation"/> class from copies of its inputs: QuantLib's
    /// <c>templateImpl</c>.
    /// </summary>
    /// <param name="x">The x values, which must be sorted.</param>
    /// <param name="y">The y values, one per x value.</param>
    /// <param name="requiredPoints">The fewest points the interpolation takes.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="y"/> and <paramref name="x"/> have different sizes, or there are fewer points than required.
    /// </exception>
    protected Interpolation(ReadOnlySpan<double> x, ReadOnlySpan<double> y, int requiredPoints)
    {
        // QuantLib takes the y values from an iterator and reads one per x value.
        if (!(y.Length == x.Length))
        {
            throw new ArgumentException($"{y.Length} y values given for {x.Length} x values", nameof(y));
        }

        CheckRequiredPoints(x.Length, requiredPoints);
        xValues = [.. x];
        yValues = [.. y];
    }

    // QuantLib's templateImpl check, which a factory's CheckPoints runs for a point count alone.
    internal static void CheckRequiredPoints(int points, int requiredPoints)
    {
        if (!(points >= requiredPoints))
        {
            throw new ArgumentException(
                $"not enough points to interpolate: at least {requiredPoints} required, {points} provided", "x");
        }
    }

    /// <summary>Gets the first x value, the smallest of the sorted values.</summary>
    public double XMin => xValues[0];

    /// <summary>Gets the last x value, the largest of the sorted values.</summary>
    public double XMax => xValues[^1];

    /// <summary>Gets the x values, as stored.</summary>
    protected ReadOnlySpan<double> X => xValues.AsSpan();

    /// <summary>Gets the y values, as stored.</summary>
    protected ReadOnlySpan<double> Y => yValues.AsSpan();

    /// <summary>The x values.</summary>
    /// <returns>The values the interpolation was built from.</returns>
    public ImmutableArray<double> XValues() => xValues;

    /// <summary>The y values.</summary>
    /// <returns>The values the interpolation was built from.</returns>
    public ImmutableArray<double> YValues() => yValues;

    /// <summary>Whether <paramref name="x"/> is in the range [<see cref="XMin"/>, <see cref="XMax"/>], or close to an end.</summary>
    /// <param name="x">The point.</param>
    /// <returns><see langword="true"/> when in range.</returns>
    public bool IsInRange(double x)
    {
        double x1 = XMin, x2 = XMax;
        return (x >= x1 && x <= x2) || ComparisonFunctions.Close(x, x1) || ComparisonFunctions.Close(x, x2);
    }

    /// <summary>The interpolated value at <paramref name="x"/>: QuantLib's <c>operator()</c>.</summary>
    /// <param name="x">The point.</param>
    /// <param name="allowExtrapolation">Whether this call may extrapolate outside the range.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NanoQLException">The point is out of range and extrapolation isn't allowed.</exception>
    public double Evaluate(double x, bool allowExtrapolation = false)
    {
        CheckRange(x, allowExtrapolation);
        return ValueCore(x);
    }

    /// <summary>The primitive: the integral of the interpolation from <see cref="XMin"/> to <paramref name="x"/>.</summary>
    /// <param name="x">The point.</param>
    /// <param name="allowExtrapolation">Whether this call may extrapolate outside the range.</param>
    /// <returns>The primitive.</returns>
    /// <exception cref="NanoQLException">
    /// The point is out of range and extrapolation isn't allowed, or the interpolation has no primitive.
    /// </exception>
    public double Primitive(double x, bool allowExtrapolation = false)
    {
        CheckRange(x, allowExtrapolation);
        return PrimitiveCore(x);
    }

    /// <summary>The first derivative at <paramref name="x"/>.</summary>
    /// <param name="x">The point.</param>
    /// <param name="allowExtrapolation">Whether this call may extrapolate outside the range.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="NanoQLException">The point is out of range and extrapolation isn't allowed.</exception>
    public double Derivative(double x, bool allowExtrapolation = false)
    {
        CheckRange(x, allowExtrapolation);
        return DerivativeCore(x);
    }

    /// <summary>The second derivative at <paramref name="x"/>.</summary>
    /// <param name="x">The point.</param>
    /// <param name="allowExtrapolation">Whether this call may extrapolate outside the range.</param>
    /// <returns>The second derivative.</returns>
    /// <exception cref="NanoQLException">The point is out of range and extrapolation isn't allowed.</exception>
    public double SecondDerivative(double x, bool allowExtrapolation = false)
    {
        CheckRange(x, allowExtrapolation);
        return SecondDerivativeCore(x);
    }

    /// <summary>
    /// The index i of the interval [x<sub>i</sub>, x<sub>i+1</sub>] holding <paramref name="x"/>: 0 below the range,
    /// the last interval above it.
    /// </summary>
    /// <param name="x">The point.</param>
    /// <returns>The interval's index.</returns>
    protected int Locate(double x)
    {
        ReadOnlySpan<double> xs = X;
        if (x < xs[0])
        {
            return 0;
        }
        else if (x > xs[^1])
        {
            return xs.Length - 2;
        }
        else
        {
            return UpperBound(xs[..^1], x) - 1;
        }
    }

    /// <summary>The interpolated value at a point, after the range check.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The value.</returns>
    protected abstract double ValueCore(double x);

    /// <summary>The primitive at a point, after the range check.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The primitive.</returns>
    protected abstract double PrimitiveCore(double x);

    /// <summary>The first derivative at a point, after the range check.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The derivative.</returns>
    protected abstract double DerivativeCore(double x);

    /// <summary>The second derivative at a point, after the range check.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The second derivative.</returns>
    protected abstract double SecondDerivativeCore(double x);

    // std::upper_bound over a sorted range, in its order of comparisons: the first position whose element is greater
    // than x, or the range's length.
    private static int UpperBound(ReadOnlySpan<double> range, double x)
    {
        int first = 0, count = range.Length;
        while (count > 0)
        {
            int step = count / 2;
            int mid = first + step;
            if (x < range[mid])
            {
                count = step;
            }
            else
            {
                first = mid + 1;
                count -= step + 1;
            }
        }

        return first;
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    private void CheckRange(double x, bool extrapolate)
    {
        if (!(extrapolate || AllowsExtrapolation || IsInRange(x)))
        {
            throw new NanoQLException(
                $"interpolation range is [{Show(XMin)}, {Show(XMax)}]: extrapolation at {Show(x)} not allowed");
        }
    }
}
