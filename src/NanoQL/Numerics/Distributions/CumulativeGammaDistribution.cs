// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/gammadistribution.hpp ql/math/distributions/gammadistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The cumulative gamma distribution of shape a and unit scale: the regularized lower incomplete gamma function
/// P(a, x), by its series below a + 1 and its continued fraction from there.
/// </summary>
public sealed class CumulativeGammaDistribution
{
    private readonly double a;

    /// <summary>Initializes a new instance of the <see cref="CumulativeGammaDistribution"/> class.</summary>
    /// <param name="a">The shape, positive.</param>
    /// <exception cref="ArgumentException"><paramref name="a"/> isn't positive.</exception>
    public CumulativeGammaDistribution(double a)
    {
        if (!(a > 0.0))
        {
            throw new ArgumentException("invalid parameter for gamma distribution", nameof(a));
        }

        this.a = a;
    }

    /// <summary>The probability of a value below <paramref name="x"/>.</summary>
    /// <param name="x">The point; 0 at or below 0.</param>
    /// <returns>P(a, x).</returns>
    /// <exception cref="NanoQLException">Neither representation converges in 100 iterations.</exception>
    public double Evaluate(double x)
    {
        if (x <= 0.0)
        {
            return 0.0;
        }

        double gln = new GammaFunction().LogValue(a);

        if (x < (a + 1.0))
        {
            double ap = a;
            double del = 1.0 / a;
            double sum = del;
            for (int n = 1; n <= 100; n++)
            {
                ap += 1.0;
                del *= x / ap;
                sum += del;
                if (Math.Abs(del) < Math.Abs(sum) * 3.0e-7)
                {
                    return sum * Math.Exp(-x + a * Math.Log(x) - gln);
                }
            }
        }
        else
        {
            double b = x + 1.0 - a;
            double c = NumericLimits.QL_MAX_REAL;
            double d = 1.0 / b;
            double h = d;
            for (int n = 1; n <= 100; n++)
            {
                double an = -1.0 * n * (n - a);
                b += 2.0;
                d = an * d + b;
                if (Math.Abs(d) < NumericLimits.QL_EPSILON)
                {
                    d = NumericLimits.QL_EPSILON;
                }

                c = b + an / c;
                if (Math.Abs(c) < NumericLimits.QL_EPSILON)
                {
                    c = NumericLimits.QL_EPSILON;
                }

                d = 1.0 / d;
                double del = d * c;
                h *= del;
                if (Math.Abs(del - 1.0) < NumericLimits.QL_EPSILON)
                {
                    return 1.0 - h * Math.Exp(-x + a * Math.Log(x) - gln);
                }
            }
        }

        throw new NanoQLException("too few iterations");
    }
}
