// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/beta.hpp ql/math/beta.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The beta function and the incomplete beta function. The implementation of the algorithm was inspired by
/// "Numerical Recipes in C", 2nd edition, Press, Teukolsky, Vetterling, Flannery, chapter 6.
/// </summary>
public static class BetaFunctions
{
    /// <summary>The beta function, B(z, w) = Γ(z)Γ(w)/Γ(z + w), through ln Γ.</summary>
    /// <param name="z">The first argument, positive.</param>
    /// <param name="w">The second argument, positive.</param>
    /// <returns>B(z, w).</returns>
    /// <exception cref="ArgumentException">An argument isn't positive.</exception>
    public static double BetaFunction(double z, double w) =>
        Math.Exp(new GammaFunction().LogValue(z)
                 + new GammaFunction().LogValue(w)
                 - new GammaFunction().LogValue(z + w));

    /// <summary>The continued fraction of the incomplete beta function, by the modified Lentz method.</summary>
    /// <param name="a">The first parameter.</param>
    /// <param name="b">The second parameter.</param>
    /// <param name="x">The point.</param>
    /// <param name="accuracy">The accuracy of the last factor.</param>
    /// <param name="maxIteration">The maximum number of iterations.</param>
    /// <returns>The continued fraction.</returns>
    /// <exception cref="NanoQLException">The fraction doesn't converge within the iterations.</exception>
    public static double BetaContinuedFraction(
        double a, double b, double x, double accuracy = 1e-16, int maxIteration = 100)
    {
        double aa, del;
        double qab = a + b;
        double qap = a + 1.0;
        double qam = a - 1.0;
        double c = 1.0;
        double d = 1.0 - (qab * x / qap);
        if (Math.Abs(d) < NumericLimits.QL_EPSILON)
        {
            d = NumericLimits.QL_EPSILON;
        }

        d = 1.0 / d;
        double result = d;

        int m, m2;
        for (m = 1; m <= maxIteration; m++)
        {
            m2 = 2 * m;
            aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1.0 + (aa * d);
            if (Math.Abs(d) < NumericLimits.QL_EPSILON)
            {
                d = NumericLimits.QL_EPSILON;
            }

            c = 1.0 + (aa / c);
            if (Math.Abs(c) < NumericLimits.QL_EPSILON)
            {
                c = NumericLimits.QL_EPSILON;
            }

            d = 1.0 / d;
            result *= d * c;
            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1.0 + (aa * d);
            if (Math.Abs(d) < NumericLimits.QL_EPSILON)
            {
                d = NumericLimits.QL_EPSILON;
            }

            c = 1.0 + (aa / c);
            if (Math.Abs(c) < NumericLimits.QL_EPSILON)
            {
                c = NumericLimits.QL_EPSILON;
            }

            d = 1.0 / d;
            del = d * c;
            result *= del;
            if (Math.Abs(del - 1.0) < accuracy)
            {
                return result;
            }
        }

        throw new NanoQLException("a or b too big, or maxIteration too small in betacf");
    }

    /// <summary>The regularized incomplete beta function, I_x(a, b).</summary>
    /// <param name="a">The first parameter, positive.</param>
    /// <param name="b">The second parameter, positive.</param>
    /// <param name="x">The point, in [0, 1].</param>
    /// <param name="accuracy">The continued fraction's accuracy.</param>
    /// <param name="maxIteration">The continued fraction's maximum number of iterations.</param>
    /// <returns>I_x(a, b).</returns>
    /// <exception cref="ArgumentException">A parameter isn't positive, or the point is outside [0, 1].</exception>
    /// <exception cref="NanoQLException">The continued fraction doesn't converge.</exception>
    public static double IncompleteBetaFunction(
        double a, double b, double x, double accuracy = 1e-16, int maxIteration = 100)
    {
        if (!(a > 0.0))
        {
            throw new ArgumentException("a must be greater than zero", nameof(a));
        }

        if (!(b > 0.0))
        {
            throw new ArgumentException("b must be greater than zero", nameof(b));
        }

        if (x == 0.0)
        {
            return 0.0;
        }
        else if (x == 1.0)
        {
            return 1.0;
        }
        else if (!(x > 0.0 && x < 1.0))
        {
            throw new ArgumentException("x must be in [0,1]", nameof(x));
        }

        double result = Math.Exp(new GammaFunction().LogValue(a + b)
                                 - new GammaFunction().LogValue(a)
                                 - new GammaFunction().LogValue(b)
                                 + (a * Math.Log(x))
                                 + (b * Math.Log(1.0 - x)));

        if (x < (a + 1.0) / (a + b + 2.0))
        {
            return result * BetaContinuedFraction(a, b, x, accuracy, maxIteration) / a;
        }
        else
        {
            return 1.0 - (result * BetaContinuedFraction(b, a, 1.0 - x, accuracy, maxIteration) / b);
        }
    }
}
