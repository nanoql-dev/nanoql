// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/convolvedstudentt.hpp ql/experimental/math/convolvedstudentt.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The cumulative (generalized) Behrens-Fisher distribution: the exact distribution of a linear combination of any
/// number of Student t variables of odd orders, adapted from Witkovsky's algorithm (Journal of Statistical Planning
/// and Inference 94, 2001, 1-13).
/// </summary>
/// <remarks>
/// <para>
/// Immutable. The characteristic function of a t of order ν = 2n + 1 is a polynomial of degree n times
/// exp(−√ν|t|); the combination's is their product, and the Gil-Pelaez theorem inverts it term by term in closed form.
/// The orders must be odd.
/// </para>
/// <para>
/// A term of degree k divides (k − 1)!, or k! for the density, by the power k of a radius, and QuantLib's arithmetic
/// overflows where either leaves the doubles: from 172 on, on the cumulative of two t's of order 173, it reads NaN,
/// and its inverse refuses with "root not bracketed" (<c>convolvedstudentt.cpp:124-127</c>), where the series stays
/// accurate. There nanoql scales the factorial and the power together, in logarithms; QuantLib's bits wherever its
/// arithmetic doesn't overflow, through order 171, where the series is accurate to 2e-14 relative against 50 digits.
/// A documented divergence (the S-xcr plan's XR8 (xxvi); <c>Divergences/BehrensFisherHighOrderTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class CumulativeBehrensFisher
{
    private readonly ImmutableArray<double> polyConvolved;

    // cached factor in the exponential of the characteristic function
    private readonly double a;
    private readonly double a2;

    /// <summary>Initializes a new instance of the <see cref="CumulativeBehrensFisher"/> class.</summary>
    /// <param name="degreesFreedom">The degrees of freedom of the t variables, odd.</param>
    /// <param name="factors">The factors of the linear combination.</param>
    /// <exception cref="ArgumentException">The sizes differ ("Incompatible sizes in convolution."), or an order is even
    /// ("Even degree of freedom not allowed") or negative ("Negative degree of freedom not allowed").</exception>
    public CumulativeBehrensFisher(IReadOnlyList<int> degreesFreedom, IReadOnlyList<double> factors)
    {
        ArgumentNullException.ThrowIfNull(degreesFreedom);
        ArgumentNullException.ThrowIfNull(factors);
        if (degreesFreedom.Count != factors.Count)
        {
            throw new ArgumentException("Incompatible sizes in convolution.", nameof(factors));
        }

        foreach (int i in degreesFreedom)
        {
            if (i % 2 == 0)
            {
                throw new ArgumentException("Even degree of freedom not allowed", nameof(degreesFreedom));
            }

            if (i < 0)
            {
                throw new ArgumentException("Negative degree of freedom not allowed", nameof(degreesFreedom));
            }
        }

        DegreeFreedom = [.. degreesFreedom];
        Factors = [.. factors];
        var polynCharFnc = new List<double[]>(DegreeFreedom.Length);
        for (int i = 0; i < DegreeFreedom.Length; i++)
        {
            polynCharFnc.Add(PolynCharactT((DegreeFreedom[i] - 1) / 2));
        }

        // adjust the polynomial coefficients by the factors in the linear
        //   combination:
        for (int i = 0; i < DegreeFreedom.Length; i++)
        {
            double multiplier = 1.0;
            for (int k = 1; k < polynCharFnc[i].Length; k++)
            {
                multiplier *= Math.Abs(Factors[i]);
                polynCharFnc[i][k] *= multiplier;
            }
        }

        // convolution, here it is a product of polynomials and exponentials
        double[] convolved = [1.0];
        foreach (double[] p in polynCharFnc)
        {
            convolved = ConvolveVectorPolynomials(convolved, p);
        }

        // trim possible zeros that might have arised:
        int size = convolved.Length;
        while (size > 0 && convolved[size - 1] == 0.0)
        {
            size--;
        }

        polyConvolved = [.. convolved.AsSpan(0, size)];

        // cache 'a' value (the exponent)
        double sum = 0.0;
        for (int i = 0; i < DegreeFreedom.Length; i++)
        {
            sum += Math.Sqrt(DegreeFreedom[i]) * Math.Abs(Factors[i]);
        }

        a = sum;
        a2 = a * a;
    }

    /// <summary>Gets the degrees of freedom of the t variables.</summary>
    public ImmutableArray<int> DegreeFreedom { get; }

    /// <summary>Gets the factors of the linear combination.</summary>
    public ImmutableArray<double> Factors { get; }

    /// <summary>The cumulative probability of the combination, by the Gil-Pelaez theorem integrated analytically.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The probability.</returns>
    public double Value(double x)
    {
        // 1st & 0th terms with the table integration
        double integral = polyConvolved[0] * Math.Atan(x / a);
        double squared = a2 + (x * x);
        double rootsqr = Math.Sqrt(squared);
        double atan2xa = Math.Atan2(-x, a);
        if (polyConvolved.Length > 1)
        {
            integral += polyConvolved[1] * x / squared;
        }

        for (int exponent = 2; exponent < polyConvolved.Length; exponent++)
        {
            integral -= Term(polyConvolved[exponent], exponent - 1, Math.Sin(exponent * atan2xa), rootsqr, exponent);
        }

        return 0.5 + (integral / Math.PI);
    }

    /// <summary>The probability density of the combination, by the Gil-Pelaez theorem as <see cref="Value"/>.</summary>
    /// <param name="x">The value.</param>
    /// <returns>The density.</returns>
    public double Density(double x)
    {
        double squared = a2 + (x * x);
        double integral = polyConvolved[0] * a / squared;
        double rootsqr = Math.Sqrt(squared);
        double atan2xa = Math.Atan2(-x, a);
        for (int exponent = 1; exponent < polyConvolved.Length; exponent++)
        {
            integral += Term(polyConvolved[exponent], exponent, Math.Cos((exponent + 1) * atan2xa), rootsqr, exponent + 1);
        }

        return integral / Math.PI;
    }

    // The term c·n!·trig/r^k: QuantLib's arithmetic, or, where its factorial or its power leaves the doubles, the two
    // scaled together in logarithms.
    private static double Term(double c, int n, double trig, double rootsqr, int k)
    {
        double factorial = Factorial.Get(n);
        double power = Math.Pow(rootsqr, k);
        if (double.IsFinite(factorial) && double.IsFinite(power))
        {
            return c * factorial * trig / power;
        }

        return c * trig * Math.Exp(Factorial.Ln(n) - (k * Math.Log(rootsqr)));
    }

    // The polynomial of the characteristic function of a t of order 2n + 1.
    private static double[] PolynCharactT(int n)
    {
        int nu = (2 * n) + 1;
        double[] low = [1.0];
        double[] high = [1.0, Math.Sqrt(nu)];
        if (n == 0)
        {
            return low;
        }

        if (n == 1)
        {
            return high;
        }

        for (int k = 1; k < n; k++)
        {
            // 0, 1 and 2 coefs
            double[] recursionFactor = [0.0, 0.0, nu / (((2.0 * k) + 1.0) * ((2.0 * k) - 1.0))];
            double[] lowUp = ConvolveVectorPolynomials(recursionFactor, low);

            // add them up:
            for (int i = 0; i < high.Length; i++)
            {
                lowUp[i] += high[i];
            }

            low = high;
            high = lowUp;
        }

        return high;
    }

    private static double[] ConvolveVectorPolynomials(double[] v1, double[] v2)
    {
        double[] shorter = v1.Length < v2.Length ? v1 : v2;
        double[] longer = ReferenceEquals(shorter, v1) ? v2 : v1;

        int newDegree = v1.Length + v2.Length - 2;
        var resultB = new double[newDegree + 1];
        for (int polyOrdr = 0; polyOrdr < resultB.Length; polyOrdr++)
        {
            for (int i = Math.Max(0, polyOrdr - longer.Length + 1); i <= Math.Min(polyOrdr, shorter.Length - 1); i++)
            {
                resultB[polyOrdr] += shorter[i] * longer[polyOrdr - i];
            }
        }

        return resultB;
    }
}
