// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/discreteintegrals.hpp ql/math/integrals/discreteintegrals.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Simpson's rule on given points, unevenly spaced: the integral of a function known at abscissas x, each pair of
/// intervals by the parabola through its three points, an odd last interval by the trapezoid rule.
/// </summary>
/// <remarks>
/// QuantLib's function object, static here: it holds no state. Levy, D., Numerical Integration
/// (http://www2.math.umd.edu/~dlevy/classes/amsc466/lecture-notes/integration-chap.pdf).
/// </remarks>
public static class DiscreteSimpsonIntegral
{
    /// <summary>The integral of the values <paramref name="f"/> at the abscissas <paramref name="x"/>.</summary>
    /// <param name="x">The abscissas, in order.</param>
    /// <param name="f">The values, one per abscissa.</param>
    /// <returns>The integral; 0 for fewer than two points.</returns>
    /// <exception cref="ArgumentException">The two differ in number, with QuantLib's message.</exception>
    public static double Integrate(ReadOnlySpan<double> x, ReadOnlySpan<double> f)
    {
        int n = f.Length;
        if (!(n == x.Length))
        {
            throw new ArgumentException("inconsistent size", nameof(f));
        }

        if (n < 2)
        {
            return 0.0;
        }

        double sum = 0.0;

        for (int j = 0; j < n - 2; j += 2)
        {
            double dxj = x[j + 1] - x[j];
            double dxjp1 = x[j + 2] - x[j + 1];

            double alpha = dxjp1 * ((2 * dxj) - dxjp1);
            double dd = dxj + dxjp1;
            double k = dd / (6 * dxjp1 * dxj);
            double beta = dd * dd;
            double gamma = dxj * ((2 * dxjp1) - dxj);

            sum += k * ((alpha * f[j]) + (beta * f[j + 1]) + (gamma * f[j + 2]));
        }

        if ((n & 1) == 0)
        {
            sum += 0.5 * (x[n - 1] - x[n - 2]) * (f[n - 1] + f[n - 2]);
        }

        return sum;
    }
}
