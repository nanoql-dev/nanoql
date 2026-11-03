// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/operators/numericaldifferentiation.hpp ql/methods/finitedifferences/operators/numericaldifferentiation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Methods;

/// <summary>
/// Numerical differentiation on arbitrarily spaced points: the derivative of a given order at x as Σᵢ wᵢ·f(x + oᵢ),
/// the weights by Fornberg's algorithm ("Calculation of Weights in Finite Difference Formulas", 1998).
/// </summary>
/// <remarks>
/// Immutable: the weights are computed at construction. Fornberg's algorithm divides by the offsets' differences, which
/// QuantLib doesn't check: repeated offsets give NaN weights, which its evaluation then skips, so x³'s first derivative
/// at 1 on offsets (−0.1, 0, 0, 0.1) comes out 0 for 3. Repeated offsets are refused here. Terms whose weight is at
/// most ε² in magnitude are skipped, as QuantLib's.
/// </remarks>
public sealed class NumericalDifferentiation
{
    private readonly ImmutableArray<double> offsets;
    private readonly ImmutableArray<double> w;
    private readonly Func<double, double>? f;

    /// <summary>Initializes a new instance of the <see cref="NumericalDifferentiation"/> class on given offsets.</summary>
    /// <param name="f">The function, or <see langword="null"/> (QuantLib's empty function) for the weights alone.</param>
    /// <param name="orderOfDerivative">The derivative's order, below the number of offsets.</param>
    /// <param name="offsets">The offsets oᵢ from x, distinct.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="orderOfDerivative"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// An offset is repeated, or there aren't more offsets than the derivative's order.
    /// </exception>
    public NumericalDifferentiation(Func<double, double>? f, int orderOfDerivative, ReadOnlySpan<double> offsets)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(orderOfDerivative);
        CheckDistinct(offsets);
        this.offsets = [.. offsets];
        w = CalcWeights(offsets, orderOfDerivative);
        this.f = f;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NumericalDifferentiation"/> class on equally spaced points.
    /// </summary>
    /// <param name="f">The function, or <see langword="null"/> (QuantLib's empty function) for the weights alone.</param>
    /// <param name="orderOfDerivative">The derivative's order, below the number of points.</param>
    /// <param name="stepSize">The points' spacing h.</param>
    /// <param name="steps">The number of points, above 1; for the central scheme, odd and above 2.</param>
    /// <param name="scheme">The points' placement around x.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="orderOfDerivative"/> or <paramref name="steps"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Too few points for the scheme or the order, or a step of 0, which repeats the points.
    /// </exception>
    public NumericalDifferentiation(
        Func<double, double>? f, int orderOfDerivative, double stepSize, int steps, NumericalDifferentiationScheme scheme)
        : this(f, orderOfDerivative, CalcOffsets(stepSize, steps, scheme))
    {
    }

    /// <summary>The derivative at <paramref name="x"/>: Σᵢ wᵢ·f(x + oᵢ), over the weights above ε² in
    /// magnitude.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="InvalidOperationException">No function was given, where QuantLib calls its empty one.</exception>
    public double Evaluate(double x)
    {
        if (f is null)
        {
            throw new InvalidOperationException("no function to differentiate: the instance was built for its weights");
        }

        double s = 0.0;
        for (int i = 0; i < w.Length; ++i)
        {
            if (Math.Abs(w[i]) > NumericLimits.QL_EPSILON * NumericLimits.QL_EPSILON)
            {
                s += w[i] * f(x + offsets[i]);
            }
        }

        return s;
    }

    /// <summary>The offsets oᵢ.</summary>
    /// <returns>The offsets.</returns>
    public ImmutableArray<double> Offsets() => offsets;

    /// <summary>The weights wᵢ.</summary>
    /// <returns>The weights.</returns>
    public ImmutableArray<double> Weights() => w;

    private static double[] CalcOffsets(double h, int n, NumericalDifferentiationScheme scheme)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n, "steps");
        if (!(n > 1))
        {
            throw new ArgumentException("number of steps must be greater than one", "steps");
        }

        var retVal = new double[n];
        switch (scheme)
        {
            case NumericalDifferentiationScheme.Central:
                if (!(n > 2 && (n % 2) != 0))
                {
                    throw new ArgumentException("number of steps must be an odd number greater than two", "steps");
                }

                for (int i = 0; i < n; ++i)
                {
                    retVal[i] = (i - (n / 2)) * h;
                }

                break;
            case NumericalDifferentiationScheme.Backward:
                for (int i = 0; i < n; ++i)
                {
                    retVal[i] = -(i * h);
                }

                break;
            case NumericalDifferentiationScheme.Forward:
                for (int i = 0; i < n; ++i)
                {
                    retVal[i] = i * h;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "unknown numerical differentiation scheme");
        }

        return retVal;
    }

    // Fornberg's algorithm divides by the offsets' differences
    private static void CheckDistinct(ReadOnlySpan<double> offsets)
    {
        for (int i = 1; i < offsets.Length; ++i)
        {
            for (int j = 0; j < i; ++j)
            {
                if (offsets[i] == offsets[j])
                {
                    throw new ArgumentException(
                        $"the offsets must be distinct: {offsets[i].ToString(CultureInfo.InvariantCulture)} is repeated",
                        nameof(offsets));
                }
            }
        }
    }

    // The weights alone, without a function: NthOrderDerivativeOp's, on the mesher's distinct locations.
    //
    // This is a C# implementation of the algorithm/pseudo code in
    // B. Fornberg, 1998. Calculation of Weights
    //                    in Finite Difference Formulas
    // https://amath.colorado.edu/faculty/fornberg/Docs/sirev_cl.pdf
    internal static ImmutableArray<double> CalcWeights(ReadOnlySpan<double> x, int m)
    {
        int bigN = x.Length;
        if (!(bigN > m))
        {
            throw new ArgumentException(
                "number of points must be greater than the order of the derivative", "orderOfDerivative");
        }

        // d[m][n][nu], zero-initialised as boost::multi_array's
        var d = new double[m + 1, bigN, bigN];
        d[0, 0, 0] = 1.0;
        double c1 = 1.0;

        for (int n = 1; n < bigN; ++n)
        {
            double c2 = 1.0;
            for (int nu = 0; nu < n; ++nu)
            {
                double c3 = x[n] - x[nu];
                c2 *= c3;

                for (int mm = 0; mm <= Math.Min(n, m); ++mm)
                {
                    d[mm, n, nu] = ((x[n] * d[mm, n - 1, nu]) - ((mm > 0) ? mm * d[mm - 1, n - 1, nu] : 0.0)) / c3;
                }
            }

            for (int mm = 0; mm <= m; ++mm)
            {
                d[mm, n, n] = c1 / c2
                    * (((mm > 0) ? mm * d[mm - 1, n - 1, n - 1] : 0.0) - (x[n - 1] * d[mm, n - 1, n - 1]));
            }

            c1 = c2;
        }

        var retVal = new double[bigN];
        for (int i = 0; i < bigN; ++i)
        {
            retVal[i] = d[m, bigN - 1, i];
        }

        return [.. retVal];
    }
}
