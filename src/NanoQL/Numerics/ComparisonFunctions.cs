// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/comparison.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>QuantLib's floating-point comparisons (<c>comparison.hpp</c>), after Knuth's advice.</summary>
/// <remarks>
/// <c>comparison.hpp</c> also declares <c>earlier_than</c>, the comparison of two objects by date, with no generic
/// implementation: each dated type gives its own, as <see cref="CashFlowFunctions.EarlierThan"/> does for cash flows.
/// Its form on shared pointers forwards to the pointees' comparison; a C# reference is the object, so the type's own
/// comparison serves for it, and neither form has code here.
/// </remarks>
public static class ComparisonFunctions
{
    /// <summary>
    /// Whether |x − y| ≤ ε|x| and |x − y| ≤ ε|y|, where ε is 42 times machine epsilon; with a zero operand, whether
    /// |x − y| &lt; ε².
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns><see langword="true"/> when close.</returns>
    public static bool Close(double x, double y) => Close(x, y, 42);

    /// <summary>
    /// Whether |x − y| ≤ ε|x| and |x − y| ≤ ε|y|, where ε is <paramref name="n"/> times machine epsilon; with a zero
    /// operand, whether |x − y| &lt; ε².
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <param name="n">The multiple of machine epsilon.</param>
    /// <returns><see langword="true"/> when close.</returns>
    public static bool Close(double x, double y, int n)
    {
        // Deals with +infinity and -infinity representations etc.
        if (x == y)
        {
            return true;
        }

        double diff = Math.Abs(x - y), tolerance = n * NumericLimits.QL_EPSILON;
        if (x == 0.0 || y == 0.0)
        {
            return diff < (tolerance * tolerance);
        }

        return diff <= tolerance * Math.Abs(x) && diff <= tolerance * Math.Abs(y);
    }

    /// <summary>
    /// Whether |x − y| ≤ ε|x| or |x − y| ≤ ε|y|, where ε is 42 times machine epsilon; with a zero operand, whether
    /// |x − y| &lt; ε².
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns><see langword="true"/> when close enough.</returns>
    public static bool CloseEnough(double x, double y) => CloseEnough(x, y, 42);

    /// <summary>
    /// Whether |x − y| ≤ ε|x| or |x − y| ≤ ε|y|, where ε is <paramref name="n"/> times machine epsilon; with a zero
    /// operand, whether |x − y| &lt; ε².
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <param name="n">The multiple of machine epsilon.</param>
    /// <returns><see langword="true"/> when close enough.</returns>
    public static bool CloseEnough(double x, double y, int n)
    {
        // Deals with +infinity and -infinity representations etc.
        if (x == y)
        {
            return true;
        }

        double diff = Math.Abs(x - y), tolerance = n * NumericLimits.QL_EPSILON;
        if (x == 0.0 || y == 0.0)
        {
            return diff < (tolerance * tolerance);
        }

        return diff <= tolerance * Math.Abs(x) || diff <= tolerance * Math.Abs(y);
    }
}
