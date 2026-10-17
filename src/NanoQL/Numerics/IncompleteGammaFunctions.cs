// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/incompletegamma.hpp ql/math/incompletegamma.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The regularized lower incomplete gamma function P(a, x), and its series and continued-fraction representations
/// (<c>incompletegamma.hpp</c>).
/// </summary>
/// <remarks>
/// The implementation of the algorithm was inspired by "Numerical Recipes in C", 2nd edition, Press, Teukolsky,
/// Vetterling, Flannery, chapter 6.
/// </remarks>
public static class IncompleteGammaFunctions
{
    /// <summary>
    /// The incomplete gamma function P(a, x): the series representation below a + 1, and one minus the continued
    /// fraction from there.
    /// </summary>
    /// <param name="a">The shape, positive.</param>
    /// <param name="x">The point, non-negative.</param>
    /// <param name="accuracy">The relative accuracy at which the representation stops.</param>
    /// <param name="maxIteration">The most iterations the representation takes.</param>
    /// <returns>P(a, x).</returns>
    /// <exception cref="ArgumentException"><paramref name="a"/> isn't positive, or <paramref name="x"/> is negative.</exception>
    /// <exception cref="NanoQLException">The accuracy isn't reached in <paramref name="maxIteration"/> iterations.</exception>
    public static double IncompleteGammaFunction(double a, double x, double accuracy = 1.0e-13, int maxIteration = 100)
    {
        if (!(a > 0.0))
        {
            throw new ArgumentException("non-positive a is not allowed", nameof(a));
        }

        if (!(x >= 0.0))
        {
            throw new ArgumentException("negative x non allowed", nameof(x));
        }

        if (x < (a + 1.0))
        {
            // Use the series representation
            return IncompleteGammaFunctionSeriesRepr(a, x, accuracy, maxIteration);
        }
        else
        {
            // Use the continued fraction representation
            return 1.0 - IncompleteGammaFunctionContinuedFractionRepr(a, x, accuracy, maxIteration);
        }
    }

    /// <summary>The series representation of P(a, x), which converges for x &lt; a + 1.</summary>
    /// <param name="a">The shape.</param>
    /// <param name="x">The point; 0 gives 0.</param>
    /// <param name="accuracy">The relative accuracy at which the series stops.</param>
    /// <param name="maxIteration">The most terms the series takes.</param>
    /// <returns>P(a, x).</returns>
    /// <exception cref="ArgumentException"><paramref name="a"/> isn't positive, and <paramref name="x"/> isn't 0.</exception>
    /// <exception cref="NanoQLException">The accuracy isn't reached in <paramref name="maxIteration"/> terms.</exception>
    public static double IncompleteGammaFunctionSeriesRepr(
        double a, double x, double accuracy = 1.0e-13, int maxIteration = 100)
    {
        if (x == 0.0)
        {
            return 0.0;
        }

        double gln = new GammaFunction().LogValue(a);
        double ap = a;
        double del = 1.0 / a;
        double sum = del;
        for (int n = 1; n <= maxIteration; n++)
        {
            ++ap;
            del *= x / ap;
            sum += del;
            if (Math.Abs(del) < Math.Abs(sum) * accuracy)
            {
                return sum * Math.Exp(-x + a * Math.Log(x) - gln);
            }
        }

        throw new NanoQLException("accuracy not reached");
    }

    /// <summary>
    /// The continued-fraction representation of Q(a, x) = 1 − P(a, x), which converges for x ≥ a + 1.
    /// </summary>
    /// <param name="a">The shape.</param>
    /// <param name="x">The point.</param>
    /// <param name="accuracy">The accuracy at which the fraction stops.</param>
    /// <param name="maxIteration">The most iterations the fraction takes.</param>
    /// <returns>1 − P(a, x).</returns>
    /// <exception cref="ArgumentException"><paramref name="a"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The accuracy isn't reached in <paramref name="maxIteration"/> iterations.</exception>
    public static double IncompleteGammaFunctionContinuedFractionRepr(
        double a, double x, double accuracy = 1.0e-13, int maxIteration = 100)
    {
        int i;
        double an, b, c, d, del, h;
        double gln = new GammaFunction().LogValue(a);
        b = x + 1.0 - a;
        c = 1.0 / NumericLimits.QL_EPSILON;
        d = 1.0 / b;
        h = d;
        for (i = 1; i <= maxIteration; i++)
        {
            an = -i * (i - a);
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
            del = d * c;
            h *= del;
            if (Math.Abs(del - 1.0) < accuracy)
            {
                return Math.Exp(-x + a * Math.Log(x) - gln) * h;
            }
        }

        throw new NanoQLException("accuracy not reached");
    }
}
