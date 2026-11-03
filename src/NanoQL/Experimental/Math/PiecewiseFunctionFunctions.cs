// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/piecewisefunction.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>A piecewise constant function: QuantLib's <c>QL_PIECEWISE_FUNCTION</c> macro.</summary>
[Experimental("NQL0001")]
public static class PiecewiseFunctionFunctions
{
    /// <summary>
    /// The value at x of the piecewise constant function, right-continuous with left limits, that takes the values
    /// Y[0], Y[1], ..., Y[n] on (−∞, X[0]), [X[0], X[1]), ..., [X[n − 1], ∞): QuantLib's
    /// <c>QL_PIECEWISE_FUNCTION(X, Y, x)</c>.
    /// </summary>
    /// <remarks>
    /// Normally Y has one value more than X. Further values are ignored; with fewer, the last value holds on the
    /// remaining intervals; with X empty, the function is the constant Y[0]. An empty Y is refused, where QuantLib's
    /// macro reads past it ("an invalid access occurs", its warning;
    /// <c>Conventions/ExperimentalMathRefusalTests.cs</c>).
    /// </remarks>
    /// <param name="X">The breaks, sorted ascending.</param>
    /// <param name="Y">The values, at least one.</param>
    /// <param name="x">The point.</param>
    /// <returns>Y at the first break above x, as <c>std::upper_bound</c> finds it, or Y's last value.</returns>
    /// <exception cref="ArgumentException"><paramref name="Y"/> is empty.</exception>
    public static double Evaluate(IReadOnlyList<double> X, IReadOnlyList<double> Y, double x)
    {
        ArgumentNullException.ThrowIfNull(X);
        ArgumentNullException.ThrowIfNull(Y);
        if (Y.Count == 0)
        {
            throw new ArgumentException("a piecewise function needs at least one value", nameof(Y));
        }

        // std::upper_bound's halving: the first break that x is below
        int first = 0, count = X.Count;
        while (count > 0)
        {
            int half = count / 2;
            int mid = first + half;
            if (x < X[mid])
            {
                count = half;
            }
            else
            {
                first = mid + 1;
                count -= half + 1;
            }
        }

        return Y[Math.Min(first, Y.Count - 1)];
    }
}
