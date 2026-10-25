// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/discreteintegrals.hpp ql/math/integrals/discreteintegrals.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The trapezoid rule on given points: the integral of a function known at abscissas x, ½ Σ (x_{i+1} − x_i)(f_i + f_{i+1}).
/// </summary>
/// <remarks>
/// QuantLib's function object, static here: it holds no state. Levy, D., Numerical Integration
/// (http://www2.math.umd.edu/~dlevy/classes/amsc466/lecture-notes/integration-chap.pdf).
/// </remarks>
public static class DiscreteTrapezoidIntegral
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

        for (int i = 0; i < n - 1; ++i)
        {
            sum += (x[i + 1] - x[i]) * (f[i] + f[i + 1]);
        }

        return 0.5 * sum;
    }
}
